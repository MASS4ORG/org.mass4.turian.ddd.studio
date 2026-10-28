using Gaya.Sdk;
using Turian.Editor.DataAssetDriven;

namespace Turian.Tests;

/// <summary>Designer management indexes authored topology and creates typed assets without generating game code.</summary>
public sealed class DataAssetOverviewTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "turian-ddd-overview-" + Guid.NewGuid().ToString("N"));
    string Assets => Path.Combine(root, "Assets");

    /// <summary>Removes the disposable project and its generated metadata.</summary>
    public void Dispose()
    {
        if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Installed provider entries cover each closed payload, and their appearances remain Studio-only.</summary>
    [Fact]
    public void ProvidersCoverClosedTypesAndPreserveGenericFallback()
    {
        using var services = Providers();
        var kinds = services.GetRequiredService<IAssetCreationProvider>().Kinds;
        var appearances = services.GetRequiredService<IAssetPresentationProvider>();
        var catalog = new AssetTypeCatalog([appearances]);
        foreach (var kind in kinds)
        {
            Assert.StartsWith("DDD/", kind.MenuPath, StringComparison.Ordinal);
            Assert.EndsWith(".dataasset", kind.DefaultName, StringComparison.Ordinal);
            var appearance = catalog.PresentationFor(kind.PayloadType);
            Assert.NotNull(appearance);
            Assert.False(string.IsNullOrWhiteSpace(appearance.Glyph));
            Assert.StartsWith("DDD/", appearance.Category, StringComparison.Ordinal);
        }
        Assert.Contains(kinds, kind => kind.PayloadType == typeof(DataAssetEventVector2));
        Assert.Contains(kinds, kind => kind.PayloadType == typeof(DataAssetRuntimeSetAssetEnum));
        Assert.NotNull(catalog.PresentationFor(typeof(DataAssetEvent<OverviewPayload>)));
    }

    /// <summary>Authored sources identify publishers/listeners, orphans and dangling wires without retaining live assets.</summary>
    [Fact]
    public void OverviewFindsRolesFiltersOrphansAndMalformedContent()
    {
        Directory.CreateDirectory(Assets);
        var channel = new DataAssetEvent();
        var variable = new DataAssetVariableInt();
        var orphan = new DataAssetConstInt();
        var missing = new DataAssetEvent();
        Save(channel, "Click");
        Save(variable, "Score");
        Save(orphan, "Unused");
        Save(new DataAssetRaiseResponse { Events = [channel, missing] }, "Publisher");
        Save(new DataAssetStateTransition { Trigger = channel }, "Transition");
        Save(new DataAssetIntResponse { Variable = variable }, "Response");
        Save(new InputReader(), "Input");
        Save(new AssetEnumValue(), "Faction");
        Save(new DataAssetRuntimeSetNode(), "Set");
        File.WriteAllText(Path.Combine(Assets, "listener.prefab"), Serializer.Serialize<Component>(
            new DataAssetListenerComponent { Event = channel }));
        File.WriteAllText(Path.Combine(Assets, "invalid.dataasset"), "{");
        File.WriteAllText(Path.Combine(Assets, "unknown.dataasset"), "{}");
        File.WriteAllText(Path.Combine(Assets, "array.prefab"), "[]");
        var index = DataAssetOverview.Scan(Assets);
        Assert.Contains(index.Wires, wire => wire.Target == channel.Id && wire.Role == "Raises");
        Assert.Contains(index.Wires, wire => wire.Target == channel.Id && wire.Role == "Listens");
        Assert.Contains(index.Wires, wire => wire.Target == variable.Id && wire.Role == "References");
        Assert.True(index.Assets.Single(asset => asset.Id == orphan.Id).Orphan);
        Assert.False(index.Assets.Single(asset => asset.Id == channel.Id).Orphan);
        Assert.Single(DataAssetOverview.Filter(index, "score"));
        Assert.Single(DataAssetOverview.Filter(index, "", TypeRegistry.GetIdOrThrow(typeof(DataAssetConstInt))));
        Assert.All(DataAssetOverview.Filter(index, "", orphanOnly: true), asset => Assert.True(asset.Orphan));
        Assert.Contains(index.Diagnostics, diagnostic => diagnostic.Contains(missing.Id.ToString(), StringComparison.Ordinal));
        var external = DataAssetOverview.Scan(Assets, id => id == missing.Id);
        Assert.DoesNotContain(external.Diagnostics, diagnostic => diagnostic.Contains(missing.Id.ToString(), StringComparison.Ordinal));
        Assert.Empty(DataAssetOverview.Scan(Path.Combine(root, "absent")).Assets);
        Save(channel, "Duplicate");
        Assert.Contains(DataAssetOverview.Scan(Assets).Diagnostics,
            diagnostic => diagnostic.Contains("duplicate asset GUID", StringComparison.Ordinal));
    }

    /// <summary>Generated listener metadata identifies channels even when their field names do not follow conventions.</summary>
    [Fact]
    public void OverviewFindsGeneratedListenersByAttribute()
    {
        Directory.CreateDirectory(Assets);
        var channel = new DataAssetEvent();
        Save(channel, "Signal");
        File.WriteAllText(Path.Combine(Assets, "listener.prefab"), Serializer.Serialize<Component>(
            new OverviewNamedListener { Notifications = channel }));
        var wire = Assert.Single(DataAssetOverview.Scan(Assets).Wires);
        Assert.Equal(channel.Id, wire.Target);
        Assert.Equal("Notifications", wire.Member);
        Assert.Equal("Listens", wire.Role);
    }

    /// <summary>Bulk creation uses unique names, seeds enum names and rejects writes outside the project or unbounded counts.</summary>
    [Fact]
    public void BulkCreationUsesCatalogAndInProjectUniqueNames()
    {
        Directory.CreateDirectory(Assets);
        using var services = Providers();
        var provider = services.GetRequiredService<IAssetCreationProvider>();
        var settings = new SettingsService();
        var app = new AppSettings { ProjectAbsoluteDir = root };
        settings.Set(app);
        using var build = new BuildManager(app, NullLogger.Instance);
        using var importer = new AssetImporter(NullLogger.Instance, new AssetDatabase(), settings, build);
        importer.StopMonitoring();
        var catalog = new AssetCreationCatalog(new AssetFileSystem(settings, importer), settings, build,
            NullLogger.Instance, [provider]);
        var kind = provider.Kinds.Single(kind => kind.PayloadType == typeof(DataAssetVariableBool));
        Assert.Equal(3, DataAssetOverview.CreateMany(catalog, kind, Assets, "Variables", 3));
        Assert.Equal(3, Directory.EnumerateFiles(Path.Combine(Assets, "Variables"), "*.dataasset").Count());
        var enumeration = provider.Kinds.Single(kind => kind.PayloadType == typeof(AssetEnumValue));
        Assert.Equal(1, DataAssetOverview.CreateMany(catalog, enumeration, Assets, "Factions", 1));
        var enumFile = Directory.EnumerateFiles(Path.Combine(Assets, "Factions"), "*.dataasset").Single();
        Assert.Equal(Path.GetFileNameWithoutExtension(enumFile), Serializer.Load<AssetEnumValue>(enumFile)!.Name);
        Assert.Throws<ArgumentException>(() => DataAssetOverview.CreateMany(catalog, kind, Assets, "../outside", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => DataAssetOverview.CreateMany(catalog, kind, Assets, "", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DataAssetOverview.CreateMany(catalog, kind, Assets, "", 101));
        Assert.Equal(0, DataAssetOverview.CreateMany(catalog, new AssetCreationKind("Unknown", "Unknown"), Assets, "", 1));
    }

    void Save(DataAsset asset, string name) => Serializer.Save(Path.Combine(Assets, name + ".dataasset"), asset);

    static ServiceProvider Providers()
    {
        var context = Substitute.For<IPluginContext>();
        var services = new ServiceCollection();
        context.Services.Returns(services);
        context.Panels.Returns(Substitute.For<IPanelRegistry>());
        context.Commands.Returns(Substitute.For<ICommandRegistry>());
        context.Menus.Returns(Substitute.For<IMenuRegistry>());
        new DataAssetStudioPlugin().Configure(context);
        return services.BuildServiceProvider();
    }

    readonly record struct OverviewPayload(int Value);
}

/// <summary>A generated listener whose source field has a designer-defined name.</summary>
[TypeId("f7ea6baa-4c74-4818-b389-6380bb4e3c83")]
public sealed partial class OverviewNamedListener : Component
{
    /// <summary>The event consumed through generated subscription ownership.</summary>
    public DataAssetEvent? Notifications { get; set; }

    [Turian.DataAssetListen(nameof(Notifications))]
    void Receive() { }
}
