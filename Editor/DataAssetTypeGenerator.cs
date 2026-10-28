using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

/// <summary>The concrete DDD families generated for a custom payload type.</summary>
[Flags]
public enum DataAssetTypeFamilies
{
    /// <summary>No families selected.</summary>
    None = 0,
    /// <summary>A typed event channel.</summary>
    Event = 1,
    /// <summary>A session-owned mutable value.</summary>
    Variable = 2,
    /// <summary>An authored immutable value.</summary>
    Constant = 4,
    /// <summary>A session-owned collection of reference objects.</summary>
    RuntimeSet = 8,
}

/// <summary>Names, payload syntax and destination for a generated set of concrete DDD types.</summary>
/// <param name="Prefix">The identifier prefixed to each family name.</param>
/// <param name="PayloadType">An existing C# type, fully qualified when outside the generated namespace.</param>
/// <param name="Namespace">The namespace of the generated classes.</param>
/// <param name="Folder">A directory relative to the project's Assets folder.</param>
/// <param name="Families">The families to generate.</param>
public sealed record DataAssetTypeRequest(string Prefix, string PayloadType, string Namespace, string Folder,
    DataAssetTypeFamilies Families);

/// <summary>Writes concrete attributed C# DDD types and stable metadata into project sources.</summary>
public static class DataAssetTypeGenerator
{
    static readonly (DataAssetTypeFamilies Kind, string Suffix, string Base)[] Families =
    [
        (DataAssetTypeFamilies.Event, "Event", "DataAssetEvent"),
        (DataAssetTypeFamilies.Variable, "Variable", "DataAssetVariable"),
        (DataAssetTypeFamilies.Constant, "Constant", "DataAssetConst"),
        (DataAssetTypeFamilies.RuntimeSet, "RuntimeSet", "DataAssetRuntimeSet"),
    ];

    /// <summary>Generates selected classes without overwriting sources or metadata.</summary>
    public static IReadOnlyList<string> Generate(IAppSettings project, DataAssetTypeRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var directory = Destination(project, request.Folder);
        var selected = Families.Where(family => request.Families.HasFlag(family.Kind)).ToArray();
        var paths = selected.Select(family => Path.Combine(directory, request.Prefix + family.Suffix + ".cs")).ToArray();
        foreach (var path in paths)
        {
            if (File.Exists(path) || File.Exists(path + ".meta"))
            {
                throw new IOException($"{Path.GetFileName(path)} already exists; choose another prefix or folder.");
            }
        }
        Directory.CreateDirectory(directory);
        for (var i = 0; i < selected.Length; i++)
        {
            Write(project, request, selected[i].Suffix, selected[i].Base, paths[i]);
        }
        return paths;
    }

    static void Validate(DataAssetTypeRequest request)
    {
        if (!Identifier(request.Prefix)) { throw new ArgumentException("Enter a valid C# type prefix."); }
        if (string.IsNullOrWhiteSpace(request.Namespace) || !request.Namespace.Split('.').All(Identifier))
        {
            throw new ArgumentException("Enter a valid C# namespace.");
        }
        if (request.Families == DataAssetTypeFamilies.None || ((int)request.Families & ~15) != 0)
        {
            throw new ArgumentException("Select at least one DDD family.");
        }
        ValidatePayload(request);
    }

    static bool Identifier(string value) => SyntaxFacts.IsValidIdentifier(value)
        && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;

    static void ValidatePayload(DataAssetTypeRequest request)
    {
        var syntax = PayloadSyntax(request.PayloadType);
        if (request.Families.HasFlag(DataAssetTypeFamilies.RuntimeSet) && IsValuePayload(syntax, request.PayloadType))
        {
            throw new ArgumentException("Runtime sets require a reference payload type; deselect Runtime Set for value types.");
        }
    }

    static TypeSyntax PayloadSyntax(string payload)
    {
        var syntax = SyntaxFactory.ParseTypeName(payload);
        if (syntax.ContainsDiagnostics || UnsupportedPayload(syntax) || payload.Trim() == "void")
        {
            throw new ArgumentException("Enter a valid payload type, such as float or MyGame.Damage.");
        }
        return syntax;
    }

    static bool UnsupportedPayload(TypeSyntax syntax) => syntax.DescendantNodesAndSelf().Any(static node =>
        node is PointerTypeSyntax or FunctionPointerTypeSyntax or RefTypeSyntax or OmittedTypeArgumentSyntax);

    static bool IsValuePayload(TypeSyntax syntax, string payload) => ResolvePayload(payload)?.IsValueType == true
        || syntax is NullableTypeSyntax or TupleTypeSyntax || IsValueKeyword(syntax);

    static bool IsValueKeyword(TypeSyntax syntax) => syntax is PredefinedTypeSyntax predefined
        && predefined.Keyword.Kind() is not (SyntaxKind.StringKeyword or SyntaxKind.ObjectKeyword);

    static Type? ResolvePayload(string payload) => Type.GetType(payload) ?? AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(payload)).FirstOrDefault(static type => type is not null);

    static string Destination(IAppSettings project, string folder)
    {
        var root = Path.GetFullPath(project.AssetsAbsoluteDir);
        var directory = Path.GetFullPath(Path.Combine(root, folder));
        var relative = Path.GetRelativePath(root, directory);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Choose a folder inside Assets.");
        }
        return directory;
    }

    static void Write(IAppSettings project, DataAssetTypeRequest request, string suffix, string baseType, string path)
    {
        var id = Guid.NewGuid();
        var name = request.Prefix + suffix;
        var menu = SymbolDisplay.FormatLiteral($"DDD/Custom/{request.Prefix}/{suffix}", quote: true);
        var fileName = SymbolDisplay.FormatLiteral(name, quote: true);
        var source = $$"""
            namespace {{request.Namespace}};

            /// <summary>A designer-created {{suffix}} using the project's custom payload.</summary>
            [global::Turian.CreateAssetMenu(path: {{menu}}, fileName: {{fileName}})]
            [global::MASS4.Attributes.TypeId("{{id:D}}")]
            public sealed class {{name}} : global::Turian.Engine.Core.{{baseType}}<{{request.PayloadType}}>;
            """;
        using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write)))
        {
            writer.WriteLine(source);
        }
        Serializer.Save(path + ".meta", new Asset
        {
            Id = id, RelativePath = Path.GetRelativePath(project.ProjectAbsoluteDir, path).Replace('\\', '/'),
        });
    }
}
