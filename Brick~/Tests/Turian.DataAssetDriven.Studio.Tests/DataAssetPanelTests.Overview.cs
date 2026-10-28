using Gaya.Sdk;
using Turian.Editor.DataAssetDriven;

namespace Turian.Tests;

public sealed partial class DataAssetPanelTests
{
    /// <summary>The real overview controls select authored topology and create bounded batches with validation feedback.</summary>
    [Fact]
    public void OverviewPanelCreatesAssetsAndReportsInvalidInput()
    {
        var root = Path.Combine(Path.GetTempPath(), "turian-ddd-panel-" + Guid.NewGuid().ToString("N"));
        var assets = Path.Combine(root, "Assets");
        Directory.CreateDirectory(assets);
        try
        {
            var channel = new DataAssetEvent();
            Serializer.Save(Path.Combine(assets, "Event.dataasset"), channel);
            Serializer.Save(Path.Combine(assets, "Response.dataasset"), new DataAssetRaiseResponse { Events = [channel, new DataAssetEvent()] });
            var context = Substitute.For<IPluginContext>();
            var registrations = new ServiceCollection();
            context.Services.Returns(registrations);
            context.Panels.Returns(Substitute.For<IPanelRegistry>());
            context.Commands.Returns(Substitute.For<ICommandRegistry>());
            context.Menus.Returns(Substitute.For<IMenuRegistry>());
            PanelDescriptor? descriptor = null;
            context.Panels.When(registry => registry.Register(Arg.Any<PanelDescriptor>()))
                .Do(call => descriptor = call.Arg<PanelDescriptor>());
            new DataAssetStudioPlugin().Configure(context);
            var settings = new SettingsService();
            var app = new AppSettings { ProjectAbsoluteDir = root };
            settings.Set(app);
            var database = new AssetDatabase();
            using var build = new BuildManager(app, NullLogger.Instance);
            using var importer = new AssetImporter(NullLogger.Instance, database, settings, build);
            importer.StopMonitoring();
            var scene = Substitute.For<IPlaySceneHost>();
            scene.CurrentSceneRoot.Returns(new Node());
            registrations.AddSingleton(database).AddSingleton(settings);
            registrations.AddSingleton(provider => new PlayModeService(scene, database, provider, NullLogger.Instance));
            registrations.AddSingleton(provider => new AssetCreationCatalog(new AssetFileSystem(settings, importer),
                settings, build, NullLogger.Instance, provider.GetServices<IAssetCreationProvider>()));
            using var services = registrations.BuildServiceProvider();
            using var frame = new Frame(descriptor!.Factory(services));
            frame.Draw();
            frame.Click("ddd/overview/" + channel.Id);
            frame.Draw();
            frame.Click("ddd/refresh");
            frame.Type("ddd/createFolder", "Created");
            frame.Type("ddd/createCount", "2");
            frame.Click("ddd/createMany");
            Assert.Equal(2, Directory.EnumerateFiles(Path.Combine(assets, "Created"), "*.dataasset").Count());
            frame.Type("ddd/createCount", "invalid");
            frame.Click("ddd/createMany");
            Assert.Equal(2, Directory.EnumerateFiles(Path.Combine(assets, "Created"), "*.dataasset").Count());
            frame.Type("ddd/createCount", "1");
            frame.Type("ddd/createFolder", "../Outside");
            frame.Click("ddd/createMany");
            Assert.False(Directory.Exists(Path.Combine(root, "Outside")));
            frame.Type("ddd/search", "Event");
            frame.Draw();
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
