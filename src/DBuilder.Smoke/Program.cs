// ABOUTME: Headless end-to-end smoke test - loads every map in a WAD, triangulates all sectors, round-trips through
// ABOUTME: all three writers (Doom/Hexen/UDMF), and resolves textures, printing per-map pass/fail with a non-zero exit on failure.

using System;
using System.Collections.Generic;
using System.IO;
using DBuilder.IO;
using DBuilder.Map;

if (args.Length < 1)
{
    Console.WriteLine("usage: DBuilder.Smoke <file.wad> [mapfilter]");
    Console.WriteLine("  Loads every map, triangulates sectors, round-trips all 3 formats, resolves textures.");
    return 2;
}

string wadPath = args[0];
string? filter = args.Length >= 2 ? args[1].ToUpperInvariant() : null;
if (!File.Exists(wadPath))
{
    Console.WriteLine($"[smoke] file not found: {wadPath}");
    return 2;
}

using var wad = new WAD(wadPath, openreadonly: true);
using var resources = new ResourceManager();
resources.AddResource(wadPath);

var markers = FindMapMarkers(wad);
if (filter != null) markers = markers.FindAll(m => m.Equals(filter, StringComparison.OrdinalIgnoreCase));

Console.WriteLine($"[smoke] {Path.GetFileName(wadPath)}  -  {markers.Count} map(s){(resources.Palette != null ? "" : "  (no palette)")}");
Console.WriteLine();

int passed = 0, failed = 0;
var healthTotals = new Dictionary<MapIssueKind, int>();
foreach (var marker in markers)
{
    if (SmokeMap(wad, marker, resources, healthTotals)) passed++; else failed++;
}

Console.WriteLine();
Console.WriteLine($"[smoke] {passed}/{markers.Count} maps passed" + (failed > 0 ? $", {failed} FAILED" : ""));
if (healthTotals.Count > 0)
{
    Console.WriteLine("[smoke] health issues across all maps (reported, not failed):");
    foreach (var kind in (MapIssueKind[])Enum.GetValues(typeof(MapIssueKind)))
        if (healthTotals.TryGetValue(kind, out int n)) Console.WriteLine($"           {kind}: {n}");
}
return failed == 0 ? 0 : 1;

// ============================================================

static bool SmokeMap(WAD wad, string marker, ResourceManager resources, Dictionary<MapIssueKind, int> healthTotals)
{
    var problems = new List<string>();
    MapSet? map = null;
    string fmt = "?";
    try
    {
        (map, fmt) = LoadAnyFormat(wad, marker);
    }
    catch (Exception ex) { problems.Add($"load threw: {ex.GetType().Name}: {ex.Message}"); }

    if (map == null)
    {
        Report(marker, fmt, "FAIL", "could not load", problems);
        return false;
    }

    // Triangulate every sector.
    int triCount = 0, triFailed = 0, triApprox = 0;
    foreach (var s in map.Sectors)
    {
        if (s.Sidedefs.Count == 0) continue;
        try
        {
            var tri = Triangulation.Create(s);
            if (tri.Vertices.Count == 0) triFailed++;
            else { triCount += tri.Vertices.Count / 3; if (tri.IsApproximate) triApprox++; }
        }
        catch (Exception ex) { problems.Add($"triangulate sector {s.Index} threw: {ex.Message}"); triFailed++; }
    }

    // Round-trip through all three writers; counts must survive.
    string doom = RoundTrip("doom", problems, () => WriteReadDoom(map, marker));
    string hexen = RoundTrip("hexen", problems, () => WriteReadHexen(map, marker));
    string udmf = RoundTrip("udmf", problems, () => WriteReadUdmf(map, marker));

    // Resolve referenced textures/flats.
    var (flatsOk, flatsTotal, wallsOk, wallsTotal) = ResolveTextures(map, resources);

    // Health check (reported, not failed - real maps legitimately contain editor-flagged geometry).
    string health = "";
    try
    {
        var issues = MapAnalysis.Check(map);
        int errs = 0, warns = 0;
        foreach (var iss in issues)
        {
            if (iss.Severity == MapIssueSeverity.Error) errs++; else warns++;
            healthTotals[iss.Kind] = healthTotals.TryGetValue(iss.Kind, out int c) ? c + 1 : 1;
        }
        if (errs + warns > 0) health = $"  health {errs}E/{warns}W";
    }
    catch (Exception ex) { problems.Add($"analysis threw: {ex.GetType().Name}: {ex.Message}"); }

    bool ok = problems.Count == 0;
    string detail = $"{fmt} sectors={map.Sectors.Count} tris={triCount}" +
                    (triFailed > 0 ? $" ({triFailed} failed" + (triApprox > 0 ? $", {triApprox} approx)" : ")") : triApprox > 0 ? $" ({triApprox} approx)" : "") +
                    $"  doom={doom} hexen={hexen} udmf={udmf}" +
                    (flatsTotal > 0 ? $"  flats {flatsOk}/{flatsTotal}" : "") +
                    (wallsTotal > 0 ? $"  walls {wallsOk}/{wallsTotal}" : "") +
                    health;
    Report(marker, fmt, ok ? "ok" : "FAIL", detail, ok ? null : problems);
    return ok;
}

static void Report(string marker, string fmt, string status, string detail, List<string>? problems)
{
    Console.WriteLine($"  {marker,-8} [{status,-4}] {detail}");
    if (problems != null)
        foreach (var p in problems) Console.WriteLine($"           - {p}");
}

// Round-trips a writer/reader and returns "ok" or "MISMATCH"/"err"; records problems on failure.
static string RoundTrip(string name, List<string> problems, Func<(bool ok, string? note)> action)
{
    try
    {
        var (ok, note) = action();
        if (ok) return "ok";
        problems.Add($"{name} round-trip mismatch: {note}");
        return "MISMATCH";
    }
    catch (Exception ex)
    {
        problems.Add($"{name} writer threw: {ex.GetType().Name}: {ex.Message}");
        return "err";
    }
}

static (bool, string?) WriteReadDoom(MapSet map, string marker)
{
    var ms = new MemoryStream();
    using (var w = new WAD(ms)) DoomMapWriter.WriteMap(map, w, marker, 0);
    ms.Position = 0;
    using var rw = new WAD(ms, openreadonly: true);
    var r = DoomMapLoader.Load(rw, marker);
    return CompareCounts(map, r, thingsExpected: map.Things.Count);
}

static (bool, string?) WriteReadHexen(MapSet map, string marker)
{
    var ms = new MemoryStream();
    using (var w = new WAD(ms)) HexenMapWriter.WriteMap(map, w, marker, 0, behaviorBytes: new byte[] { 0x41, 0x43, 0x53, 0x00 });
    ms.Position = 0;
    using var rw = new WAD(ms, openreadonly: true);
    var r = HexenMapLoader.Load(rw, marker);
    return CompareCounts(map, r, thingsExpected: map.Things.Count);
}

static (bool, string?) WriteReadUdmf(MapSet map, string marker)
{
    var ms = new MemoryStream();
    using (var w = new WAD(ms)) UdmfMapWriter.WriteMap(map, w, marker, 0);
    ms.Position = 0;
    using var rw = new WAD(ms, openreadonly: true);
    var textmap = rw.FindLump("TEXTMAP");
    if (textmap == null) return (false, "no TEXTMAP written");
    var r = UdmfMapLoader.Load(System.Text.Encoding.ASCII.GetString(textmap.Stream.ReadAllBytes()), out var parser);
    if (parser.ErrorResult != 0) return (false, $"reparse error line {parser.ErrorLine}");
    return CompareCounts(map, r, thingsExpected: map.Things.Count);
}

static (bool, string?) CompareCounts(MapSet src, MapSet? r, int thingsExpected)
{
    if (r == null) return (false, "reload null");
    if (r.Vertices.Count != src.Vertices.Count) return (false, $"verts {r.Vertices.Count}!={src.Vertices.Count}");
    if (r.Linedefs.Count != src.Linedefs.Count) return (false, $"lines {r.Linedefs.Count}!={src.Linedefs.Count}");
    if (r.Sidedefs.Count != src.Sidedefs.Count) return (false, $"sides {r.Sidedefs.Count}!={src.Sidedefs.Count}");
    if (r.Sectors.Count != src.Sectors.Count) return (false, $"sectors {r.Sectors.Count}!={src.Sectors.Count}");
    if (r.Things.Count != thingsExpected) return (false, $"things {r.Things.Count}!={thingsExpected}");
    return (true, null);
}

static (int flatsOk, int flatsTotal, int wallsOk, int wallsTotal) ResolveTextures(MapSet map, ResourceManager resources)
{
    var flats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var s in map.Sectors)
    {
        if (s.FloorTexture is { } f && f != "-") flats.Add(f);
        if (s.CeilTexture is { } c && c != "-") flats.Add(c);
    }
    var walls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var l in map.Linedefs)
    {
        foreach (var sd in new[] { l.Front, l.Back })
        {
            if (sd == null) continue;
            foreach (var t in new[] { sd.HighTexture, sd.MidTexture, sd.LowTexture })
                if (!string.IsNullOrEmpty(t) && t != "-") walls.Add(t);
        }
    }

    int fok = 0;
    foreach (var f in flats) if (resources.GetFlat(f) != null) fok++;
    int wok = 0;
    foreach (var w in walls) if (resources.GetWallTexture(w) != null) wok++;
    return (fok, flats.Count, wok, walls.Count);
}

// Loads a map with the matching loader, returning the MapSet and a short format tag.
static (MapSet? map, string fmt) LoadAnyFormat(WAD wad, string marker)
{
    int idx = wad.FindLumpIndex(marker);
    bool udmf = false, hexen = false;
    for (int j = idx + 1; j < wad.Lumps.Count && j <= idx + 12; j++)
    {
        string sub = wad.Lumps[j].Name;
        if (sub == "TEXTMAP") { udmf = true; break; }
        if (sub == "BEHAVIOR") hexen = true;
    }
    if (udmf)
    {
        var textmap = wad.FindLump("TEXTMAP");
        return (textmap == null ? null : UdmfMapLoader.Load(System.Text.Encoding.ASCII.GetString(textmap.Stream.ReadAllBytes()), out _), "UDMF");
    }
    return hexen ? (HexenMapLoader.Load(wad, marker), "Hexen") : (DoomMapLoader.Load(wad, marker), "Doom");
}

// A map marker is a zero-length lump immediately followed by the first map sub-lump:
// THINGS (Doom/Hexen) or TEXTMAP (UDMF). Other sub-lumps (LINEDEFS, SIDEDEFS, ...) are not markers.
static List<string> FindMapMarkers(WAD wad)
{
    var result = new List<string>();
    for (int i = 0; i < wad.Lumps.Count - 1; i++)
    {
        if (wad.Lumps[i].Length != 0) continue; // markers are zero-length
        string next = wad.Lumps[i + 1].Name;
        if (next is "THINGS" or "TEXTMAP")
            result.Add(wad.Lumps[i].Name);
    }
    return result;
}
