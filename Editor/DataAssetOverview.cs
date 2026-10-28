using System.Reflection;
using System.Text.Json;
using Turian.Editor.Core;
using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

/// <summary>One authored DDD asset, with no retained runtime instance or collectible type.</summary>
public sealed record DataAssetOverviewEntry(Guid Id, Guid TypeId, string Name, string Path, string TypeName, bool Orphan);

/// <summary>A serialized reference and its authored publisher/listener role.</summary>
public sealed record DataAssetOverviewWire(Guid Owner, Guid Target, string File, string Member, string Role);

/// <summary>A project snapshot for management, references and dangling-wire review.</summary>
public sealed record DataAssetOverviewSnapshot(IReadOnlyList<DataAssetOverviewEntry> Assets,
    IReadOnlyList<DataAssetOverviewWire> Wires, IReadOnlyList<string> Diagnostics);

/// <summary>Indexes project assets and authored wires without entering Play Mode or inventing code-defined types.</summary>
public static class DataAssetOverview
{
    /// <summary>Scans authored assets and scenes; external metadata can confirm wires to installed brick assets.</summary>
    public static DataAssetOverviewSnapshot Scan(string directory, Func<Guid, bool>? externalExists = null)
    {
        var assets = new List<DataAssetOverviewEntry>();
        var documents = new List<(string File, JsonElement Json)>();
        var diagnostics = new List<string>();
        if (!Directory.Exists(directory)) { return new(assets, [], diagnostics); }
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .Where(static path => Path.GetExtension(path) is ".dataasset" or ".prefab" or ".scene")
                     .Order(StringComparer.Ordinal))
        {
            Read(file, directory, documents, assets, diagnostics);
        }
        var known = assets.Select(static asset => asset.Id).ToHashSet();
        var wires = new List<DataAssetOverviewWire>();
        foreach (var (file, json) in documents) { Visit(json, null, Guid.Empty, file, "", known, wires); }
        AddDangling(wires, known, externalExists, diagnostics);
        var referenced = wires.Select(static wire => wire.Target).ToHashSet();
        return new(assets.Select(asset => asset with { Orphan = !referenced.Contains(asset.Id) }).ToArray(), wires, diagnostics);
    }

    static void AddDangling(IReadOnlyList<DataAssetOverviewWire> wires, IReadOnlySet<Guid> known,
        Func<Guid, bool>? externalExists, List<string> diagnostics)
    {
        foreach (var wire in wires.Where(wire => IsDangling(wire, known, externalExists)))
        {
            diagnostics.Add($"{wire.File}: {wire.Member}: dangling asset {wire.Target}");
        }
    }

    static bool IsDangling(DataAssetOverviewWire wire, IReadOnlySet<Guid> known, Func<Guid, bool>? externalExists) =>
        !known.Contains(wire.Target) && externalExists?.Invoke(wire.Target) != true;

    /// <summary>Searches names and paths, optionally restricting to one payload identity and unreferenced assets.</summary>
    public static IReadOnlyList<DataAssetOverviewEntry> Filter(DataAssetOverviewSnapshot snapshot,
        string search, Guid type = default, bool orphanOnly = false) =>
        snapshot.Assets.Where(asset => (type == Guid.Empty || type == asset.TypeId) && (!orphanOnly || asset.Orphan)
            && (asset.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || asset.Path.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

    /// <summary>Creates a bounded batch inside the project's Assets folder using the same menu catalog and unique names.</summary>
    public static int CreateMany(AssetCreationCatalog catalog, AssetCreationKind kind, string assetsRoot,
        string relativeDirectory, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 100);
        var root = Path.GetFullPath(assetsRoot);
        var target = Path.GetFullPath(Path.Combine(root, relativeDirectory));
        var relative = Path.GetRelativePath(root, target);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Choose a folder inside Assets.", nameof(relativeDirectory));
        }
        var created = 0;
        for (var index = 0; index < count; index++)
        {
            if (!catalog.Create(kind, target)) { break; }
            created++;
        }
        return created;
    }

    static void Read(string file, string root, List<(string, JsonElement)> documents,
        List<DataAssetOverviewEntry> assets, List<string> diagnostics)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var json = document.RootElement;
            var relative = Path.GetRelativePath(root, file);
            documents.Add((relative, json.Clone()));
            var typeId = Identity(json, "__TypeId");
            if (!TypeRegistry.TryGetType(typeId, out var type) || type is null || !IsDdd(type)) { return; }
            var id = Identity(json, "Id");
            if (id == Guid.Empty) { diagnostics.Add($"{relative}: asset requires a GUID."); return; }
            if (assets.Any(asset => asset.Id == id)) { diagnostics.Add($"{relative}: duplicate asset GUID {id}"); }
            assets.Add(new(id, typeId, Path.GetFileNameWithoutExtension(file), relative, type.Name, false));
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            diagnostics.Add($"{Path.GetRelativePath(root, file)}: {error.Message}");
        }
    }

    static void Visit(JsonElement json, Type? expected, Guid owner, string file, string member,
        IReadOnlySet<Guid> known, List<DataAssetOverviewWire> wires, string role = "References")
    {
        if (json.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in json.EnumerateArray()) { Visit(item, ItemType(expected), owner, file, member, known, wires, role); }
            return;
        }
        if (json.ValueKind != JsonValueKind.Object) { return; }
        VisitObject(json, expected, owner, file, member, known, wires, role);
    }

    static void VisitObject(JsonElement json, Type? expected, Guid owner, string file, string member,
        IReadOnlySet<Guid> known, List<DataAssetOverviewWire> wires, string role)
    {
        var typeId = Identity(json, "__TypeId");
        var type = TypeRegistry.TryGetType(typeId, out var declared) ? declared : expected;
        var identity = Identity(json, "Id");
        if (identity != Guid.Empty) { owner = identity; }
        if (json.TryGetProperty("$ref", out _))
        {
            var target = Identity(json, "$ref");
            if (known.Contains(target) || IsDdd(expected))
            {
                wires.Add(new(owner, target, file, member, role));
            }
            return;
        }
        foreach (var property in json.EnumerateObject())
        {
            var path = member.Length == 0 ? property.Name : member + "." + property.Name;
            var nextType = MemberType(type, property.Name);
            Visit(property.Value, nextType, owner, file, path, known, wires, Role(type, property.Name));
        }
    }

    static Type? MemberType(Type? type, string member)
    {
        if (type is null) { return null; }
        return type.GetProperty(member, BindingFlags.Instance | BindingFlags.Public)?.PropertyType
            ?? type.GetField(member, BindingFlags.Instance | BindingFlags.Public)?.FieldType;
    }

    static string Role(Type? owner, string member)
    {
        if (IsGeneratedListener(owner, member)) { return "Listens"; }
        if (member.StartsWith("On", StringComparison.Ordinal) || member == "Events") { return "Raises"; }
        return member is "Event" or "Trigger" ? "Listens" : "References";
    }

    static bool IsGeneratedListener(Type? owner, string member)
    {
        for (var type = owner; type is not null; type = type.BaseType)
        {
            if (type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Any(method => method.GetCustomAttribute<Turian.DataAssetListenAttribute>()?.EventMember == member))
            {
                return true;
            }
        }
        return false;
    }

    static Type? ItemType(Type? type) => type?.IsArray == true ? type.GetElementType()
        : type?.IsGenericType == true ? type.GetGenericArguments()[0] : null;

    static Guid Identity(JsonElement json, string name) => json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.TryGetGuid(out var id) ? id : Guid.Empty;

    static bool IsDdd(Type? type)
    {
        if (type is null) { return false; }
        if (IsSpecialType(type)) { return true; }
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (!current.IsGenericType) { continue; }
            var family = current.GetGenericTypeDefinition();
            if (IsGenericFamily(family)) { return true; }
        }
        return false;
    }

    static bool IsSpecialType(Type type) => typeof(InputReader).IsAssignableFrom(type)
        || typeof(AssetEnumValue).IsAssignableFrom(type) || typeof(DataAssetResponse).IsAssignableFrom(type)
        || typeof(DataAssetStateTransition).IsAssignableFrom(type);

    static bool IsGenericFamily(Type type) => type == typeof(DataAssetEvent<>) || type == typeof(DataAssetVariable<>)
        || type == typeof(DataAssetConst<>) || type == typeof(DataAssetRuntimeSet<>);

}
