using System.Numerics;
using Gaya.Sdk;
using Guinevere;
using SkiaSharp;
using Turian.Editor.DataAssetDriven;

namespace Turian.Tests;

/// <summary>Exercises the installed plugin's actual panel and service contributions without a desktop window.</summary>
[Collection(SerialTests.Name)]
public sealed partial class DataAssetPanelTests
{
    /// <summary>Designer tools resolve their services, show live assets, raise channels and edit supported scalar types.</summary>
    [Fact]
    public void PluginPanelWiresLiveAssetsAndResetsWithPlay()
    {
        var context = Substitute.For<IPluginContext>();
        var registrations = new ServiceCollection();
        context.Services.Returns(registrations);
        var panels = Substitute.For<IPanelRegistry>();
        context.Panels.Returns(panels);
        var commands = Substitute.For<ICommandRegistry>();
        context.Commands.Returns(commands);
        CommandDescriptor? command = null;
        commands.When(registry => registry.Register(Arg.Any<CommandDescriptor>()))
            .Do(call => command = call.Arg<CommandDescriptor>());
        context.Menus.Returns(Substitute.For<IMenuRegistry>());
        PanelDescriptor? descriptor = null;
        panels.When(registry => registry.Register(Arg.Any<PanelDescriptor>()))
            .Do(call => descriptor = call.Arg<PanelDescriptor>());
        new DataAssetStudioPlugin().Configure(context);
        var scene = Substitute.For<IPlaySceneHost>();
        scene.CurrentSceneRoot.Returns(new Node());
        var database = new AssetDatabase();
        var shell = Substitute.For<IShellHost>();
        registrations.AddSingleton<IShellHost>(shell);
        registrations.AddSingleton(database).AddSingleton(new SettingsService());
        registrations.AddSingleton(provider => new PlayModeService(scene, database, provider, NullLogger.Instance));
        using var services = registrations.BuildServiceProvider();
        var creation = services.GetRequiredService<IAssetCreationProvider>();
        Assert.Contains(creation.Kinds, static kind => kind.PayloadType == typeof(DataAssetVariableInt));
        var timeline = services.GetRequiredService<DataAssetTimeline>();
        Assert.Same(timeline, services.GetRequiredService<IDataAssetEventObserver>());
        var play = services.GetRequiredService<PlayModeService>();
        command!.Execute(services);
        shell.Received().ShowPanel(descriptor!.Id);
        var panel = descriptor.Factory(services);
        using var frame = new Frame(panel);
        frame.Draw();
        frame.Click("ddd/validate");
        Assert.True(play.Start());
        try
        {
            var session = play.DataAssets!;
            var channel = new DataAssetEvent();
            var values = new DataAsset[]
            {
                new DataAssetVariableInt { DefaultValue = 10 },
                new DataAssetVariableFloat { DefaultValue = 2.5f },
                new DataAssetVariableBool { DefaultValue = true }
            };
            session.Attach(channel);
            foreach (var value in values) { session.Attach(value); }
            var raised = 0;
            var owner = Guid.NewGuid();
            using var handle = channel.Subscribe(() => raised++, priority: 3, owner: owner);
            frame.Draw();
            frame.Click($"ddd/asset/{channel.Id}");
            frame.Draw();
            frame.Click("ddd/raise");
            Assert.Equal(1, raised);
            Assert.Equal(1, timeline.Count);
            Assert.True(timeline[0].Timestamp > 0);
            Assert.Equal(1, timeline[0].ListenerCount);
            var listener = Assert.Single(session.Listeners(channel.Id));
            Assert.Equal(owner, listener.Owner);
            Assert.Equal(3, listener.Priority);
            foreach (var value in values)
            {
                frame.Click($"ddd/asset/{value.Id}");
                frame.Draw();
                frame.Click("ddd/apply");
            }
            frame.Click("ddd/clear");
            Assert.Equal(0, timeline.Count);
        }
        finally { play.Stop(); }
        frame.Draw();
        Assert.Null(play.DataAssets);
        var directory = Path.Combine(Path.GetTempPath(), $"turian-ddd-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "Assets"));
        try
        {
            services.GetRequiredService<SettingsService>().Set(new AppSettings { ProjectAbsoluteDir = directory });
            frame.Click("ddd/validate");
            var broken = new DataAssetVariableInt { OnChanged = new DataAssetEvent<int>() };
            File.WriteAllText(Path.Combine(directory, "Assets", "broken.dataasset"), Serializer.Serialize<DataAsset>(broken));
            File.WriteAllText(Path.Combine(directory, "Assets", "invalid.dataasset"), "{");
            frame.Click("ddd/validate");
            frame.Draw();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    sealed class Frame(IPanel panel) : IDisposable
    {
        readonly SKSurface surface = SKSurface.Create(new SKImageInfo(1000, 1600));
        readonly Font font = Font.FromFamilyName("sans-serif", 14);
        readonly IInputHandler input = Substitute.For<IInputHandler>();
        readonly Gui gui = new();

        internal void Draw()
        {
            gui.Input = input;
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas, font, font);
            panel.Render(gui);
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            panel.Render(gui);
            gui.Render();
            gui.EndFrame();
        }

        internal void Click(string id)
        {
            var node = Find(gui.RootNode!, id) ?? throw new InvalidOperationException($"Missing control {id}");
            input.MousePosition.Returns(node.Center);
            input.PrevMousePosition.Returns(node.Center);
            input.IsMouseButtonPressed(MouseButton.Left).Returns(true);
            Draw();
            input.IsMouseButtonPressed(MouseButton.Left).Returns(false);
            input.MousePosition.Returns(new Vector2(-1, -1));
        }

        internal void Type(string id, string text)
        {
            Click(id);
            input.IsKeyDown(KeyboardKey.LeftControl).Returns(true);
            input.IsKeyPressed(KeyboardKey.A).Returns(true);
            Draw();
            input.IsKeyDown(KeyboardKey.LeftControl).Returns(false);
            input.IsKeyPressed(KeyboardKey.A).Returns(false);
            input.GetTypedCharacters().Returns(text);
            Draw();
            input.GetTypedCharacters().Returns(string.Empty);
        }

        static LayoutNode? Find(LayoutNode root, string id)
        {
            if (root.Id == id) { return root; }
            foreach (var child in root.Children)
            {
                if (Find(child, id) is { } found) { return found; }
            }
            return null;
        }

        public void Dispose()
        {
            (panel as IDisposable)?.Dispose();
            surface.Dispose();
        }
    }
}
