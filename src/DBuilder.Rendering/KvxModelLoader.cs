// ABOUTME: Loads Build engine KVX voxel data into UDB-style world vertices and palette atlas pixels.
// ABOUTME: Preserves UDB's slab decoding, voxel face generation, pivot transform, UVs, radius, and errors.

using System.Text;

namespace DBuilder.Rendering;

public sealed record KvxModelLoadResult(
    IReadOnlyList<GzModelMesh> Meshes,
    IReadOnlyList<PixelColor> Palette,
    IReadOnlyList<PixelColor> TexturePixels,
    int TextureWidth,
    int TextureHeight,
    int Radius,
    string? Errors,
    GzModelBounds Bounds);

public static class KvxModelLoader
{
    private const int PaletteBytes = 256 * 3;
    private const int TextureSize = 64;

    public static KvxModelLoadResult Load(byte[] bytes, IReadOnlyList<PixelColor>? overridePalette = null)
    {
        using var stream = new MemoryStream(bytes);
        return Load(stream, overridePalette);
    }

    public static KvxModelLoadResult Load(Stream stream, IReadOnlyList<PixelColor>? overridePalette = null)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII);
            _ = reader.ReadInt32();
            int xsize = reader.ReadInt32();
            int ysize = reader.ReadInt32();
            int zsize = reader.ReadInt32();
            if (xsize <= 0 || ysize <= 0 || zsize <= 0)
                throw new InvalidDataException("KVX model dimensions must be positive");

            var pivot = new KvxVector(
                reader.ReadInt32() / 256f,
                reader.ReadInt32() / 256f,
                reader.ReadInt32() / 256f);

            var xoffsets = new int[xsize + 1];
            for (int i = 0; i < xoffsets.Length; i++)
                xoffsets[i] = reader.ReadInt32();

            var xyoffsets = new short[xsize, ysize + 1];
            for (int x = 0; x < xsize; x++)
                for (int y = 0; y < ysize + 1; y++)
                    xyoffsets[x, y] = reader.ReadInt16();

            long slabsEnd = stream.Length - PaletteBytes;
            if (slabsEnd < stream.Position)
                throw new InvalidDataException("KVX model is missing palette data");

            PixelColor[] palette = LoadPalette(reader, slabsEnd, overridePalette);
            var builder = new KvxModelBuilder();
            var vertexHashes = new Dictionary<long, int>();
            var offsets = new List<int>(xsize * ysize);
            for (int x = 0; x < xsize; x++)
                for (int y = 0; y < ysize; y++)
                    offsets.Add(xoffsets[x] + xyoffsets[x, y] + 28);

            int counter = 0;
            for (int x = 0; x < xsize; x++)
                for (int y = 0; y < ysize; y++)
                {
                    long offset = offsets[counter];
                    long next = counter < offsets.Count - 1 ? offsets[counter + 1] : slabsEnd;
                    if (offset < 0 || offset > slabsEnd || next < offset || next > slabsEnd)
                        throw new InvalidDataException("KVX slab offset is outside the model data");

                    stream.Position = offset;
                    while (stream.Position < next)
                    {
                        int ztop = reader.ReadByte();
                        int zleng = reader.ReadByte();
                        if (ztop + zleng > zsize) break;
                        int flags = reader.ReadByte();

                        if (zleng > 0)
                            LoadSlab(reader, builder, vertexHashes, x, y, ztop, zleng, flags, pivot);
                    }

                    counter++;
                }

            int minX = (int)(xsize / 2f - pivot.X);
            int maxX = (int)(xsize / 2f + pivot.X);
            int minY = (int)(ysize / 2f - pivot.Y);
            int maxY = (int)(ysize / 2f + pivot.Y);
            int radius = Math.Max(Math.Max(Math.Abs(minY), Math.Abs(maxY)), Math.Max(Math.Abs(minX), Math.Abs(maxX)));

            builder.AddMesh();
            return builder.Build(palette, CreateVoxelTexture(palette), TextureSize, TextureSize, radius);
        }
        catch (Exception ex)
        {
            return KvxModelBuilder.Empty(ex.Message);
        }
    }

    private static PixelColor[] LoadPalette(BinaryReader reader, long slabsEnd, IReadOnlyList<PixelColor>? overridePalette)
    {
        if (overridePalette != null)
        {
            if (overridePalette.Count != 256)
                throw new InvalidDataException("KVX override palette must contain 256 colors");

            return overridePalette.ToArray();
        }

        reader.BaseStream.Position = slabsEnd;
        var palette = new PixelColor[256];
        for (int i = 0; i < palette.Length; i++)
        {
            byte r = (byte)(reader.ReadByte() * 4);
            byte g = (byte)(reader.ReadByte() * 4);
            byte b = (byte)(reader.ReadByte() * 4);
            palette[i] = new PixelColor(255, r, g, b);
        }

        return palette;
    }

    private static void LoadSlab(
        BinaryReader reader,
        KvxModelBuilder builder,
        Dictionary<long, int> vertexHashes,
        int x,
        int y,
        int ztop,
        int zleng,
        int flags,
        KvxVector pivot)
    {
        var colorIndices = new int[zleng];
        for (int i = 0; i < colorIndices.Length; i++)
            colorIndices[i] = reader.ReadByte();

        if ((flags & 16) != 0)
        {
            AddFace(builder, vertexHashes, new KvxVector(x, y, ztop), new KvxVector(x + 1, y, ztop), new KvxVector(x, y + 1, ztop), new KvxVector(x + 1, y + 1, ztop), pivot, colorIndices[0]);
        }

        int z = ztop;
        int colorStart = 0;
        while (z < ztop + zleng)
        {
            int colorLength = 0;
            while (z + colorLength < ztop + zleng && colorIndices[colorStart + colorLength] == colorIndices[colorStart])
                colorLength++;

            if ((flags & 1) != 0)
                AddFace(builder, vertexHashes, new KvxVector(x, y, z), new KvxVector(x, y + 1, z), new KvxVector(x, y, z + colorLength), new KvxVector(x, y + 1, z + colorLength), pivot, colorIndices[colorStart]);
            if ((flags & 2) != 0)
                AddFace(builder, vertexHashes, new KvxVector(x + 1, y + 1, z), new KvxVector(x + 1, y, z), new KvxVector(x + 1, y + 1, z + colorLength), new KvxVector(x + 1, y, z + colorLength), pivot, colorIndices[colorStart]);
            if ((flags & 4) != 0)
                AddFace(builder, vertexHashes, new KvxVector(x + 1, y, z), new KvxVector(x, y, z), new KvxVector(x + 1, y, z + colorLength), new KvxVector(x, y, z + colorLength), pivot, colorIndices[colorStart]);
            if ((flags & 8) != 0)
                AddFace(builder, vertexHashes, new KvxVector(x, y + 1, z), new KvxVector(x + 1, y + 1, z), new KvxVector(x, y + 1, z + colorLength), new KvxVector(x + 1, y + 1, z + colorLength), pivot, colorIndices[colorStart]);

            if (colorLength == 0) colorLength++;
            z += colorLength;
            colorStart += colorLength;
        }

        if ((flags & 32) != 0)
        {
            z = ztop + zleng - 1;
            AddFace(builder, vertexHashes, new KvxVector(x + 1, y, z + 1), new KvxVector(x, y, z + 1), new KvxVector(x + 1, y + 1, z + 1), new KvxVector(x, y + 1, z + 1), pivot, colorIndices[zleng - 1]);
        }
    }

    private static void AddFace(
        KvxModelBuilder builder,
        Dictionary<long, int> hashes,
        KvxVector v1,
        KvxVector v2,
        KvxVector v3,
        KvxVector v4,
        KvxVector pivot,
        int colorIndex)
    {
        float u0 = colorIndex % 16 / 16f;
        float u1 = u0 + 0.001f;
        float v0 = colorIndex / 16 / 16f;
        float v1Coord = v0 + 0.001f;

        int i1 = AddVertex(builder, hashes, ToWorldVertex(v1, pivot, u0, v0));
        _ = AddVertex(builder, hashes, ToWorldVertex(v2, pivot, u1, v1Coord));
        int i4 = AddVertex(builder, hashes, ToWorldVertex(v4, pivot, u0, v0));
        _ = AddVertex(builder, hashes, ToWorldVertex(v3, pivot, u1, v1Coord));

        builder.Indices.Add(i1);
        builder.Indices.Add(i4);
    }

    private static WorldVertex ToWorldVertex(KvxVector vertex, KvxVector pivot, float u, float v)
        => new()
        {
            x = vertex.X - pivot.X,
            y = -vertex.Y + pivot.Y,
            z = -vertex.Z + pivot.Z,
            c = -1,
            u = u,
            v = v,
        };

    private static int AddVertex(KvxModelBuilder builder, Dictionary<long, int> hashes, WorldVertex vertex)
    {
        long hash;
        unchecked
        {
            hash = 2166136261;
            hash = (hash * 16777619) ^ vertex.x.GetHashCode();
            hash = (hash * 16777619) ^ vertex.y.GetHashCode();
            hash = (hash * 16777619) ^ vertex.z.GetHashCode();
            hash = (hash * 16777619) ^ vertex.u.GetHashCode();
            hash = (hash * 16777619) ^ vertex.v.GetHashCode();
        }

        if (hashes.TryGetValue(hash, out int existingIndex))
        {
            builder.Indices.Add(existingIndex);
            return existingIndex;
        }

        builder.Vertices.Add(vertex);
        builder.UpdateBounds(vertex);
        int index = builder.Vertices.Count - 1;
        hashes.Add(hash, index);
        builder.Indices.Add(index);
        return index;
    }

    private static PixelColor[] CreateVoxelTexture(IReadOnlyList<PixelColor> palette)
    {
        var texture = new PixelColor[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            int paletteY = y / 4;
            for (int x = 0; x < TextureSize; x++)
            {
                int paletteX = x / 4;
                texture[y * TextureSize + x] = palette[paletteY * 16 + paletteX];
            }
        }

        return texture;
    }

    private sealed record KvxVector(float X, float Y, float Z);

    private sealed class KvxModelBuilder
    {
        private readonly List<GzModelMesh> meshes = new();
        private bool hasBounds;
        private float minX;
        private float minY;
        private float minZ;
        private float maxX;
        private float maxY;
        private float maxZ;

        public List<WorldVertex> Vertices { get; } = new();

        public List<int> Indices { get; } = new();

        public void AddMesh()
        {
            if (Vertices.Count == 0 && Indices.Count == 0) return;
            meshes.Add(new GzModelMesh(Vertices.ToArray(), Indices.ToArray()));
        }

        public void UpdateBounds(WorldVertex vertex)
        {
            if (!hasBounds)
            {
                minX = maxX = vertex.x;
                minY = maxY = vertex.y;
                minZ = maxZ = vertex.z;
                hasBounds = true;
                return;
            }

            minX = Math.Min(minX, vertex.x);
            minY = Math.Min(minY, vertex.y);
            minZ = Math.Min(minZ, vertex.z);
            maxX = Math.Max(maxX, vertex.x);
            maxY = Math.Max(maxY, vertex.y);
            maxZ = Math.Max(maxZ, vertex.z);
        }

        public KvxModelLoadResult Build(
            IReadOnlyList<PixelColor> palette,
            IReadOnlyList<PixelColor> texturePixels,
            int textureWidth,
            int textureHeight,
            int radius)
            => new(meshes.ToArray(), palette.ToArray(), texturePixels.ToArray(), textureWidth, textureHeight, radius, null, hasBounds ? new GzModelBounds(minX, minY, minZ, maxX, maxY, maxZ) : GzModelBounds.Empty);

        public static KvxModelLoadResult Empty(string errors)
            => new(Array.Empty<GzModelMesh>(), Array.Empty<PixelColor>(), Array.Empty<PixelColor>(), 0, 0, 0, errors, GzModelBounds.Empty);
    }
}
