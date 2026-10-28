using Guinevere;
using Turian.Editor.Core;
using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

sealed partial class DataAssetPanel
{
    readonly AssetCreationCatalog? creation;
    DataAssetOverviewSnapshot snapshot = new([], [], []);
    string? indexedRoot;
    string search = "";
    bool orphanOnly;
    int typeIndex;
    Guid[] typeIds = [];
    string[] typeNames = [];
    int createIndex;
    string[] createNames = [];
    IReadOnlyList<AssetCreationKind> createKinds = [];
    string folder = "";
    string count = "1";
    string creationMessage = "";

    bool Overview(Gui gui)
    {
        if (settings.Settings is not { } project) { return false; }
        var root = project.AssetsAbsoluteDir;
        if (indexedRoot != root) { RefreshOverview(root); }
        if (Button(gui, "Refresh DDD assets", "ddd/refresh")) { RefreshOverview(root); }
        search = gui.TextInput(search, placeholder: "Search DDD assets", id: "ddd/search");
        gui.Dropdown(typeNames, ref typeIndex, width: 0, placeholder: "All DDD types");
        gui.Checkbox(ref orphanOnly, "No authored references");
        var type = typeIndex > 0 && typeIndex <= typeIds.Length ? typeIds[typeIndex - 1] : Guid.Empty;
        OverviewAssets(gui, type);
        BulkCreation(gui, root);
        return true;
    }

    void OverviewAssets(Gui gui, Guid type)
    {
        foreach (var asset in DataAssetOverview.Filter(snapshot, search, type, orphanOnly))
        {
            var orphan = asset.Orphan ? "  (unreferenced)" : "";
            if (Button(gui, $"{asset.Name} — {asset.TypeName}{orphan}", "ddd/overview/" + asset.Id)) { selected = asset.Id; }
        }
        foreach (var diagnostic in snapshot.Diagnostics) { Line(gui, diagnostic); }
        foreach (var wire in snapshot.Wires.Where(wire => wire.Target == selected))
        {
            Line(gui, $"Authored {wire.Role}: {wire.File} / {wire.Member} (owner {wire.Owner})");
        }
    }

    void RefreshOverview(string root)
    {
        snapshot = DataAssetOverview.Scan(root, id => database.TryGetAsset(id, out _));
        indexedRoot = root;
        var types = snapshot.Assets.DistinctBy(static asset => asset.TypeId).OrderBy(static asset => asset.TypeName).ToArray();
        typeIds = types.Select(static asset => asset.TypeId).ToArray();
        typeNames = ["All DDD types", .. types.Select(static asset => asset.TypeName)];
        typeIndex = 0;
        RefreshCreationChoices();
    }

    void RefreshCreationChoices()
    {
        createKinds = creation is null ? []
            : creation.Kinds.Where(static kind => kind.MenuPath.StartsWith("DDD/", StringComparison.Ordinal)).ToArray();
        createNames = createKinds.Select(static kind => kind.MenuPath[4..]).ToArray();
        createIndex = Math.Clamp(createIndex, 0, Math.Max(0, createNames.Length - 1));
    }

    void BulkCreation(Gui gui, string root)
    {
        if (creation is null || createKinds.Count == 0) { return; }
        Line(gui, "Bulk creation");
        gui.Dropdown(createNames, ref createIndex, width: 0, placeholder: "Asset type");
        folder = gui.TextInput(folder, placeholder: "Folder inside Assets", id: "ddd/createFolder");
        count = gui.TextInput(count, placeholder: "Count (1–100)", id: "ddd/createCount");
        if (Button(gui, "Create assets", "ddd/createMany"))
        {
            try
            {
                if (!int.TryParse(count, out var amount)) { throw new ArgumentException("Enter a count from 1 to 100."); }
                var created = DataAssetOverview.CreateMany(creation, createKinds[createIndex], root, folder, amount);
                creationMessage = $"Created {created} asset(s).";
                RefreshOverview(root);
            }
            catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
            {
                creationMessage = error.Message;
            }
        }
        if (creationMessage.Length > 0) { Line(gui, creationMessage); }
    }
}
