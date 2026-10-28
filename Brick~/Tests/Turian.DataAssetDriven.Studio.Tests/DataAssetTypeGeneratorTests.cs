using System.Runtime.Loader;
using Gaya.Sdk;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Turian.Editor.DataAssetDriven;

namespace Turian.Tests;

/// <summary>Generated concrete DDD types compile, expose creation menus and retain script identities.</summary>
[Collection(SerialTests.Name)]
public sealed class DataAssetTypeGeneratorTests : IDisposable
{
    readonly string root = Directory.CreateTempSubdirectory("ddd-types-").FullName;
    AppSettings Project => new() { ProjectAbsoluteDir = root };
    static DataAssetTypeRequest Request => new("Hit", "Game.Hit", "Game.Assets", "DDD", (DataAssetTypeFamilies)15);

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(root, recursive: true);

    /// <summary>The plugin exposes the generator through its menu, command and panel factory.</summary>
    [Fact]
    public void PluginExposesTypeGenerator()
    {
        var context = Substitute.For<IPluginContext>();
        var registrations = new ServiceCollection();
        context.Services.Returns(registrations);
        var panels = new List<PanelDescriptor>();
        var commands = new List<CommandDescriptor>();
        context.Panels.Returns(Substitute.For<IPanelRegistry>());
        context.Commands.Returns(Substitute.For<ICommandRegistry>());
        context.Menus.Returns(Substitute.For<IMenuRegistry>());
        context.Panels.When(registry => registry.Register(Arg.Any<PanelDescriptor>()))
            .Do(call => panels.Add(call.Arg<PanelDescriptor>()));
        context.Commands.When(registry => registry.Register(Arg.Any<CommandDescriptor>()))
            .Do(call => commands.Add(call.Arg<CommandDescriptor>()));
        new DataAssetStudioPlugin().Configure(context);
        var shell = Substitute.For<IShellHost>();
        registrations.AddSingleton(shell).AddSingleton(new SettingsService());
        registrations.AddSingleton(provider => new ProjectSession(provider, NullLogger.Instance));
        using var services = registrations.BuildServiceProvider();
        const string id = "turian.ddd.studio.generator";
        Assert.IsType<DataAssetTypeGeneratorPanel>(panels.Single(panel => panel.Id == id).Factory(services));
        commands.Single(command => command.Id == id).Execute(services);
        shell.Received().ShowPanel(id);
    }

    /// <summary>All four generated families compile for custom payloads and inherit the Studio family presentation.</summary>
    [Fact]
    public void GeneratedCustomTypesCompileAndAppearInCreationMenu()
    {
        var paths = DataAssetTypeGenerator.Generate(Project, Request);
        Assert.Equal(4, paths.Count);
        var syntax = paths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), cancellationToken: TestContext.Current.CancellationToken))
            .Append(CSharpSyntaxTree.ParseText("namespace Game; public sealed record Hit(int Amount);", cancellationToken: TestContext.Current.CancellationToken));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(DataAssetEvent).Assembly.Location).Distinct()
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CustomDdd" + Guid.NewGuid().ToString("N"), syntax, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var bytes = new MemoryStream();
        var result = compilation.Emit(bytes, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        bytes.Position = 0;
        var context = new AssemblyLoadContext("generated-ddd", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(bytes);
            var candidates = AssetCreationCatalog.CandidateAssemblies([assembly], [assembly]);
            Assert.Contains(assembly, candidates);
            var kinds = AssetCreationCatalog.Scan(candidates, NullLogger.Instance);
            Assert.Equal(4, kinds.Count);
            foreach (var kind in kinds)
            {
                Assert.StartsWith("DDD/Custom/Hit/", kind.MenuPath, StringComparison.Ordinal);
                Assert.IsAssignableFrom<DataAsset>(Activator.CreateInstance(kind.PayloadType!));
                var meta = Asset.Load(Path.Combine(Project.AssetsAbsoluteDir, "DDD", kind.PayloadType!.Name + ".cs.meta"));
                Assert.NotNull(meta);
                Assert.Contains(meta.Id.ToString(), File.ReadAllText(Path.Combine(Project.AssetsAbsoluteDir, "DDD", kind.PayloadType.Name + ".cs")), StringComparison.Ordinal);
            }
        }
        finally { context.Unload(); }
    }

    /// <summary>Invalid declarations are rejected before any file is written.</summary>
    [Theory]
    [InlineData("prefix")]
    [InlineData("namespace")]
    [InlineData("payload")]
    [InlineData("void")]
    [InlineData("pointer")]
    [InlineData("none")]
    [InlineData("unknown")]
    [InlineData("folder")]
    [InlineData("valueSet")]
    public void InvalidRequestsDoNotWriteSources(string scenario)
    {
        var request = scenario switch
        {
            "prefix" => Request with { Prefix = "class" },
            "namespace" => Request with { Namespace = "Game..Assets" },
            "payload" => Request with { PayloadType = "Game." },
            "void" => Request with { PayloadType = "void" },
            "pointer" => Request with { PayloadType = "int*" },
            "none" => Request with { Families = DataAssetTypeFamilies.None },
            "unknown" => Request with { Families = (DataAssetTypeFamilies)32 },
            "folder" => Request with { Folder = "../../Outside" },
            _ => Request with { PayloadType = "float" },
        };
        Assert.Throws<ArgumentException>(() => DataAssetTypeGenerator.Generate(Project, request));
        Assert.Empty(Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories));
    }

    /// <summary>Existing sources and orphan metadata are preserved without creating part of a requested batch.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingFilesAreNeverOverwritten(bool metadata)
    {
        var directory = Directory.CreateDirectory(Path.Combine(Project.AssetsAbsoluteDir, "DDD")).FullName;
        var path = Path.Combine(directory, "HitVariable.cs" + (metadata ? ".meta" : ""));
        File.WriteAllText(path, "original");
        Assert.Throws<IOException>(() => DataAssetTypeGenerator.Generate(Project, Request));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Single(Directory.EnumerateFiles(directory));
    }

    /// <summary>Primitive payloads work for event, variable and constant types; reference payloads allow runtime sets.</summary>
    [Theory]
    [InlineData("float", 7)]
    [InlineData("string", 15)]
    [InlineData("object", 15)]
    public void SupportedPayloadsGenerate(string payload, int families)
    {
        var paths = DataAssetTypeGenerator.Generate(Project, Request with { PayloadType = payload, Families = (DataAssetTypeFamilies)families });
        Assert.Equal(families == 7 ? 3 : 4, paths.Count);
    }
}
