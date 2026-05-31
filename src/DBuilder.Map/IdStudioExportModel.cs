// ABOUTME: Data-level idStudio export settings and map writer ported from UDB BuilderModes.
// ABOUTME: Builds refmap hierarchies and entity text without UI or filesystem dependencies.

using System.Globalization;
using System.Text;

namespace DBuilder.Map;

public sealed record IdStudioExportOptions
{
    public string ModPath { get; init; } = string.Empty;
    public string MapName { get; init; } = string.Empty;
    public float Downscale { get; init; } = 20;
    public float XShift { get; init; }
    public float YShift { get; init; }
    public float ZShift { get; init; }
    public bool ExportTextures { get; init; }
    public bool ExportAllTextures { get; init; }
}

public sealed record IdStudioExportSettings(
    string ModPath,
    string MapName,
    float Downscale,
    float XShift,
    float YShift,
    float ZShift,
    bool ExportTextures,
    bool ExportAllTextures)
{
    public static IdStudioExportSettings FromOptions(IdStudioExportOptions options)
        => new(
            options.ModPath,
            options.MapName,
            options.Downscale,
            options.XShift,
            options.YShift,
            options.ZShift,
            options.ExportTextures,
            options.ExportAllTextures);
}

public readonly record struct IdStudioExportFile(string Path, string Content);

public sealed record IdStudioTextureImage(string Name, byte[] TgaBytes, bool IsTranslucent = false, bool IsMasked = false);
public readonly record struct IdStudioTextureExportFile(string Path, byte[] Content, string MaterialName, bool IsFlat);
public sealed record IdStudioTextureExportPlan(
    IReadOnlyList<IdStudioTextureExportFile> ArtFiles,
    IReadOnlyList<IdStudioExportFile> MaterialFiles,
    IReadOnlyList<string> MissingImages);

public readonly record struct IdStudioVertex(float X, float Y);

public struct IdStudioVector
{
    public float X;
    public float Y;
    public float Z;

    public IdStudioVector(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public IdStudioVector(IdStudioVertex start, IdStudioVertex end)
    {
        X = end.X - start.X;
        Y = end.Y - start.Y;
        Z = 0.0f;
    }

    public readonly float Magnitude() => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

    public void Normalize()
    {
        float magnitude = Magnitude();
        if (magnitude == 0) return;
        X /= magnitude;
        Y /= magnitude;
        Z /= magnitude;
    }
}

public struct IdStudioPlane
{
    public IdStudioVector Normal;
    public float Distance;

    public void SetFrom(IdStudioVector normal, IdStudioVertex point)
    {
        Normal = normal;
        Normal.Normalize();
        Distance = Normal.X * point.X + Normal.Y * point.Y;
    }
}

public static class IdStudioExportValidation
{
    public static bool IsValidMapName(string mapName)
    {
        if (mapName.Length == 0) return false;
        if (mapName[0] < 'a' || mapName[0] > 'z') return false;
        foreach (char c in mapName)
        {
            if (c is >= 'a' and <= 'z') continue;
            if (c is >= '0' and <= '9') continue;
            if (c == '_') continue;
            return false;
        }

        return true;
    }
}

public static class IdStudioTextureExporter
{
    private const string FlatsArtDirectory = "base/art/wadtobrush/flats/";
    private const string FlatsMaterialDirectory = "base/declTree/material2/art/wadtobrush/flats/";
    private const string WallsArtDirectory = "base/art/wadtobrush/walls/";
    private const string WallsMaterialDirectory = "base/declTree/material2/art/wadtobrush/walls/";

    public static IReadOnlyList<string> RequiredDirectories(string modPath)
        =>
        [
            Path.Combine(modPath, FlatsArtDirectory),
            Path.Combine(modPath, FlatsMaterialDirectory),
            Path.Combine(modPath, WallsArtDirectory),
            Path.Combine(modPath, WallsMaterialDirectory)
        ];

    public static IdStudioTextureExportPlan CreatePlan(
        IdStudioExportSettings settings,
        IEnumerable<string> mapTextureNames,
        IEnumerable<string> mapFlatNames,
        IEnumerable<IdStudioTextureImage> allTextures,
        IEnumerable<IdStudioTextureImage> allFlats,
        Func<string, IdStudioTextureImage?> getTexture,
        Func<string, IdStudioTextureImage?> getFlat)
    {
        var artFiles = new List<IdStudioTextureExportFile>();
        var materialFiles = new List<IdStudioExportFile>();
        var missing = new List<string>();
        if (!settings.ExportTextures) return new IdStudioTextureExportPlan(artFiles, materialFiles, missing);

        if (settings.ExportAllTextures)
        {
            foreach (IdStudioTextureImage image in allTextures)
            {
                AddImage(settings.ModPath, "walls/", image, isFlat: false, artFiles, materialFiles);
            }

            foreach (IdStudioTextureImage image in allFlats)
            {
                AddImage(settings.ModPath, "flats/", image, isFlat: true, artFiles, materialFiles);
            }
        }
        else
        {
            foreach (string name in mapTextureNames)
            {
                IdStudioTextureImage? image = getTexture(name);
                if (image == null)
                {
                    missing.Add($"idStudio Exporter: texture \"{name}\" does not exist!");
                    continue;
                }

                AddImage(settings.ModPath, "walls/", image, isFlat: false, artFiles, materialFiles);
            }

            foreach (string name in mapFlatNames)
            {
                IdStudioTextureImage? image = getFlat(name);
                if (image == null)
                {
                    missing.Add($"idStudio Exporter: flat \"{name}\" does not exist!");
                    continue;
                }

                AddImage(settings.ModPath, "flats/", image, isFlat: true, artFiles, materialFiles);
            }
        }

        return new IdStudioTextureExportPlan(artFiles, materialFiles, missing);
    }

    public static void WriteTextureFiles(IdStudioTextureExportPlan plan)
    {
        foreach (IdStudioTextureExportFile file in plan.ArtFiles)
        {
            string? directory = Path.GetDirectoryName(file.Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(file.Path, file.Content);
        }

        foreach (IdStudioExportFile file in plan.MaterialFiles)
        {
            string? directory = Path.GetDirectoryName(file.Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(file.Path, file.Content);
        }
    }

    public static string BuildMaterialDeclaration(string subFolder, string imageName, bool useAlpha)
    {
        string lowerName = imageName.ToLowerInvariant();
        return useAlpha
            ? string.Format(CultureInfo.InvariantCulture, MaterialTemplateAlpha, subFolder, lowerName)
            : string.Format(CultureInfo.InvariantCulture, MaterialTemplate, subFolder, lowerName);
    }

    private static void AddImage(
        string modPath,
        string subFolder,
        IdStudioTextureImage image,
        bool isFlat,
        List<IdStudioTextureExportFile> artFiles,
        List<IdStudioExportFile> materialFiles)
    {
        string lowerName = image.Name.ToLowerInvariant();
        string artPath = Path.Combine(modPath, "base/art/wadtobrush/", subFolder, lowerName + ".tga");
        string materialPath = Path.Combine(modPath, "base/declTree/material2/art/wadtobrush/", subFolder, lowerName + ".decl");
        artFiles.Add(new IdStudioTextureExportFile(artPath, image.TgaBytes, lowerName, isFlat));
        materialFiles.Add(new IdStudioExportFile(
            materialPath,
            BuildMaterialDeclaration(subFolder, lowerName, image.IsTranslucent || image.IsMasked)));
    }

    private const string MaterialTemplate =
        """
declType( material2 ) {{
	inherit = "template/pbr";
	edit = {{
		RenderLayers = {{
			item[0] = {{
				parms = {{
					smoothness = {{
						filePath = "textures/system/constant_color/black.tga";
					}}
					specular = {{
						filePath = "textures/system/constant_color/black.tga";
					}}
					albedo = {{
						filePath = "art/wadtobrush/{0}{1}.tga";
					}}
				}}
			}}
		}}
	}}
}}
""";

    private const string MaterialTemplateAlpha =
        """
declType( material2 ) {{
	inherit = "template/pbr_alphatest";
	edit = {{
		RenderLayers = {{
			item[0] = {{
				parms = {{
					cover = {{
						filePath = "art/wadtobrush/{0}{1}.tga";
					}}
					smoothness = {{
						filePath = "textures/system/constant_color/black.tga";
					}}
					specular = {{
						filePath = "textures/system/constant_color/black.tga";
					}}
					albedo = {{
						filePath = "art/wadtobrush/{0}{1}.tga";
					}}
				}}
			}}
		}}
	}}
}}
""";
}

public sealed class IdStudioEntityBuilder
{
    private readonly StringBuilder builder = new();

    public void BeginBrushDef(string group, int sectorNumber)
    {
        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            """
{{
	groups {{
		"nav"
		"{0}/{1}"
	}}
	brushDef3 {{

""",
            group,
            sectorNumber);
    }

    public void BeginBrushDef()
    {
        builder.Append(
            """
{
	brushDef3 {

""");
    }

    public void WriteClipPlane(IdStudioPlane plane)
    {
        AppendPlane(plane);
        builder.Append(" ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/clip/clip\" 0 0 0\n");
    }

    public void WriteCasterPlane(IdStudioPlane plane)
    {
        AppendPlane(plane);
        builder.Append(" ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0\n");
    }

    public void WriteFloorPlane(
        IdStudioPlane plane,
        IdStudioExportSettings settings,
        bool isCeiling,
        string texture,
        int textureWidth,
        int textureHeight)
    {
        float xRatio = 1.0f / textureWidth;
        float yRatio = 1.0f / textureHeight;
        float xScale = xRatio * settings.Downscale;
        float yScale = yRatio * settings.Downscale;
        float xShift = -xRatio * settings.XShift;
        float yShift = yRatio * settings.YShift;

        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            "\t\t( {0} {1} {2} {3} ) ( ( 0 {4} {5} ) ( {6} 0 {7} ) ) \"art/wadtobrush/flats/{8}\" 0 0 0\n",
            plane.Normal.X,
            plane.Normal.Y,
            plane.Normal.Z,
            -plane.Distance,
            isCeiling ? -xScale : xScale,
            xShift,
            -yScale,
            yShift,
            texture.ToLowerInvariant());
    }

    public void WriteWallPlane(
        IdStudioPlane plane,
        IdStudioExportSettings settings,
        IdStudioVector horizontal,
        IdStudioVertex start,
        float drawHeight,
        string texture,
        float offsetX,
        int textureWidth,
        int textureHeight)
    {
        float xScale = 1.0f / textureWidth * settings.Downscale;
        float yScale = 1.0f / textureHeight * settings.Downscale;
        float projection = ((horizontal.X * start.X + horizontal.Y * start.Y) / horizontal.Magnitude() - offsetX) * xScale * -1;

        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            "\t\t( {0} {1} {2} {3} ) ( ( {4} 0 {5} ) ( 0 {6} {7} ) ) \"art/wadtobrush/walls/{8}\" 0 0 0\n",
            plane.Normal.X,
            plane.Normal.Y,
            plane.Normal.Z,
            -plane.Distance,
            xScale,
            projection,
            yScale,
            drawHeight * yScale,
            texture.ToLowerInvariant());
    }

    public void EndBrushDef() => builder.Append("\t}\n}\n");

    public string Render() => LowercaseScientificNotation(builder.ToString());

    public static string LowercaseScientificNotation(string text)
    {
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length - 1; i++)
        {
            if (chars[i] != 'E') continue;
            if (chars[i + 1] == '+' || chars[i + 1] == '-') chars[i] = 'e';
        }

        return new string(chars);
    }

    private void AppendPlane(IdStudioPlane plane)
    {
        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            "\t\t( {0} {1} {2} {3}",
            plane.Normal.X,
            plane.Normal.Y,
            plane.Normal.Z,
            -plane.Distance);
    }
}

public static class IdStudioBrushFormatter
{
    public static string BuildFloorBrush(
        IdStudioExportSettings settings,
        IdStudioVertex a,
        IdStudioVertex b,
        IdStudioVertex c,
        float height,
        bool isCeiling,
        string texture,
        int sectorNumber,
        int textureWidth,
        int textureHeight)
    {
        IdStudioPlane[] bounds = new IdStudioPlane[4];

        IdStudioVector edge = new(a, b);
        bounds[0].SetFrom(new IdStudioVector(edge.Y, -edge.X, 0.0f), a);

        edge = new IdStudioVector(b, c);
        bounds[1].SetFrom(new IdStudioVector(edge.Y, -edge.X, 0.0f), b);

        edge = new IdStudioVector(c, a);
        bounds[2].SetFrom(new IdStudioVector(edge.Y, -edge.X, 0.0f), c);

        IdStudioPlane surface = new();
        if (isCeiling)
        {
            bounds[3].Normal = new IdStudioVector(0, 0, 1);
            bounds[3].Distance = height + 0.0075f;
            surface.Normal = new IdStudioVector(0, 0, -1);
            surface.Distance = -height;
        }
        else
        {
            surface.Normal = new IdStudioVector(0, 0, 1);
            surface.Distance = height;
            bounds[3].Normal = new IdStudioVector(0, 0, -1);
            bounds[3].Distance = 0.0075f - height;
        }

        var builder = new IdStudioEntityBuilder();
        builder.BeginBrushDef();
        foreach (IdStudioPlane bound in bounds)
        {
            builder.WriteCasterPlane(bound);
        }

        builder.WriteFloorPlane(surface, settings, isCeiling, texture, textureWidth, textureHeight);
        builder.EndBrushDef();
        return builder.Render();
    }

    public static string BuildWallBrush(
        IdStudioExportSettings settings,
        IdStudioVertex start,
        IdStudioVertex end,
        float minHeight,
        float maxHeight,
        float drawHeight,
        string texture,
        float offsetX,
        int textureWidth,
        int textureHeight)
    {
        if (maxHeight - minHeight < 0.0075f)
        {
            minHeight -= 100.0f / settings.Downscale;
            maxHeight += 100.0f / settings.Downscale;
        }

        IdStudioPlane[] bounds = new IdStudioPlane[5];
        IdStudioVector horizontal = new(start, end);
        var surface = new IdStudioPlane();
        surface.SetFrom(new IdStudioVector(horizontal.Y, -horizontal.X, 0), end);

        bounds[0].Normal.X = -surface.Normal.X;
        bounds[0].Normal.Y = -surface.Normal.Y;
        bounds[0].Normal.Z = 0;

        IdStudioVertex insetEnd = new(
            bounds[0].Normal.X * 0.0075f + end.X,
            bounds[0].Normal.Y * 0.0075f + end.Y);
        bounds[0].Distance = bounds[0].Normal.X * insetEnd.X + bounds[0].Normal.Y * insetEnd.Y;

        IdStudioVector deltaVector = new(end, insetEnd);
        bounds[1].SetFrom(new IdStudioVector(deltaVector.Y, -deltaVector.X, 0), insetEnd);

        bounds[2].Normal.X = -bounds[1].Normal.X;
        bounds[2].Normal.Y = -bounds[1].Normal.Y;
        bounds[2].Normal.Z = 0;
        bounds[2].Distance = bounds[2].Normal.X * start.X + bounds[2].Normal.Y * start.Y;

        bounds[3].Normal = new IdStudioVector(0, 0, 1);
        bounds[3].Distance = maxHeight;

        bounds[4].Normal = new IdStudioVector(0, 0, -1);
        bounds[4].Distance = minHeight * -1;

        var builder = new IdStudioEntityBuilder();
        builder.BeginBrushDef();
        foreach (IdStudioPlane bound in bounds)
        {
            builder.WriteCasterPlane(bound);
        }

        builder.WriteWallPlane(surface, settings, horizontal, start, drawHeight, texture, offsetX, textureWidth, textureHeight);
        builder.EndBrushDef();
        return builder.Render();
    }

    public static string BuildStepBrush(
        IdStudioVertex start,
        IdStudioVertex end,
        float minHeight,
        float maxHeight,
        int sectorNumber)
    {
        float xyShift = (maxHeight - minHeight) * 2;
        IdStudioPlane[] bounds = new IdStudioPlane[5];
        IdStudioVector horizontal = new(start, end);

        IdStudioVector cross = new(horizontal.Y, -horizontal.X, 0);
        cross.Normalize();

        IdStudioVertex baseStart = new(cross.X * xyShift + start.X, cross.Y * xyShift + start.Y);

        bounds[0].Normal.X = -cross.X;
        bounds[0].Normal.Y = -cross.Y;
        bounds[0].Normal.Z = 0;
        bounds[0].Distance = bounds[0].Normal.X * start.X + bounds[0].Normal.Y * start.Y;

        IdStudioVector leftHorizontal = new(start, baseStart);
        bounds[1].SetFrom(new IdStudioVector(leftHorizontal.Y, -leftHorizontal.X, 0), start);

        bounds[2].Normal.X = -bounds[1].Normal.X;
        bounds[2].Normal.Y = -bounds[1].Normal.Y;
        bounds[2].Normal.Z = 0;
        bounds[2].Distance = bounds[2].Normal.X * end.X + bounds[2].Normal.Y * end.Y;

        bounds[3].Normal = new IdStudioVector(0, 0, -1);
        bounds[3].Distance = -minHeight;

        IdStudioVector slopeBase = new(leftHorizontal.X, leftHorizontal.Y, minHeight - maxHeight);
        IdStudioVector slopeHorizontal = new(horizontal.X, horizontal.Y, 0);
        IdStudioVector normal = new(
            -slopeHorizontal.Y * slopeBase.Z,
            slopeHorizontal.X * slopeBase.Z,
            slopeBase.X * slopeHorizontal.Y - slopeHorizontal.X * slopeBase.Y);
        normal.Normalize();
        bounds[4].Normal = normal;
        bounds[4].Distance = normal.X * start.X + normal.Y * start.Y + normal.Z * maxHeight;

        var builder = new IdStudioEntityBuilder();
        builder.BeginBrushDef("stepclip", sectorNumber);
        foreach (IdStudioPlane bound in bounds)
        {
            builder.WriteClipPlane(bound);
        }

        builder.EndBrushDef();
        return builder.Render();
    }
}

public sealed class IdStudioMapWriter
{
    private readonly IdStudioExportSettings settings;
    private readonly string fileName;
    private readonly string fileExtension;
    private readonly string prefix;
    private readonly StringBuilder world = new();
    private readonly StringBuilder entities = new();
    private readonly List<IdStudioMapWriter> childMaps = new();
    private int staticModels;

    public IdStudioMapWriter(IdStudioExportSettings settings)
    {
        this.settings = settings;
        fileName = settings.MapName;
        fileExtension = ".map";
        prefix = string.Empty;
        AppendRootMap(string.Empty);
    }

    private IdStudioMapWriter(IdStudioMapWriter parent, string refmapPrefix)
    {
        settings = parent.settings;
        fileName = parent.fileName + "_" + refmapPrefix;
        fileExtension = ".refmap";
        prefix = refmapPrefix + "_";
        AppendRootMap(refmapPrefix);
    }

    public IdStudioMapWriter AddRefmap(string refmapPrefix)
    {
        var child = new IdStudioMapWriter(this, refmapPrefix);
        childMaps.Add(child);
        AppendFuncReference(childMaps.Count, child.fileName);
        return child;
    }

    public string BeginFuncStatic(string group, int subGroup)
    {
        string entityName = prefix + settings.MapName + "_func_static_" + (++staticModels).ToString(CultureInfo.InvariantCulture);
        entities.AppendFormat(
            CultureInfo.InvariantCulture,
            """
entity {{
	groups {{
		"nav"
		"{2}/{3}"
	}}
	entityDef {0} {{
		inherit = "func/static";
		edit = {{
			renderModelInfo = {{
				model = "maps/{1}/{0}";
			}}
			clipModelInfo = {{
				clipModelName = "maps/{1}/{0}";
			}}
		}}
	}}

""",
            entityName,
            settings.MapName,
            group,
            subGroup);
        return entityName;
    }

    public void EndFuncStatic() => entities.Append("}\n");

    public IReadOnlyList<IdStudioExportFile> CreateFilePlan()
    {
        var files = new List<IdStudioExportFile>();
        AddFiles(files);
        return files;
    }

    private void AddFiles(List<IdStudioExportFile> files)
    {
        string fullPath = Path.Combine(settings.ModPath, "base/maps/", fileName + fileExtension);
        files.Add(new IdStudioExportFile(fullPath, RenderContent()));
        foreach (IdStudioMapWriter child in childMaps)
        {
            child.AddFiles(files);
        }
    }

    private string RenderContent()
    {
        var content = new StringBuilder();
        content.Append(world);
        content.Append("}\n");
        content.Append(entities);
        return content.ToString();
    }

    private void AppendRootMap(string entityPrefix)
    {
        world.AppendFormat(
            CultureInfo.InvariantCulture,
            """
Version 7
HierarchyVersion 1
entity {{
	entityDef world {{
		inherit = "worldspawn";
		edit = {{
			entityPrefix = "{0}";
		}}
	}}

""",
            entityPrefix);
    }

    private void AppendFuncReference(int index, string childFileName)
    {
        entities.AppendFormat(
            CultureInfo.InvariantCulture,
            """
entity {{
	entityDef {0}func_reference_{1} {{
		inherit = "func/reference";
		edit = {{
			mapname = "maps/{2}.refmap";
		}}
	}}
}}
// reference 0
	{{
	reference {{
		"maps/{2}.refmap"
	}}
}}
}}

""",
            prefix,
            index,
            childFileName);
    }
}
