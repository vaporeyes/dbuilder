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

public sealed record SurfaceRenderCommand(
    long Texture,
    int BufferIndex,
    int VertexOffset,
    int PrimitiveCount,
    double Desaturation);

public sealed record SurfaceBufferBinding(int CommandIndex, int BufferIndex);

public enum SurfaceRenderOperationKind
{
    SetTexture,
    SetDesaturation,
    SetVertexBuffer,
    Draw,
    ResetDesaturation,
}

public sealed record SurfaceRenderOperation(
    SurfaceRenderOperationKind Kind,
    long? Texture = null,
    int? BufferIndex = null,
    int? VertexOffset = null,
    int? PrimitiveCount = null,
    double? Desaturation = null);

public enum SurfaceTextureFallback
{
    Source,
    White,
    Missing,
    Unknown,
}

public sealed record SurfaceTextureResolution(long Texture, SurfaceTextureFallback Fallback);

public sealed record SurfaceRenderStatePlan(
    string ShaderName,
    TextureAddress SamplerAddress,
    SamplerFilterPlan SamplerFilter,
    bool ResetDesaturationAfterRender);

public static class SurfaceRenderPlan
{
    public const long BrightnessTexture = 0;
    public const long WhiteTextureToken = 0;
    public const long MissingTextureToken = -1;
    public const long UnknownTextureToken = -2;
    public const string Display2DNormalShaderName = "display2d_normal";
    public const string Display2DFullBrightShaderName = "display2d_fullbright";

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

    public static SurfaceTextureResolution ResolveTexture(
        long longTextureName,
        long emptyTextureName,
        bool imageExists,
        bool isUnknownImage,
        bool isImageLoaded,
        bool loadFailed)
    {
        if (longTextureName == BrightnessTexture)
            return new SurfaceTextureResolution(WhiteTextureToken, SurfaceTextureFallback.White);
        if (longTextureName == emptyTextureName)
            return new SurfaceTextureResolution(MissingTextureToken, SurfaceTextureFallback.Missing);
        if (!imageExists || isUnknownImage)
            return new SurfaceTextureResolution(UnknownTextureToken, SurfaceTextureFallback.Unknown);
        if (!isImageLoaded || loadFailed)
            return new SurfaceTextureResolution(WhiteTextureToken, SurfaceTextureFallback.White);

        return new SurfaceTextureResolution(longTextureName, SurfaceTextureFallback.Source);
    }

    public static bool IsVisible(SurfaceEntry entry, SurfaceBounds viewport, bool skipHidden)
        => (!skipHidden || !entry.Hidden) && entry.Bounds.Intersects(viewport);

    public static IReadOnlyList<SurfaceRenderCommand> BuildCommands(
        IEnumerable<SurfaceRenderBatch> batches,
        SurfaceRenderPass pass)
    {
        ValidatePass(pass);

        int surfaceVertexOffsetMultiplier = pass == SurfaceRenderPass.Ceiling ? 1 : 0;
        var commands = new List<SurfaceRenderCommand>();
        foreach (SurfaceRenderBatch batch in batches)
        {
            foreach (SurfaceEntry entry in batch.Entries)
            {
                if (entry.NumVertices <= 0 || entry.BufferIndex < 0) continue;

                commands.Add(new SurfaceRenderCommand(
                    batch.Texture,
                    entry.BufferIndex,
                    entry.VertexOffset + entry.NumVertices * surfaceVertexOffsetMultiplier,
                    entry.NumVertices / 3,
                    entry.Desaturation));
            }
        }

        return commands;
    }

    public static IReadOnlyList<SurfaceBufferBinding> BufferBindings(IEnumerable<SurfaceRenderCommand> commands)
    {
        var bindings = new List<SurfaceBufferBinding>();
        int? lastBuffer = null;
        int index = 0;
        foreach (SurfaceRenderCommand command in commands)
        {
            if (command.BufferIndex != lastBuffer)
            {
                bindings.Add(new SurfaceBufferBinding(index, command.BufferIndex));
                lastBuffer = command.BufferIndex;
            }

            index++;
        }

        return bindings;
    }

    public static IReadOnlyList<SurfaceRenderOperation> BuildRenderOperations(
        IEnumerable<SurfaceRenderBatch> batches,
        SurfaceRenderPass pass)
    {
        IReadOnlyList<SurfaceRenderCommand> commands = BuildCommands(batches, pass);
        var operations = new List<SurfaceRenderOperation>();
        long? lastTexture = null;
        int? lastBuffer = null;

        foreach (SurfaceRenderCommand command in commands)
        {
            if (command.Texture != lastTexture)
            {
                operations.Add(new SurfaceRenderOperation(
                    SurfaceRenderOperationKind.SetTexture,
                    Texture: command.Texture));
                lastTexture = command.Texture;
                lastBuffer = null;
            }

            operations.Add(new SurfaceRenderOperation(
                SurfaceRenderOperationKind.SetDesaturation,
                Desaturation: command.Desaturation));

            if (command.BufferIndex != lastBuffer)
            {
                operations.Add(new SurfaceRenderOperation(
                    SurfaceRenderOperationKind.SetVertexBuffer,
                    BufferIndex: command.BufferIndex));
                lastBuffer = command.BufferIndex;
            }

            operations.Add(new SurfaceRenderOperation(
                SurfaceRenderOperationKind.Draw,
                VertexOffset: command.VertexOffset,
                PrimitiveCount: command.PrimitiveCount));
        }

        operations.Add(new SurfaceRenderOperation(
            SurfaceRenderOperationKind.ResetDesaturation,
            Desaturation: 0.0));
        return operations;
    }

    public static SurfaceRenderStatePlan BuildRenderStatePlan(
        SurfaceRenderPass pass,
        bool fullBrightness,
        bool visualBilinear,
        float filterAnisotropy)
    {
        ValidatePass(pass);

        TextureFilter filter = visualBilinear ? TextureFilter.Linear : TextureFilter.Nearest;
        MipmapFilter mipFilter = visualBilinear ? MipmapFilter.Linear : MipmapFilter.Nearest;
        string shaderName = fullBrightness && pass != SurfaceRenderPass.Brightness
            ? Display2DFullBrightShaderName
            : Display2DNormalShaderName;

        return new SurfaceRenderStatePlan(
            shaderName,
            TextureAddress.Wrap,
            RenderDevice.BuildSamplerFilterPlan(filter, filter, mipFilter, filterAnisotropy),
            ResetDesaturationAfterRender: true);
    }

    private static void ValidatePass(SurfaceRenderPass pass)
    {
        if (!Enum.IsDefined(pass)) throw new ArgumentOutOfRangeException(nameof(pass), pass, null);
    }
}
