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
