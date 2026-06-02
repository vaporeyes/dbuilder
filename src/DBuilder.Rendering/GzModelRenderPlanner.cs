// ABOUTME: Builds render batches for loaded GZDoom model meshes.
// ABOUTME: Keeps mesh, texture, tint, and transform pairing testable before OpenGL draw wiring.

using System.Numerics;

namespace DBuilder.Rendering;

public sealed record GzModelRenderBatch(
    GzModelMesh Mesh,
    string? TexturePath,
    Matrix4x4 World,
    int TintArgb,
    int TriangleCount);

public static class GzModelRenderPlanner
{
    public static IReadOnlyList<GzModelRenderBatch> Plan(GzLoadedModel model, Matrix4x4 world, int tintArgb)
    {
        var batches = new List<GzModelRenderBatch>(model.Meshes.Count);
        for (int i = 0; i < model.Meshes.Count; i++)
        {
            GzModelMesh mesh = model.Meshes[i];
            if (mesh.Indices.Count == 0) continue;

            batches.Add(new GzModelRenderBatch(
                mesh,
                TextureAt(model.TexturePaths, i),
                world,
                tintArgb,
                mesh.Indices.Count / 3));
        }

        return batches;
    }

    private static string? TextureAt(IReadOnlyList<string?> textures, int index)
        => index >= 0 && index < textures.Count ? textures[index] : null;
}
