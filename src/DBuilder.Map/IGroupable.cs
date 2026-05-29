// ABOUTME: Interface for map elements that carry UDB-style transient selection group membership.
// ABOUTME: Groups are stored as a bitmask so editor code can select or clear multiple groups efficiently.

namespace DBuilder.Map;

public interface IGroupable : ISelectable
{
    int Groups { get; set; }
}

public static class GroupableExtensions
{
    public static void AddToGroup(this IGroupable element, int groupsMask)
        => element.Groups |= groupsMask;

    public static void RemoveFromGroup(this IGroupable element, int groupsMask)
        => element.Groups &= ~groupsMask;

    public static void SelectByGroup(this IGroupable element, int groupsMask)
        => element.Selected = element.IsInGroup(groupsMask);

    public static bool IsInGroup(this IGroupable element, int groupsMask)
        => (element.Groups & groupsMask) != 0;
}
