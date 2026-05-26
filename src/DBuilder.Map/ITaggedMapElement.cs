// ABOUTME: Shared contracts for map elements that expose primary tags and UDMF multi-tags.
// ABOUTME: Mirrors UDB's tagged element shape without tying callers to concrete map element classes.

namespace DBuilder.Map;

public interface ITaggedMapElement
{
    int Tag { get; set; }
}

public interface IMultiTaggedMapElement : ITaggedMapElement
{
    List<int> Tags { get; }
}
