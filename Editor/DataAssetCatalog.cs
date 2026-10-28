using Turian.Editor.Core;
using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

sealed class DataAssetCreation : IAssetCreationProvider
{
    public IReadOnlyList<AssetCreationKind> Kinds { get; } =
        AssetCreationCatalog.Scan([typeof(DataAssetEvent).Assembly],
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
}

sealed class DataAssetPresentation : IAssetPresentationProvider
{
    public IReadOnlyList<AssetTypePresentation> Presentations { get; } =
    [
        new(typeof(DataAssetEvent), "ddd.DataAssetEvent", "Event", "ddd.Event",
            new Color32(226, 75, 106), "DDD/Event") { Glyph = "ⓔ", ImageData = DataAssetIcons.Load("cherry") },
        new(typeof(DataAssetVariableFloat), "ddd.DataAssetVariableFloat", "Float Variable", "ddd.Variable",
            new Color32(43, 185, 139), "DDD/Variable") { Glyph = "ⓥ", ImageData = DataAssetIcons.Load("lush") },
        new(typeof(DataAssetVariableInt), "ddd.DataAssetVariableInt", "Int Variable", "ddd.Variable",
            new Color32(43, 185, 139), "DDD/Variable") { Glyph = "ⓥ", ImageData = DataAssetIcons.Load("lush") },
        new(typeof(DataAssetVariableBool), "ddd.DataAssetVariableBool", "Bool Variable", "ddd.Variable",
            new Color32(43, 185, 139), "DDD/Variable") { Glyph = "ⓥ", ImageData = DataAssetIcons.Load("lush") },
        new(typeof(DataAssetVariableVector2), "ddd.DataAssetVariableVector2", "Vector2 Variable", "ddd.Variable",
            new Color32(43, 185, 139), "DDD/Variable") { Glyph = "ⓥ", ImageData = DataAssetIcons.Load("lush") },
        new(typeof(DataAssetVariableVector3), "ddd.DataAssetVariableVector3", "Vector3 Variable", "ddd.Variable",
            new Color32(43, 185, 139), "DDD/Variable") { Glyph = "ⓥ", ImageData = DataAssetIcons.Load("lush") },
        new(typeof(DataAssetVariableAssetEnum), "ddd.DataAssetVariableAssetEnum", "Asset Enum Variable", "ddd.Variable",
            new Color32(43, 185, 139), "DDD/Variable") { Glyph = "ⓥ", ImageData = DataAssetIcons.Load("lush") },
        new(typeof(DataAssetEventFloat), "ddd.DataAssetEventFloat", "Float Event", "ddd.Event",
            new Color32(226, 75, 106), "DDD/Event") { Glyph = "ⓔ", ImageData = DataAssetIcons.Load("cherry") },
        new(typeof(DataAssetConstFloat), "ddd.DataAssetConstFloat", "Float Constant", "ddd.Constant",
            new Color32(76, 162, 226), "DDD/Constant") { Glyph = "ⓒ", ImageData = DataAssetIcons.Load("teal") },
        new(typeof(DataAssetEventInt), "ddd.DataAssetEventInt", "Int Event", "ddd.Event",
            new Color32(226, 75, 106), "DDD/Event") { Glyph = "ⓔ", ImageData = DataAssetIcons.Load("cherry") },
        new(typeof(DataAssetConstInt), "ddd.DataAssetConstInt", "Int Constant", "ddd.Constant",
            new Color32(76, 162, 226), "DDD/Constant") { Glyph = "ⓒ", ImageData = DataAssetIcons.Load("teal") },
        new(typeof(DataAssetEventBool), "ddd.DataAssetEventBool", "Bool Event", "ddd.Event",
            new Color32(226, 75, 106), "DDD/Event") { Glyph = "ⓔ", ImageData = DataAssetIcons.Load("cherry") },
        new(typeof(DataAssetConstBool), "ddd.DataAssetConstBool", "Bool Constant", "ddd.Constant",
            new Color32(76, 162, 226), "DDD/Constant") { Glyph = "ⓒ", ImageData = DataAssetIcons.Load("teal") },
        new(typeof(DataAssetEventVector2), "ddd.DataAssetEventVector2", "Vector2 Event", "ddd.Event",
            new Color32(226, 75, 106), "DDD/Event") { Glyph = "ⓔ", ImageData = DataAssetIcons.Load("cherry") },
        new(typeof(DataAssetConstVector2), "ddd.DataAssetConstVector2", "Vector2 Constant", "ddd.Constant",
            new Color32(76, 162, 226), "DDD/Constant") { Glyph = "ⓒ", ImageData = DataAssetIcons.Load("teal") },
        new(typeof(DataAssetEventVector3), "ddd.DataAssetEventVector3", "Vector3 Event", "ddd.Event",
            new Color32(226, 75, 106), "DDD/Event") { Glyph = "ⓔ", ImageData = DataAssetIcons.Load("cherry") },
        new(typeof(DataAssetConstVector3), "ddd.DataAssetConstVector3", "Vector3 Constant", "ddd.Constant",
            new Color32(76, 162, 226), "DDD/Constant") { Glyph = "ⓒ", ImageData = DataAssetIcons.Load("teal") },
        new(typeof(DataAssetEventAssetEnum), "ddd.DataAssetEventAssetEnum", "Asset Enum Event", "ddd.Event",
            new Color32(226, 75, 106), "DDD/Event") { Glyph = "ⓔ", ImageData = DataAssetIcons.Load("cherry") },
        new(typeof(DataAssetConstAssetEnum), "ddd.DataAssetConstAssetEnum", "Asset Enum Constant", "ddd.Constant",
            new Color32(76, 162, 226), "DDD/Constant") { Glyph = "ⓒ", ImageData = DataAssetIcons.Load("teal") },
        new(typeof(DataAssetRuntimeSetNode), "ddd.DataAssetRuntimeSetNode", "Node Runtime Set", "ddd.RuntimeSet",
            new Color32(226, 183, 65), "DDD/Runtime Set") { Glyph = "ⓢ", ImageData = DataAssetIcons.Load("kingsyellow") },
        new(typeof(DataAssetRuntimeSetAssetEnum), "ddd.DataAssetRuntimeSetAssetEnum", "Asset Enum Runtime Set", "ddd.RuntimeSet",
            new Color32(226, 183, 65), "DDD/Runtime Set") { Glyph = "ⓢ", ImageData = DataAssetIcons.Load("kingsyellow") },
        new(typeof(DataAssetRuntimeSetDataAsset), "ddd.DataAssetRuntimeSetDataAsset", "DataAsset Runtime Set", "ddd.RuntimeSet",
            new Color32(226, 183, 65), "DDD/Runtime Set") { Glyph = "ⓢ", ImageData = DataAssetIcons.Load("kingsyellow") },
        new(typeof(AssetEnumValue), "ddd.AssetEnumValue", "Asset Enum", "ddd.Enum",
            new Color32(61, 191, 201), "DDD/Enum") { Glyph = "ⓐ", ImageData = DataAssetIcons.Load("delicate") },
        new(typeof(InputReader), "ddd.InputReader", "Input Reader", "ddd.Reader",
            new Color32(160, 107, 224), "DDD/Reader") { Glyph = "ⓘ", ImageData = DataAssetIcons.Load("sign-blue") },
        new(typeof(DataAssetStateTransition), "ddd.DataAssetStateTransition", "State Transition", "ddd.StateMachine",
            new Color32(223, 145, 64), "DDD/State Machine") { Glyph = "ⓣ", ImageData = DataAssetIcons.Load("orange") },
        new(typeof(DataAssetRaiseResponse), "ddd.DataAssetRaiseResponse", "Raise Events Response", "ddd.Response",
            new Color32(193, 116, 184), "DDD/Response") { Glyph = "ⓡ", ImageData = DataAssetIcons.Load("purple") },
        new(typeof(DataAssetIntResponse), "ddd.DataAssetIntResponse", "Int Response", "ddd.Response",
            new Color32(193, 116, 184), "DDD/Response") { Glyph = "ⓡ", ImageData = DataAssetIcons.Load("purple") },
        new(typeof(DataAssetEvent<>), "ddd.family.Event", "DDD Event", "ddd.Event",
            new Color32(226, 75, 106), "DDD/Event") { Glyph = "ⓔ", ImageData = DataAssetIcons.Load("cherry") },
        new(typeof(DataAssetVariable<>), "ddd.family.Variable", "DDD Variable", "ddd.Variable",
            new Color32(43, 185, 139), "DDD/Variable") { Glyph = "ⓥ", ImageData = DataAssetIcons.Load("lush") },
        new(typeof(DataAssetConst<>), "ddd.family.Constant", "DDD Constant", "ddd.Constant",
            new Color32(76, 162, 226), "DDD/Constant") { Glyph = "ⓒ", ImageData = DataAssetIcons.Load("teal") },
        new(typeof(DataAssetRuntimeSet<>), "ddd.family.RuntimeSet", "DDD Runtime Set", "ddd.RuntimeSet",
            new Color32(226, 183, 65), "DDD/Runtime Set") { Glyph = "ⓢ", ImageData = DataAssetIcons.Load("kingsyellow") },
    ];
}
