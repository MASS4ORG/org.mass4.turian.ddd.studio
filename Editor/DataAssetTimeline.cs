using Turian.Engine.Core;

namespace Turian.Editor.DataAssetDriven;

/// <summary>A bounded allocation-free Studio event timeline, independent of gameplay assets.</summary>
public sealed class DataAssetTimeline : IDataAssetEventObserver
{
    readonly DataAssetEventTrace[] entries = new DataAssetEventTrace[1024];
    int next;

    /// <summary>The number of retained raises, capped at the fixed timeline capacity.</summary>
    public int Count { get; private set; }

    /// <inheritdoc />
    public void OnRaised(in DataAssetEventTrace trace)
    {
        entries[next] = trace;
        next = (next + 1) % entries.Length;
        Count = Math.Min(Count + 1, entries.Length);
    }

    /// <summary>Reads a retained raise from oldest to newest.</summary>
    public DataAssetEventTrace this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return entries[(next - Count + index + entries.Length) % entries.Length];
        }
    }

    /// <summary>Clears the timeline without retaining event payloads or listener objects.</summary>
    public void Clear()
    {
        Array.Clear(entries);
        next = 0;
        Count = 0;
    }
}
