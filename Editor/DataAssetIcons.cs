namespace Turian.Editor.DataAssetDriven;

static class DataAssetIcons
{
    internal static byte[] Load(string color)
    {
        using var stream = typeof(DataAssetIcons).Assembly.GetManifestResourceStream(
            $"Turian.DDD.Icons.atom-icon-{color}.png")!;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }
}
