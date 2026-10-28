using Gaya.Sdk;
using Guinevere;
using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

/// <summary>Creates custom C# DDD classes from an existing payload type and queues a script recompile.</summary>
public sealed class DataAssetTypeGeneratorPanel(SettingsService settings, Func<long> recompile) : IPanel
{
    string prefix = "Custom";
    string payload = "float";
    string typeNamespace = "Usercode";
    string folder = "DDD";
    bool events = true;
    bool variables = true;
    bool constants = true;
    bool runtimeSets;
    string message = "";

    /// <inheritdoc />
    public void Render(Gui gui)
    {
        using var panel = gui.Node().Expand().Direction(Axis.Vertical).Gap(8).Padding(12).Enter();
        gui.ScrollY();
        Line(gui, "Generate concrete C# DDD types for an existing payload type.");
        if (settings.Settings is not { } project)
        {
            Line(gui, "Open a project to generate types.");
            return;
        }
        Line(gui, "Type prefix (e.g. Damage)");
        prefix = gui.TextInput(prefix, width: 0, placeholder: "Type prefix, e.g. Damage", id: "ddd/types/prefix");
        Line(gui, "Existing payload type (e.g. MyGame.Damage)");
        payload = gui.TextInput(payload, width: 0, placeholder: "Payload type, e.g. MyGame.Damage", id: "ddd/types/payload");
        Line(gui, "Generated C# namespace");
        typeNamespace = gui.TextInput(typeNamespace, width: 0, placeholder: "C# namespace", id: "ddd/types/namespace");
        Line(gui, "Output folder inside Assets");
        folder = gui.TextInput(folder, width: 0, placeholder: "Destination folder inside Assets", id: "ddd/types/folder");
        gui.Checkbox(ref events, "Event", id: "ddd/types/events");
        gui.Checkbox(ref variables, "Variable", id: "ddd/types/variables");
        gui.Checkbox(ref constants, "Constant", id: "ddd/types/constants");
        gui.Checkbox(ref runtimeSets, "Runtime Set (reference payloads)", id: "ddd/types/runtimeSets");
        if (GenerateButton(gui)) { Generate(project); }
        Line(gui, message);
        Line(gui, "After compilation, create instances under Create → DDD → Custom. Check Output for script errors.");
    }

    static bool GenerateButton(Gui gui)
    {
        using var button = gui.Node(-1, 30, "ddd/types/generate").ExpandWidth().Enter();
        var interaction = gui.GetInteractable();
        var theme = ThemeTokens.Current;
        if (gui.Pass == Pass.Pass2Render)
        {
            gui.DrawBackgroundRect(interaction.OnHover() ? theme.Hover : theme.Chrome, 3);
        }
        gui.DrawText("Generate C# types", theme.Text(13), theme.Ink);
        return gui.Pass == Pass.Pass2Render && interaction.OnClick();
    }

    void Generate(Turian.Engine.Core.IAppSettings project)
    {
        try
        {
            var families = DataAssetTypeFamilies.None;
            if (events) { families |= DataAssetTypeFamilies.Event; }
            if (variables) { families |= DataAssetTypeFamilies.Variable; }
            if (constants) { families |= DataAssetTypeFamilies.Constant; }
            if (runtimeSets) { families |= DataAssetTypeFamilies.RuntimeSet; }
            var paths = DataAssetTypeGenerator.Generate(project,
                new(prefix, payload, typeNamespace, folder, families));
            message = $"Created {paths.Count} C# type(s) in Assets/{folder}. Recompiling scripts…";
            recompile();
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            message = error.Message;
        }
    }

    static void Line(Gui gui, string text)
    {
        using var line = gui.Node(-1, 28).ExpandWidth().Enter();
        gui.DrawText(text, ThemeTokens.Current.Text(12), ThemeTokens.Current.Ink);
    }
}
