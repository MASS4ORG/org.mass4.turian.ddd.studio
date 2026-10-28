using System.Reflection;
using System.Text.Json;
using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

/// <summary>A wire needing designer attention, including retained GUIDs after a code refactor.</summary>
public sealed record DataAssetWireDiagnostic(Guid Owner, string Member, Guid Target, string Kind, string Message);

/// <summary>Checks serialized GUID wires against current compiled member types without modifying source assets.</summary>
public static class DataAssetWireValidator
{
    /// <summary>Reports dangling targets, payload mismatches and removed wire members for a scene or DataAsset payload.</summary>
    public static IReadOnlyList<DataAssetWireDiagnostic> Validate(string json, Func<Guid, DataAsset?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        using var document = JsonDocument.Parse(json);
        var diagnostics = new List<DataAssetWireDiagnostic>();
        Visit(document.RootElement, resolve, diagnostics);
        return diagnostics;
    }

    static void Visit(JsonElement element, Func<Guid, DataAsset?> resolve,
        List<DataAssetWireDiagnostic> diagnostics, Type? expected = null, Guid owner = default,
        string member = "", bool typedScope = false)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Visit(item, resolve, diagnostics, ItemType(expected), owner, member, typedScope);
            }
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) { return; }
        VisitObject(element, resolve, diagnostics, expected, owner, member, typedScope);
    }

    static void VisitObject(JsonElement element, Func<Guid, DataAsset?> resolve,
        List<DataAssetWireDiagnostic> diagnostics, Type? expected, Guid owner, string member, bool typedScope)
    {
        var type = TypeRegistry.TryGetType(Identity(element, "__TypeId"), out var declared) ? declared : expected;
        var identity = Identity(element, "Id");
        if (identity != Guid.Empty) { owner = identity; }
        typedScope |= type is not null;
        if (element.TryGetProperty("$ref", out _))
        {
            if (typedScope) { Check(owner, member, element, expected, resolve, diagnostics); }
            return;
        }
        foreach (var property in element.EnumerateObject())
        {
            var path = member.Length == 0 ? property.Name : member + "." + property.Name;
            Visit(property.Value, resolve, diagnostics, type is null ? null : MemberType(type, property.Name),
                owner, path, typedScope);
        }
    }

    static Guid Identity(JsonElement element, string name) => element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.String && property.TryGetGuid(out var id) ? id : Guid.Empty;

    static Type? MemberType(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.PropertyType
        ?? type.GetField(name, BindingFlags.Instance | BindingFlags.Public)?.FieldType;

    static Type? ItemType(Type? type)
    {
        if (type?.IsArray == true) { return type.GetElementType(); }
        return type?.IsGenericType == true ? type.GetGenericArguments()[0] : null;
    }

    static void Check(Guid owner, string member, JsonElement value, Type? expected,
        Func<Guid, DataAsset?> resolve, List<DataAssetWireDiagnostic> diagnostics)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("$ref", out _)) { return; }
        var target = Identity(value, "$ref");
        if (target == Guid.Empty)
        {
            diagnostics.Add(new(owner, member, target, "InvalidGuid", "Wire requires a nonempty asset GUID."));
            return;
        }
        CheckTarget(owner, member, target, expected, resolve, diagnostics);
    }

    static void CheckTarget(Guid owner, string member, Guid target, Type? expected,
        Func<Guid, DataAsset?> resolve, List<DataAssetWireDiagnostic> diagnostics)
    {
        if (expected is null)
        {
            diagnostics.Add(new(owner, member, target, "Rewire", "Serialized wire member no longer exists; reassign it in Studio."));
            return;
        }
        if (!typeof(DataAsset).IsAssignableFrom(expected)) { return; }
        var resolved = resolve(target);
        if (resolved is null)
        {
            diagnostics.Add(new(owner, member, target, "Dangling", "Referenced DataAsset is missing."));
        }
        else if (!expected.IsInstanceOfType(resolved))
        {
            diagnostics.Add(new(owner, member, target, "TypeMismatch",
                $"Expected {expected.Name}; target is {resolved.GetType().Name}."));
        }
    }
}
