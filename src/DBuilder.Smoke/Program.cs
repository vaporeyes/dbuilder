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

var maps = WadMaps.Find(wad);
if (filter != null) maps = maps.FindAll(m => m.Name.Equals(filter, StringComparison.OrdinalIgnoreCase));

Console.WriteLine($"[smoke] {Path.GetFileName(wadPath)}  -  {maps.Count} map(s){(resources.Palette != null ? "" : "  (no palette)")}");
Console.WriteLine();

int passed = 0, failed = 0;
var healthTotals = new Dictionary<MapIssueKind, int>();
foreach (var entry in maps)
{
    if (SmokeMap(wad, entry, resources, healthTotals)) passed++; else failed++;
}

Console.WriteLine();
Console.WriteLine($"[smoke] {passed}/{maps.Count} maps passed" + (failed > 0 ? $", {failed} FAILED" : ""));
if (healthTotals.Count > 0)
{
    Console.WriteLine("[smoke] health issues across all maps (reported, not failed):");
    foreach (var kind in (MapIssueKind[])Enum.GetValues(typeof(MapIssueKind)))
        if (healthTotals.TryGetValue(kind, out int n)) Console.WriteLine($"           {kind}: {n}");
}
return failed == 0 ? 0 : 1;

// ============================================================

static bool SmokeMap(WAD wad, MapEntry entry, ResourceManager resources, Dictionary<MapIssueKind, int> healthTotals)
{
    var problems = new List<string>();
    string marker = entry.Name;
    string fmt = entry.Format.ToString();
    MapSet? map = null;
    try
    {
        map = WadMaps.Load(wad, entry);
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

