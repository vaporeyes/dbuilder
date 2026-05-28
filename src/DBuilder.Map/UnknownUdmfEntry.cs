// ABOUTME: Represents top-level UDMF fields or blocks that DBuilder does not interpret.
// ABOUTME: Keeps unknown map data independent from parser-specific IO types for clone and undo use.

namespace DBuilder.Map;

public sealed class UnknownUdmfEntry
{
    public string Key { get; }
    public object Value { get; }

    public bool IsCollection => Value is List<UnknownUdmfEntry>;
    public IReadOnlyList<UnknownUdmfEntry> Children => (IReadOnlyList<UnknownUdmfEntry>)Value;

    public UnknownUdmfEntry(string key, object value)
    {
        Key = key;
        Value = value;
    }

    public UnknownUdmfEntry Clone()
    {
        if (Value is List<UnknownUdmfEntry> children)
            return new UnknownUdmfEntry(Key, children.Select(child => child.Clone()).ToList());

        return new UnknownUdmfEntry(Key, Value);
    }
}
