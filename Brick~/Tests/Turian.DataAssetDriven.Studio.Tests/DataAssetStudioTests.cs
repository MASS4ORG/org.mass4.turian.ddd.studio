using Gaya.Sdk;
using Turian.Editor.DataAssetDriven;

namespace Turian.Tests;

/// <summary>Studio tracing and edit-time rewire diagnostics are independent of dispatch.</summary>
public sealed class DataAssetStudioTests
{
    /// <summary>Every concrete designer asset has a creation attribute and a packaged, decodable icon.</summary>
    [Fact]
    public void DesignerAssetsHaveCreationAttributesAndIcons()
    {
        var context = Substitute.For<IPluginContext>();
        var registrations = new ServiceCollection();
        context.Services.Returns(registrations);
        context.Panels.Returns(Substitute.For<IPanelRegistry>());
        context.Commands.Returns(Substitute.For<ICommandRegistry>());
        context.Menus.Returns(Substitute.For<IMenuRegistry>());
        new DataAssetStudioPlugin().Configure(context);
        using var services = registrations.BuildServiceProvider();
        var kinds = services.GetRequiredService<IAssetCreationProvider>().Kinds;
        var presentations = services.GetRequiredService<IAssetPresentationProvider>().Presentations;
        Assert.Equal(27, kinds.Count);
        Assert.Equal(kinds.Count, kinds.Select(kind => kind.PayloadType).Distinct().Count());
        foreach (var kind in kinds)
        {
            Assert.NotNull(kind.PayloadType!.GetCustomAttributes(typeof(CreateAssetMenuAttribute), false).SingleOrDefault());
            var presentation = Assert.Single(presentations, entry => entry.PayloadType == kind.PayloadType);
            Assert.NotNull(presentation.ImageData);
            using var image = SkiaSharp.SKImage.FromEncodedData(presentation.ImageData);
            Assert.NotNull(image);
            Assert.Equal(96, image.Width);
        }
    }

    /// <summary>The ring retains the newest raises in timeline order and can be cleared without retaining runtime owners.</summary>
    [Fact]
    public void TimelineBoundsAndOrdering()
    {
        var timeline = new DataAssetTimeline();
        for (var i = 0; i < 1100; i++) { timeline.OnRaised(new DataAssetEventTrace(i, Guid.Empty, i, 1)); }
        Assert.Equal(1024, timeline.Count);
        Assert.Equal(76, timeline[0].Sequence);
        Assert.Equal(1099, timeline[1023].Sequence);
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline[1024]);
        timeline.Clear();
        Assert.Equal(0, timeline.Count);
    }

    /// <summary>Collection wires retain GUID identity and malformed GUIDs are reported before resolution.</summary>
    [Fact]
    public void ValidatorChecksCollectionsAndInvalidGuids()
    {
        var channel = new DataAssetEvent();
        var response = new DataAssetRaiseResponse { Events = [channel] };
        var json = Serializer.Serialize<DataAsset>(response);
        Assert.Empty(DataAssetWireValidator.Validate(json, _ => channel));
        Assert.Equal("Dangling", Assert.Single(DataAssetWireValidator.Validate(json, _ => null)).Kind);
        var invalid = json.Replace(channel.Id.ToString(), "broken-guid", StringComparison.Ordinal);
        Assert.Equal("InvalidGuid", Assert.Single(DataAssetWireValidator.Validate(invalid, _ => channel)).Kind);
        var array = new DataAssetValidationArray { Events = [channel] };
        Assert.Empty(DataAssetWireValidator.Validate(Serializer.Serialize<DataAsset>(array), _ => channel));
    }

    /// <summary>Inline references inherit their compiled nested type and enclosing owner for wire diagnostics.</summary>
    [Theory]
    [InlineData("valid", "")]
    [InlineData("missing", "Dangling")]
    [InlineData("wrong", "TypeMismatch")]
    [InlineData("renamed", "Rewire")]
    public void ValidatorChecksNestedVariableReferences(string scenario, string expected)
    {
        var variable = new DataAssetVariableInt();
        var source = new DataAssetValidationReference
        {
            Amount = new() { UseConstant = false, Variable = variable }
        };
        var json = Serializer.Serialize<DataAsset>(source);
        if (scenario == "renamed") { json = json.Replace("Variable", "OldVariable", StringComparison.Ordinal); }
        var result = DataAssetWireValidator.Validate(json,
            _ => scenario == "missing" ? null : scenario == "wrong" ? new DataAssetEvent() : variable);
        if (expected.Length == 0) { Assert.Empty(result); return; }
        var diagnostic = Assert.Single(result);
        Assert.Equal(expected, diagnostic.Kind);
        Assert.Equal(source.Id, diagnostic.Owner);
        Assert.StartsWith("Amount.", diagnostic.Member, StringComparison.Ordinal);
    }

    /// <summary>Missing targets, changed payload types and renamed members produce actionable rewire reports.</summary>
    [Theory]
    [InlineData("dangling", "Dangling")]
    [InlineData("mismatch", "TypeMismatch")]
    [InlineData("refactor", "Rewire")]
    public void ValidatorReportsBrokenWires(string scenario, string kind)
    {
        var source = new DataAssetVariableInt { OnChanged = new DataAssetEvent<int>() };
        var target = source.OnChanged.Id;
        var json = Serializer.Serialize<DataAsset>(source);
        if (scenario == "refactor") { json = json.Replace("OnChanged", "RemovedChannel", StringComparison.Ordinal); }
        var report = DataAssetWireValidator.Validate(json, _ => scenario == "dangling" ? null : new DataAssetEvent());
        var diagnostic = Assert.Single(report);
        Assert.Equal(target, diagnostic.Target);
        Assert.Equal(kind, diagnostic.Kind);
    }
}

/// <summary>An array wire used to validate source-defined event collection types.</summary>
[TypeId("ce2066c6-2f7c-40b3-b29f-0818a9c0c40d")]
public sealed class DataAssetValidationArray : DataAsset
{
    /// <summary>The event assets referenced by this payload.</summary>
    public DataAssetEvent[] Events { get; set; } = [];
}

/// <summary>A nested designer value used to validate compiled member-type propagation.</summary>
[TypeId("ba77bd4b-5e0f-4db5-86b2-a5eb6663fa04")]
public sealed class DataAssetValidationReference : DataAsset
{
    /// <summary>The inline-or-shared variable field.</summary>
    public Turian.Engine.DataAssetDriven.DataAssetReference<int> Amount { get; set; } = new();
}
