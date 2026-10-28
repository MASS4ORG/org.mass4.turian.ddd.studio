using Gaya.Sdk;
using Guinevere;
using Turian.Editor.Core;
using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

sealed partial class DataAssetPanel : IPanel, IDisposable
{
    readonly PlayModeService play;
    readonly DataAssetTimeline timeline;
    readonly SettingsService settings;
    readonly AssetDatabase database;

    internal DataAssetPanel(PlayModeService play, DataAssetTimeline timeline, SettingsService settings,
        AssetDatabase database, AssetCreationCatalog? creation = null)
    {
        this.play = play;
        this.timeline = timeline;
        this.settings = settings;
        this.database = database;
        this.creation = creation;
        play.StateChanged += ClearStoppedAssets;
    }

    void ClearStoppedAssets(PlayState state)
    {
        if (state == PlayState.Stopped) { assets = []; }
    }

    public void Dispose() => play.StateChanged -= ClearStoppedAssets;
    IReadOnlyList<DataAsset> assets = [];
    Guid selected;
    string value = string.Empty;
    readonly List<string> validation = [];

    public void Render(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(4).Padding(8).Enter())
        {
            gui.ScrollY();
            if (Button(gui, "Validate wires / rewire report", "ddd/validate")) { Validate(); }
            foreach (var diagnostic in validation) { Line(gui, diagnostic); }
            var overview = Overview(gui);
            if (play.DataAssets is not { } session)
            {
                Line(gui, "Enter Play Mode to inspect DataAsset values and listeners.");
                return;
            }
            if (!overview) { AssetList(gui, session); }
            Details(gui, session);
            Timeline(gui);
        }
    }

    void AssetList(Gui gui, DataAssetSession session)
    {
        if (gui.Pass == Pass.Pass1Build) { assets = session.Assets; }
        foreach (var asset in assets)
        {
            if (!Button(gui, $"{asset.GetType().Name}  {asset.Id}", $"ddd/asset/{asset.Id}")) { continue; }
            selected = asset.Id;
            value = asset is IDataAssetValue variable ? variable.ReadValue()?.ToString() ?? "" : "";
        }
    }

    void Details(Gui gui, DataAssetSession session)
    {
        var current = session.Find(selected);
        if (current is IDataAssetEventDebug channel && Button(gui, "Raise default payload", "ddd/raise"))
        {
            channel.RaiseDefault();
        }
        if (current is IDataAssetValue shared) { Variable(gui, current, shared); }
        Line(gui, "Who listens");
        foreach (var listener in session.Listeners(selected))
        {
            Line(gui, $"Owner {listener.Owner}  priority {listener.Priority}  order {listener.Sequence}");
        }
    }

    void Variable(Gui gui, DataAsset current, IDataAssetValue shared)
    {
        Line(gui, $"Live value: {shared.ReadValue()}");
        value = gui.TextInput(value, placeholder: "Runtime value", id: "ddd/value");
        if (Button(gui, "Apply runtime value", "ddd/apply"))
        {
            var typed = ParseValue(current, value);
            if (typed is not null) { shared.TryWriteValue(typed); }
        }
    }

    static object? ParseValue(DataAsset asset, string text) => asset switch
    {
        DataAssetVariableInt when int.TryParse(text, out var parsed) => parsed,
        DataAssetVariableFloat when float.TryParse(text, out var parsed) => parsed,
        DataAssetVariableBool when bool.TryParse(text, out var parsed) => parsed,
        _ => null
    };

    void Timeline(Gui gui)
    {
        if (Button(gui, "Clear timeline", "ddd/clear")) { timeline.Clear(); }
        Line(gui, "Event timeline (newest first)");
        for (var i = timeline.Count - 1; i >= Math.Max(0, timeline.Count - 100); i--)
        {
            var trace = timeline[i];
            Line(gui, $"{trace.Sequence}: {trace.Topic}  listeners {trace.ListenerCount}  clock {trace.Timestamp}");
        }
    }

    void Validate()
    {
        validation.Clear();
        if (settings.Settings is not { } project) { return; }
        var loader = new RuntimeAssetLoader(database);
        foreach (var file in Directory.EnumerateFiles(project.AssetsAbsoluteDir, "*", SearchOption.AllDirectories)
                     .Where(static file => Path.GetExtension(file) is ".dataasset" or ".prefab" or ".scene")
                     .Order(StringComparer.Ordinal))
        {
            ValidateFile(file, project.AssetsAbsoluteDir, loader);
        }
        if (validation.Count == 0) { validation.Add("No dangling, mismatched or removed DataAsset wires found."); }
    }

    void ValidateFile(string file, string assetsDirectory, IAssetLoader loader)
    {
        try
        {
            var report = DataAssetWireValidator.Validate(File.ReadAllText(file),
                id => loader.LoadContentAsync<DataAsset>(id).GetAwaiter().GetResult());
            validation.AddRange(report.Select(diagnostic =>
                $"{Path.GetRelativePath(assetsDirectory, file)}: {diagnostic.Member}: "
                + $"{diagnostic.Kind} {diagnostic.Target}: {diagnostic.Message}"));
        }
        catch (Exception error) { validation.Add($"{file}: {error.Message}"); }
    }

    static void Line(Gui gui, string text)
    {
        using (gui.Node(-1, 24).Enter()) { gui.DrawText(text); }
    }

    static bool Button(Gui gui, string text, string id)
    {
        using (gui.Node(-1, 28, id).Enter())
        {
            gui.DrawText(text);
            return gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick();
        }
    }
}
