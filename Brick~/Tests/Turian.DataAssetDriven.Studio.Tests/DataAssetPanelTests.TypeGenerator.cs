using Turian.Editor.DataAssetDriven;

namespace Turian.Tests;

public sealed partial class DataAssetPanelTests
{
    /// <summary>The generator panel writes selected C# types, requests compilation and preserves existing files.</summary>
    [Fact]
    public void TypeGeneratorPanelWritesCustomTypes()
    {
        var root = Directory.CreateTempSubdirectory("ddd-generator-panel-").FullName;
        try
        {
            var settings = new SettingsService();
            var recompiles = 0;
            var panel = new DataAssetTypeGeneratorPanel(settings, () => recompiles++);
            using var frame = new Frame(panel);
            frame.Draw();
            settings.Set(new AppSettings { ProjectAbsoluteDir = root });
            frame.Draw();
            frame.Type("ddd/types/prefix", "Damage");
            frame.Type("ddd/types/payload", "int");
            frame.Type("ddd/types/namespace", "Game.Generated");
            frame.Type("ddd/types/folder", "Generated");
            frame.Click("ddd/types/generate");
            var folder = Path.Combine(root, "Assets", "Generated");
            Assert.Equal(3, Directory.EnumerateFiles(folder, "*.cs").Count());
            Assert.Equal(1, recompiles);
            frame.Click("ddd/types/generate");
            Assert.Equal(1, recompiles);
            frame.Type("ddd/types/prefix", "References");
            frame.Type("ddd/types/payload", "string");
            frame.Click("ddd/types/events");
            frame.Click("ddd/types/variables");
            frame.Click("ddd/types/constants");
            frame.Click("ddd/types/runtimeSets");
            frame.Click("ddd/types/generate");
            Assert.True(File.Exists(Path.Combine(folder, "ReferencesRuntimeSet.cs")));
            Assert.Equal(2, recompiles);
            frame.Click("ddd/types/runtimeSets");
            frame.Type("ddd/types/prefix", "Nothing");
            frame.Click("ddd/types/generate");
            Assert.Equal(2, recompiles);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
