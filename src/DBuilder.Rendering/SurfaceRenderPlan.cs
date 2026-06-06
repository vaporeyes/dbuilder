// ABOUTME: Plans UDB-style 2D surface render batches from buffered surface entries.
// ABOUTME: Keeps floor, ceiling, brightness, viewport, and hidden filtering behavior testable.

namespace DBuilder.Rendering;

public enum SurfaceRenderPass
{
    Floor,
    Ceiling,
    Brightness,
}

public sealed record SurfaceRenderBatch(long Texture, IReadOnlyList<SurfaceEntry> Entries);

public static class SurfaceRenderPlan
{
    public const long BrightnessTexture = 0;

    public static IReadOnlyList<SurfaceRenderBatch> Build(
        IEnumerable<SurfaceBufferSetState> sets,
        SurfaceRenderPass pass,
        SurfaceBounds viewport,
        bool skipHidden)
    {
        var batches = new Dictionary<long, List<SurfaceEntry>>();
        foreach (SurfaceBufferSetState set in sets)
        {
            foreach (SurfaceEntry entry in set.Entries)
            {
                if (!IsVisible(entry, viewport, skipHidden)) continue;

                long texture = pass switch
                {
                    SurfaceRenderPass.Floor => entry.FloorTexture,
                    SurfaceRenderPass.Ceiling => entry.CeilingTexture,
                    SurfaceRenderPass.Brightness => BrightnessTexture,
                    _ => throw new ArgumentOutOfRangeException(nameof(pass), pass, null),
                };

                if (!batches.TryGetValue(texture, out List<SurfaceEntry>? entries))
                {
                    entries = new List<SurfaceEntry>();
                    batches.Add(texture, entries);
                }

                entries.Add(entry);
            }
        }

        return batches
            .Select(batch => new SurfaceRenderBatch(batch.Key, batch.Value))
            .ToArray();
    }

    public static bool IsVisible(SurfaceEntry entry, SurfaceBounds viewport, bool skipHidden)
        => (!skipHidden || !entry.Hidden) && entry.Bounds.Intersects(viewport);
}
