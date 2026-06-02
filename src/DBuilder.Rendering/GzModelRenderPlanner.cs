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

public sealed record GzPreparedModelRenderBatch(
    IReadOnlyList<WorldVertex> Vertices,
    IReadOnlyList<int> Indices,
    string? TexturePath,
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

    public static GzPreparedModelRenderBatch PrepareVertices(GzModelRenderBatch batch)
    {
        var vertices = new WorldVertex[batch.Mesh.Vertices.Count];
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = TransformVertex(batch.Mesh.Vertices[i], batch.World, batch.TintArgb);

        return new GzPreparedModelRenderBatch(
            vertices,
            batch.Mesh.Indices,
            batch.TexturePath,
            batch.TriangleCount);
    }

    private static string? TextureAt(IReadOnlyList<string?> textures, int index)
        => index >= 0 && index < textures.Count ? textures[index] : null;

    private static WorldVertex TransformVertex(WorldVertex source, Matrix4x4 world, int tintArgb)
    {
        Vector3 position = Vector3.Transform(new Vector3(source.x, source.y, source.z), world);
        Vector3 normal = Vector3.TransformNormal(new Vector3(source.nx, source.ny, source.nz), world);
        if (normal.LengthSquared() > 0.000001f)
            normal = Vector3.Normalize(normal);

        return new WorldVertex
        {
            x = position.X,
            y = position.Y,
            z = position.Z,
            c = tintArgb,
            u = source.u,
            v = source.v,
            nx = normal.X,
            ny = normal.Y,
            nz = normal.Z,
        };
    }
}
