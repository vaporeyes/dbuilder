// ABOUTME: Avalonia OpenGlControlBase that renders a MapSet's 2D line overlay via the DBuilder.Rendering stack.
// ABOUTME: Bridges Avalonia's GL context to Silk.NET so RenderDevice/Shader/VertexBuffer work unchanged; supports pan/zoom and click-pick.

using System;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;
using DBuilder.Rendering;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using GlVertexBuffer = DBuilder.Rendering.VertexBuffer;
using DBShader = DBuilder.Rendering.Shader;
using DBTexture = DBuilder.Rendering.Texture;
using DBPrimitiveType = DBuilder.Rendering.PrimitiveType;
using Vec2D = DBuilder.Geometry.Vector2D;

namespace DBuilder.Editor;

public class MapControl : OpenGlControlBase, ICustomHitTest
{
    // OpenGlControlBase has no hit-testable visual of its own, so pointer events (pan/zoom/click) never
    // reach it by default. Claim only points actually inside the control's bounds - returning true for
    // ALL points made the map swallow clicks meant for the surrounding menu/toolbar chrome.
    public bool HitTest(Point point) => point.X >= 0 && point.Y >= 0 && point.X < Bounds.Width && point.Y < Bounds.Height;

    private const string VertexSrc = @"#version 330 core
layout(location=0) in vec4 a_pos;
layout(location=1) in vec4 a_color;
layout(location=2) in vec2 a_uv;
uniform mat4 projection;
out vec4 v_color;
out vec2 v_uv;
void main() { gl_Position = projection * vec4(a_pos.xyz, 1.0); v_color = a_color; v_uv = a_uv; }";

    private const string FragmentSrc = @"#version 330 core
in vec4 v_color;
in vec2 v_uv;
uniform sampler2D tex0;
uniform float useTexture;
out vec4 frag;
void main() { vec4 s = texture(tex0, v_uv); frag = mix(v_color, s * v_color, useTexture); }";

    private GL? _gl;
    private RenderDevice? _device;
    private DBShader? _shader;
    private DBTexture? _placeholderTex;
    // Sector fill buckets: VB + triangle count + flat name to bind ("" = untextured/gray). The name (not a
    // cached texture) is stored so the animated frame can be resolved each draw.
    private readonly System.Collections.Generic.List<(GlVertexBuffer Vb, int Tris, string Name)> _fillBuckets = new();
    // Flat-name -> uploaded GL texture (null cached when unresolvable). Lives across geometry rebuilds.
    private readonly System.Collections.Generic.Dictionary<string, DBTexture?> _flatTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Generic.Dictionary<string, DBTexture?> _wallTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Generic.Dictionary<string, DBTexture?> _spriteTextures = new(StringComparer.OrdinalIgnoreCase);
    // 2D thing sprite quads, bucketed by sprite lump (alpha-blended). Things without a resolvable sprite
    // fall back to the colored diamond markers in _thingsVb.
    private readonly System.Collections.Generic.List<(GlVertexBuffer Vb, int Tris, DBTexture? Tex)> _spriteBuckets = new();
    private GlVertexBuffer? _linesVb;
    private int _lineCount;
    private GlVertexBuffer? _thingDirVb; // thing angle direction ticks, drawn above sprites so they stay visible
    private int _thingDirCount;
    private GlVertexBuffer? _thingsVb;
    private int _thingTris;
    private GlVertexBuffer? _selVertsVb;
    private int _selVertTris;
    private bool _geometryDirty = true;

    // 3D fly-mode state (toggled with Tab). Geometry built lazily into textured buckets.
    private bool _mode3D;
    private bool _walkMode;          // G toggles: camera snaps to floor + eye height instead of free flight
    private const double EyeHeight = 41; // Doom player view height above the floor
    private bool _geo3DDirty = true;
    private readonly System.Collections.Generic.List<(GlVertexBuffer Vb, int Tris, string Name)> _floor3D = new();
    private readonly System.Collections.Generic.List<(GlVertexBuffer Vb, int Tris, string Name)> _ceil3D = new();
    private readonly System.Collections.Generic.List<(GlVertexBuffer Vb, int Tris, string Name)> _wall3D = new();
    private Vector3 _cam3DPos;
    private double _yaw, _pitch;
    private bool _cam3DInit;
    private readonly System.Collections.Generic.HashSet<Key> _heldKeys = new();
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private double _lastTime;

    // 3D visual-editing target (surface under the crosshair) and its marker buffer.
    private VisualHit? _target3D;
    private string _target3DDesc = "";
    private string? _texClipboard3D; // texture name copied off a surface for painting onto others
    private bool _look3D;            // left-drag mouse-look active in 3D
    private bool _lookMoved;         // whether the left-drag actually moved (vs a click to select)
    private VisualHit? _drag3DTarget; // surface/thing captured for a right-drag height change
    private double _drag3DAccum;       // accumulated sub-unit drag movement
    private readonly System.Collections.Generic.List<VisualHit> _sel3D = new(); // multi-surface selection
    private GlVertexBuffer? _pick3DVb;
    private GlVertexBuffer? _things3DVb; // reused per-frame for camera-facing thing billboards
    /// <summary>Raised when the 3D crosshair target changes (for the status bar).</summary>
    public event Action<string>? Target3DChanged;

    public MapControl()
    {
        Focusable = true; // required to receive keyboard input for the 3D fly camera
    }

    private ResourceManager? _resources;
    /// <summary>Texture source for sector fills. Setting it invalidates the flat-texture cache and geometry.</summary>
    public ResourceManager? MapResources
    {
        get => _resources;
        set { _resources = value; _invalidateTextures = true; _geometryDirty = true; _geo3DDirty = true; RequestNextFrameRendering(); }
    }

    private GameConfiguration? _gameConfig;
    /// <summary>Game config used to resolve a thing's sprite name for 2D sprite rendering.</summary>
    public GameConfiguration? GameConfig
    {
        get => _gameConfig;
        set { _gameConfig = value; _activeThingsFilter = null; _thingsFilterResult = null; _thingsFilterHidden.Clear(); _geometryDirty = true; RequestNextFrameRendering(); }
    }

    private bool _needsFit;
    private MapSet? _map;
    public MapSet? Map
    {
        get => _map;
        // Defer the fit: when a map is set at startup the control isn't laid out yet (Bounds == 0),
        // so fitting now would compute a bogus zoom. Fit on the first render that has real dimensions.
        set { _map = value; _thingsFilterResult = null; _thingsFilterHidden.Clear(); _sel3D.Clear(); _geometryDirty = true; _geo3DDirty = true; _needsFit = true; _cam3DInit = false; _blockmapCache = null; RequestNextFrameRendering(); }
    }

    // 2D view-layer visibility toggles.
    private bool _showFills = true;
    private bool _showThings = true;

    private bool _thingArrows;
    /// <summary>When true, things draw as Doom-Builder-style colored discs with a direction arrow instead of sprites.</summary>
    public bool ThingArrows
    {
        get => _thingArrows;
        set { _thingArrows = value; _geometryDirty = true; RequestNextFrameRendering(); }
    }

    // Draw-geometry tool state. While active, left-clicks place loop vertices; closing builds a sector
    // (or, in lines-only mode, just the linedefs of the drawn polyline).
    private bool _drawMode;
    private bool _drawLinesOnly; // Shift+D: lay plain linedefs instead of building a sector
    private bool _drawClosed;    // set when the user closes the polyline by clicking the first point
    private readonly System.Collections.Generic.List<Vec2D> _drawPoints = new();
    private Vec2D _drawCursor;
    private Vec2D _cursorWorld; // last known cursor position in world space (for cursor-targeted actions)
    private GlVertexBuffer? _drawVb;
    private int _drawLineCount;
    private bool _drawDirty; // rebuild the preview buffer on the render thread, not from input handlers

    // Shape-draw tool: while active, a left-drag defines a bounding box that becomes a rectangle/ellipse sector.
    public enum ShapeKind { None, Rectangle, Ellipse }
    private ShapeKind _shapeKind = ShapeKind.None;
    private int _shapeSides = 24; // segments for the ellipse
    public ShapeKind CurrentShape => _shapeKind;

    // Thing categories hidden from rendering (keyed by config category, "(uncategorized)" for blank).
    private readonly System.Collections.Generic.HashSet<string> _hiddenThingCategories = new(StringComparer.OrdinalIgnoreCase);
    private ThingsFilterInfo? _activeThingsFilter;
    private ThingsFilterResult? _thingsFilterResult;
    private readonly System.Collections.Generic.HashSet<Thing> _thingsFilterHidden = new(ReferenceEqualityComparer.Instance);

    /// <summary>The display key for a thing's category (its config category, or "(uncategorized)").</summary>
    public static string ThingCategoryKey(string? category) => string.IsNullOrEmpty(category) ? "(uncategorized)" : category;

    public bool IsThingCategoryHidden(string categoryKey) => _hiddenThingCategories.Contains(categoryKey);

    public ThingsFilterInfo? ActiveThingsFilter => _activeThingsFilter;

    /// <summary>Shows or hides all things in a category (by <see cref="ThingCategoryKey"/>) and redraws.</summary>
    public void SetThingCategoryHidden(string categoryKey, bool hidden)
    {
        if (hidden) _hiddenThingCategories.Add(categoryKey); else _hiddenThingCategories.Remove(categoryKey);
        _geometryDirty = true;
        RequestNextFrameRendering();
    }

    public void SetActiveThingsFilter(ThingsFilterInfo? filter)
    {
        _activeThingsFilter = filter;
        _thingsFilterResult = null;
        _thingsFilterHidden.Clear();
        _geometryDirty = true;
        _geo3DDirty = true;
        RequestNextFrameRendering();
    }

    private bool ThingHidden2D(Thing t)
        => ThingCategoryHidden(t) || ThingFilterHidden2D(t);

    private bool ThingHidden3D(Thing t)
        => ThingCategoryHidden(t) || ThingFilterHidden3D(t);

    private bool ThingCategoryHidden(Thing t)
        => _hiddenThingCategories.Count > 0
           && _hiddenThingCategories.Contains(ThingCategoryKey(_gameConfig?.GetThing(t.Type)?.Category));

    private bool ThingFilterHidden2D(Thing t)
    {
        EnsureThingsFilterResult();
        return _thingsFilterHidden.Contains(t);
    }

    private bool ThingFilterHidden3D(Thing t)
    {
        EnsureThingsFilterResult();
        return _thingsFilterResult != null &&
               (!_thingsFilterResult.VisualVisibility.TryGetValue(t, out bool visible) || !visible);
    }

    private void EnsureThingsFilterResult()
    {
        if (_activeThingsFilter == null || _map == null || _gameConfig == null || _thingsFilterResult != null) return;

        _thingsFilterResult = ThingsFilterEvaluator.Evaluate(_map, _gameConfig, _activeThingsFilter);
        _thingsFilterHidden.Clear();
        foreach (var thing in _thingsFilterResult.HiddenThings) _thingsFilterHidden.Add(thing);
    }

    /// <summary>Toggles a shape-draw tool (off if the same kind is already active). Disables the polyline draw tool.</summary>
    public void SetShapeMode(ShapeKind kind)
    {
        _shapeKind = _shapeKind == kind ? ShapeKind.None : kind;
        if (_shapeKind != ShapeKind.None) { _drawMode = false; _drawPoints.Clear(); _drawDirty = true; }
        RequestNextFrameRendering();
    }
    private bool _invalidateTextures; // dispose cached GL textures on the render thread (context current)

    // Camera: world-space center + zoom in world-units-per-DIP.
    private double _camX, _camY, _zoom = 1.0;

    /// <summary>Which element class clicks select. Switched with the number keys 1-4.</summary>
    public enum EditMode { Vertices, Linedefs, Sectors, Things }
    private EditMode _editMode = EditMode.Linedefs;
    public EditMode CurrentEditMode => _editMode;
    /// <summary>Raised when the active selection mode changes (for the status bar).</summary>
    public event Action? ModeChanged;

    private void SetEditMode(EditMode m)
    {
        if (_editMode == m) return;
        _editMode = m;
        ModeChanged?.Invoke();
        Picked?.Invoke($"mode: {m}");
        RequestNextFrameRendering();
    }

    // Grid: power-of-two world-unit spacing, rendered behind geometry; snap aligns draws/moves to it.
    private int _gridSize = 64;
    private bool _snapToGrid = true;
    private GlVertexBuffer? _gridVb;
    private int _gridLineCount;
    private GlVertexBuffer? _blockmapVb;
    private int _blockmapLineCount;
    private DBuilder.Map.BlockMap? _blockmapCache; // built lazily while the overlay is shown
    private bool _showBlockmap;

    /// <summary>When true, draws the 128-unit blockmap grid (occupied blocks brighter) over the map.</summary>
    public bool ShowBlockmap
    {
        get => _showBlockmap;
        set { _showBlockmap = value; RequestNextFrameRendering(); }
    }

    private GlVertexBuffer? _nodesVb;
    private int _nodesLineCount;
    private bool _showNodes;
    private (Vec2D a, Vec2D b)[] _nodeLines = System.Array.Empty<(Vec2D, Vec2D)>();

    /// <summary>When true, overlays the BSP node partition lines (set via <see cref="SetNodeLines"/>).</summary>
    public bool ShowNodes
    {
        get => _showNodes;
        set { _showNodes = value; _nodesDirty = true; RequestNextFrameRendering(); }
    }
    private bool _nodesDirty;

    /// <summary>Supplies the BSP partition segments (world coordinates) for the nodes overlay.</summary>
    public void SetNodeLines(System.Collections.Generic.IReadOnlyList<(Vec2D a, Vec2D b)> lines)
    {
        _nodeLines = new (Vec2D, Vec2D)[lines.Count];
        for (int i = 0; i < lines.Count; i++) _nodeLines[i] = lines[i];
        _nodesDirty = true;
        RequestNextFrameRendering();
    }
    private enum DragKind { None, Pan, Move, Box }
    private bool _pressed;
    private DragKind _drag = DragKind.None;
    private bool _moveCandidate;
    private System.Collections.Generic.HashSet<Vertex>? _moveVerts; // vertices being dragged (captured at move start)
    private Point _dragStart;
    private Point _lastPointer;
    // Right-button: a drag pans, a click splits the nearest line. Decided on release.
    private bool _rightPressed, _rightDragging;
    // Rubber-band box selection (left-drag over empty space).
    private bool _boxAdditive;
    private Vec2D _boxStartWorld, _boxCurWorld;
    private GlVertexBuffer? _boxVb;

    /// <summary>Raised with the world coordinates under the cursor (for the status bar).</summary>
    public event Action<Vec2D>? CursorWorldMoved;

    /// <summary>Raised when a left-click pick selects (or clears) something; carries a short description.</summary>
    public event Action<string>? Picked;

    /// <summary>Raised just before an interactive edit mutates the map, so the host can snapshot for undo.</summary>
    public event Action<string>? EditBegun;

    /// <summary>Raised after the map changes (move/pick) so the host can refresh its panels.</summary>
    public event Action? Changed;

    /// <summary>Raised on a double-click after selecting a single element, so the host can open a property dialog.</summary>
    public event Action? EditRequested;

    /// <summary>
    /// Reveals a map element: selects it (switching to the matching edit mode) and centers the 2D view on
    /// <paramref name="focus"/>, zooming in if the view is currently zoomed far out. Used by the map check panel.
    /// </summary>
    public void NavigateTo(ISelectable? target, Vec2D? focus)
    {
        if (_map == null) return;
        if (target != null)
        {
            _map.ClearAllSelected();
            target.Selected = true;
            SetEditMode(target switch
            {
                Vertex => EditMode.Vertices,
                Sector => EditMode.Sectors,
                Thing => EditMode.Things,
                _ => EditMode.Linedefs, // Linedef and Sidedef both select in Linedefs mode
            });
        }
        if (focus is { } f)
        {
            _camX = f.x;
            _camY = f.y;
            if (_zoom > 1.0) _zoom = 1.0; // zoom in to reveal a small element, but never zoom further out
        }
        _geometryDirty = true;
        Changed?.Invoke();
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Shows an externally-set selection (e.g. from find): switches to <paramref name="mode"/> so clicks act on
    /// that element class, centers the view on <paramref name="focus"/> when given, and redraws.
    /// </summary>
    public void RevealSelection(EditMode mode, Vec2D? focus)
    {
        SetEditMode(mode);
        if (focus is { } f)
        {
            _camX = f.x;
            _camY = f.y;
            if (_zoom > 1.0) _zoom = 1.0;
        }
        _geometryDirty = true;
        Changed?.Invoke();
        RequestNextFrameRendering();
    }

    public void FitToMap()
    {
        if (_map == null || _map.Vertices.Count == 0) return;
        var (minX, minY, maxX, maxY) = _map.Bounds();
        _camX = (minX + maxX) * 0.5;
        _camY = (minY + maxY) * 0.5;
        double w = Math.Max(1, maxX - minX);
        double h = Math.Max(1, maxY - minY);
        double availW = Math.Max(1, Bounds.Width);
        double availH = Math.Max(1, Bounds.Height);
        _zoom = Math.Max(w / availW, h / availH) * 1.15;
        if (_zoom <= 0) _zoom = 1;
    }

    public void MarkGeometryDirty()
    {
        _thingsFilterResult = null;
        _thingsFilterHidden.Clear();
        _geometryDirty = true;
        _geo3DDirty = true;
        _blockmapCache = null;
        RequestNextFrameRendering();
    }

    public int SelectAllInCurrentMode()
    {
        if (_map == null) return 0;

        _map.ClearAllSelected();
        int count = 0;
        switch (_editMode)
        {
            case EditMode.Vertices:
                _map.SelectAllVertices();
                count = _map.SelectedVerticesCount;
                break;
            case EditMode.Things:
                foreach (var thing in _map.Things)
                {
                    if (ThingHidden2D(thing)) continue;
                    thing.Selected = true;
                    count++;
                }
                break;
            case EditMode.Sectors:
                _map.SelectAllSectors();
                count = _map.SelectedSectorsCount;
                break;
            default:
                _map.SelectAllLinedefs();
                count = _map.SelectedLinedefsCount;
                break;
        }

        MarkGeometryDirty();
        Changed?.Invoke();
        return count;
    }

    public int InvertSelectionInCurrentMode()
    {
        if (_map == null) return 0;

        switch (_editMode)
        {
            case EditMode.Vertices:
                _map.InvertSelectedVertices();
                break;
            case EditMode.Things:
                foreach (var thing in _map.Things)
                {
                    if (ThingHidden2D(thing)) continue;
                    thing.Selected = !thing.Selected;
                }
                break;
            case EditMode.Sectors:
                _map.InvertSelectedSectors();
                break;
            default:
                _map.InvertSelectedLinedefs();
                break;
        }

        MarkGeometryDirty();
        Changed?.Invoke();
        return _editMode switch
        {
            EditMode.Vertices => _map.SelectedVerticesCount,
            EditMode.Things => _map.SelectedThingsCount,
            EditMode.Sectors => _map.SelectedSectorsCount,
            _ => _map.SelectedLinedefsCount,
        };
    }

    // ---- GL lifecycle ----

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = new GL(new LamdaNativeContext(name => gl.GetProcAddress(name)));
        _device = new RenderDevice(_gl);
        _shader = new DBShader(_gl, VertexSrc, FragmentSrc);
        _linesVb = new GlVertexBuffer(_gl);
        _thingDirVb = new GlVertexBuffer(_gl);
        _thingsVb = new GlVertexBuffer(_gl);
        _selVertsVb = new GlVertexBuffer(_gl);
        _drawVb = new GlVertexBuffer(_gl);
        _gridVb = new GlVertexBuffer(_gl);
        _blockmapVb = new GlVertexBuffer(_gl);
        _nodesVb = new GlVertexBuffer(_gl);
        _boxVb = new GlVertexBuffer(_gl);
        _pick3DVb = new GlVertexBuffer(_gl);
        _things3DVb = new GlVertexBuffer(_gl);
        // 1x1 white placeholder so the sampler is always complete during untextured draws.
        _placeholderTex = new DBTexture(_gl);
        _placeholderTex.SetPixelsRgba8(1, 1, new byte[] { 255, 255, 255, 255 }, generateMipmaps: false);
        _device.SetTexture(0, _placeholderTex);
        _device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
        _device.SetSamplerState(TextureAddress.Wrap);
        _device.SetCullMode(Cull.None);
        _device.SetZEnable(false);
        _device.SetAlphaBlendEnable(false);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        foreach (var b in _fillBuckets) b.Vb.Dispose();
        _fillBuckets.Clear();
        foreach (var b in _floor3D) b.Vb.Dispose();
        foreach (var b in _ceil3D) b.Vb.Dispose();
        foreach (var b in _wall3D) b.Vb.Dispose();
        _floor3D.Clear(); _ceil3D.Clear(); _wall3D.Clear();
        foreach (var b in _spriteBuckets) b.Vb.Dispose();
        _spriteBuckets.Clear();
        foreach (var t in _flatTextures.Values) t?.Dispose();
        _flatTextures.Clear();
        foreach (var t in _wallTextures.Values) t?.Dispose();
        _wallTextures.Clear();
        foreach (var t in _spriteTextures.Values) t?.Dispose();
        _spriteTextures.Clear();
        _placeholderTex?.Dispose();
        _linesVb?.Dispose();
        _thingDirVb?.Dispose();
        _thingsVb?.Dispose();
        _selVertsVb?.Dispose();
        _drawVb?.Dispose();
        _gridVb?.Dispose();
        _blockmapVb?.Dispose();
        _nodesVb?.Dispose();
        _boxVb?.Dispose();
        _pick3DVb?.Dispose();
        _things3DVb?.Dispose();
        _shader?.Dispose();
        _device?.Dispose();
        _placeholderTex = null; _linesVb = null; _thingDirVb = null; _thingsVb = null; _selVertsVb = null; _drawVb = null; _gridVb = null; _blockmapVb = null; _nodesVb = null; _boxVb = null; _pick3DVb = null; _things3DVb = null; _shader = null; _device = null; _gl = null;
    }

    private void InvalidateTextures()
    {
        foreach (var t in _flatTextures.Values) t?.Dispose();
        _flatTextures.Clear();
        foreach (var t in _wallTextures.Values) t?.Dispose();
        _wallTextures.Clear();
        foreach (var t in _spriteTextures.Values) t?.Dispose();
        _spriteTextures.Clear();
    }

    // Returns the cached GL texture for a wall texture, uploading it from MapResources on first use.
    private DBTexture? GetWallTexture(string name)
    {
        if (_wallTextures.TryGetValue(name, out var cached)) return cached;
        DBTexture? tex = null;
        var img = _resources?.GetWallTexture(name);
        if (img != null && _device != null && _gl != null)
        {
            tex = new DBTexture(_gl);
            tex.SetPixelsRgba8(img.Width, img.Height, img.Rgba, generateMipmaps: false);
            _device.SetTexture(0, tex);
            _device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
            _device.SetSamplerState(TextureAddress.Wrap);
        }
        _wallTextures[name] = tex;
        return tex;
    }

    // Returns the cached GL texture for a sprite (clamped, transparent edges), uploading on first use.
    private DBTexture? GetSpriteTexture(string name)
    {
        if (_spriteTextures.TryGetValue(name, out var cached)) return cached;
        DBTexture? tex = null;
        var img = _resources?.GetSprite(name);
        if (img != null && _device != null && _gl != null)
        {
            tex = new DBTexture(_gl);
            tex.SetPixelsRgba8(img.Width, img.Height, img.Rgba, generateMipmaps: false);
            _device.SetTexture(0, tex);
            _device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
            _device.SetSamplerState(TextureAddress.Clamp);
        }
        _spriteTextures[name] = tex;
        return tex;
    }

    // Returns the cached GL texture for a flat, uploading it from MapResources on first use. Null when unresolved.
    private DBTexture? GetFlatTexture(string name)
    {
        if (_flatTextures.TryGetValue(name, out var cached)) return cached;
        DBTexture? tex = null;
        var img = _resources?.GetFlat(name);
        if (img != null && _device != null && _gl != null)
        {
            tex = new DBTexture(_gl);
            tex.SetPixelsRgba8(img.Width, img.Height, img.Rgba, generateMipmaps: false);
            _device.SetTexture(0, tex);
            _device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
            _device.SetSamplerState(TextureAddress.Wrap);
        }
        _flatTextures[name] = tex;
        return tex;
    }

    // ---- 3D fly mode ----

    private void Render3D(int pw, int ph)
    {
        if (_device is null || _map is null) return;
        if (_geo3DDirty) { Rebuild3D(); _geo3DDirty = false; }
        _blockmapCache ??= new DBuilder.Map.BlockMap(_map);
        UpdateFlyCamera();

        _device.SetZEnable(true);
        _device.SetUniform("tex0", 0);

        var pos = _cam3DPos;
        var view = Matrix4x4.CreateLookAt(pos, pos + Cam3DForward(), new Vector3(0, 0, 1));
        float aspect = ph > 0 ? (float)pw / ph : 1f;
        var persp = Matrix4x4.CreatePerspectiveFieldOfView((float)(75.0 * Math.PI / 180.0), aspect, 1f, 20000f);
        _device.SetUniform("projection", view * persp);

        DrawBuckets3D(_floor3D, wall: false);
        DrawBuckets3D(_ceil3D, wall: false);
        DrawBuckets3D(_wall3D, wall: true);
        DrawThings3D();

        UpdateTarget3D();
        DrawTargetHighlight3D();
    }

    // Draws things as upright camera-facing sprite billboards (rebuilt each frame; depth-tested against geometry).
    private void DrawThings3D()
    {
        if (_device is null || _map is null || _things3DVb is null) return;
        var right = new Vector3((float)Math.Sin(_yaw), -(float)Math.Cos(_yaw), 0);
        var up = new Vector3(0, 0, 1);

        var buckets = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _map.Things)
        {
            if (ThingHidden3D(t)) continue;
            string? sprite = _gameConfig?.GetThing(t.Type)?.Sprite;
            if (string.IsNullOrEmpty(sprite)) continue;
            var img = _resources?.GetSprite(sprite!);
            if (img == null || GetSpriteTexture(sprite!) == null) continue;

            double floorZ = (_blockmapCache?.GetSectorAt(t.Position) ?? _map.GetSectorAt(t.Position))?.GetFloorZ(t.Position) ?? 0;
            float hw = img.Width * 0.5f, hh = img.Height * 0.5f;
            double originZ = floorZ + t.Height;
            // Use the sprite hot-spot when present (OffsetX from left, OffsetY above the origin); else
            // fall back to centered horizontally with the bottom resting on the floor.
            float cz, hcx;
            if (img.OffsetX != 0 || img.OffsetY != 0)
            {
                cz = (float)(originZ + img.OffsetY - img.Height * 0.5); // top at origin+OffsetY, so center is half-height below
                hcx = img.Width * 0.5f - img.OffsetX;                    // shift so the hot-spot column sits at the thing
            }
            else { cz = (float)(originZ) + hh; hcx = 0f; }
            var c = new Vector3((float)t.Position.x, (float)t.Position.y, cz) + right * hcx;
            int col = t.Selected ? unchecked((int)0xfffff080) : unchecked((int)0xffffffff);
            FlatVertex V(Vector3 p, float u, float v) => new FlatVertex { x = p.X, y = p.Y, z = p.Z, w = 1, c = col, u = u, v = v };

            var bl = c - right * hw - up * hh; var br = c + right * hw - up * hh;
            var tr = c + right * hw + up * hh; var tl = c - right * hw + up * hh;
            if (!buckets.TryGetValue(sprite!, out var list)) { list = new(); buckets[sprite!] = list; }
            list.Add(V(tl, 0, 0)); list.Add(V(tr, 1, 0)); list.Add(V(br, 1, 1));
            list.Add(V(tl, 0, 0)); list.Add(V(br, 1, 1)); list.Add(V(bl, 0, 1));
        }
        if (buckets.Count == 0) return;

        _device.SetAlphaBlendEnable(true);
        _device.SetSourceBlend(Blend.SourceAlpha);
        _device.SetDestinationBlend(Blend.InverseSourceAlpha);
        _device.SetUniform("useTexture", 1f);
        foreach (var (name, verts) in buckets)
        {
            _device.SetTexture(0, GetSpriteTexture(name) ?? _placeholderTex);
            _device.SetBufferData(_things3DVb, verts.ToArray());
            _device.SetVertexBuffer(_things3DVb);
            _device.Draw(DBPrimitiveType.TriangleList, 0, verts.Count / 3);
        }
        _device.SetAlphaBlendEnable(false);
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
    }

    // Raycasts from the camera crosshair and records the targeted surface (for editing + highlight).
    private void UpdateTarget3D()
    {
        if (_map == null) { _target3D = null; return; }
        var f = Cam3DForward();
        _target3D = VisualPicking.Raycast(_map,
            _blockmapCache,
            new DBuilder.Geometry.Vector3D(_cam3DPos.X, _cam3DPos.Y, _cam3DPos.Z),
            new DBuilder.Geometry.Vector3D(f.X, f.Y, f.Z),
            ThingSize3D);

        string desc = _target3D == null ? "" : _target3D.Kind switch
        {
            VisualHitKind.Floor => $"floor (sector {_target3D.Sector?.Index}, z={_target3D.Sector?.FloorHeight})",
            VisualHitKind.Ceiling => $"ceiling (sector {_target3D.Sector?.Index}, z={_target3D.Sector?.CeilHeight})",
            VisualHitKind.Thing => $"thing {_target3D.Thing?.Type} (z={_target3D.Thing?.Height})",
            _ => $"wall (linedef {(_target3D.Line != null ? _map.Linedefs.IndexOf(_target3D.Line) : -1)})",
        };
        if (desc != _target3DDesc) { _target3DDesc = desc; Target3DChanged?.Invoke(desc); }
    }

    // Outlines selected surfaces (cyan) and the current crosshair target (yellow).
    private void DrawTargetHighlight3D()
    {
        if (_device == null || _pick3DVb == null || _map == null) return;
        var verts = new System.Collections.Generic.List<FlatVertex>();
        foreach (var h in _sel3D) AppendHitOutline(verts, h, unchecked((int)0xff00ccff)); // cyan = selected
        if (_target3D is { } target) AppendHitOutline(verts, target, unchecked((int)0xffffee00)); // yellow = target
        if (verts.Count == 0) return;

        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetBufferData(_pick3DVb, verts.ToArray());
        _device.SetVertexBuffer(_pick3DVb);
        _device.Draw(DBPrimitiveType.LineList, 0, verts.Count / 2);
    }

    // Appends a hit's outline edges (wall quad / thing box / sector edge loop) to the line list in the given color.
    private void AppendHitOutline(System.Collections.Generic.List<FlatVertex> verts, VisualHit hit, int color)
    {
        FlatVertex V(double x, double y, double z) => new FlatVertex { x = (float)x, y = (float)y, z = (float)z, w = 1, c = color };
        void Edge(Vec2D a, Vec2D b, double za, double zb) { verts.Add(V(a.x, a.y, za)); verts.Add(V(b.x, b.y, zb)); }

        if (hit.Kind == VisualHitKind.Wall && hit.Line is { } l)
        {
            var a = l.Start.Position; var b = l.End.Position;
            double bot = hit.Bottom, top = hit.Top;
            Edge(a, b, bot, bot); Edge(b, b, bot, top); Edge(b, a, top, top); Edge(a, a, top, bot);
        }
        else if (hit.Kind == VisualHitKind.Thing && hit.Thing is { } t)
        {
            double r = ThingSize3D(t).radius;
            double zb = hit.Bottom, zt = hit.Top;
            var p = t.Position;
            var c00 = new Vec2D(p.x - r, p.y - r); var c10 = new Vec2D(p.x + r, p.y - r);
            var c11 = new Vec2D(p.x + r, p.y + r); var c01 = new Vec2D(p.x - r, p.y + r);
            Edge(c00, c10, zb, zb); Edge(c10, c11, zb, zb); Edge(c11, c01, zb, zb); Edge(c01, c00, zb, zb);
            Edge(c00, c10, zt, zt); Edge(c10, c11, zt, zt); Edge(c11, c01, zt, zt); Edge(c01, c00, zt, zt);
            Edge(c00, c00, zb, zt); Edge(c10, c10, zb, zt); Edge(c11, c11, zb, zt); Edge(c01, c01, zb, zt);
        }
        else if (hit.Sector is { } s)
        {
            double z = hit.Bottom;
            foreach (var sd in s.Sidedefs)
                if (sd.Line is { } line) Edge(line.Start.Position, line.End.Position, z, z);
        }
    }

    // The texture name on the targeted surface (sector flat or the hit wall part), or null.
    private string? TargetTextureName3D()
    {
        var h = _target3D;
        if (h == null) return null;
        if (h.Kind == VisualHitKind.Floor) return h.Sector?.FloorTexture;
        if (h.Kind == VisualHitKind.Ceiling) return h.Sector?.CeilTexture;
        var sd = h.Front ? h.Line?.Front : h.Line?.Back;
        if (sd == null) return null;
        return sd.GetTexture(h.Part);
    }

    // Copies the targeted surface's texture into the 3D texture clipboard.
    private void CopyTexture3D()
    {
        var tex = TargetTextureName3D();
        if (string.IsNullOrEmpty(tex)) { Target3DChanged?.Invoke("nothing to copy"); return; }
        _texClipboard3D = tex;
        Target3DChanged?.Invoke($"copied texture {tex}");
    }

    /// <summary>Raised when the 3D user requests the texture browser (true = flats for a floor/ceiling target).</summary>
    public event Action<bool>? BrowseTexturesRequested;

    /// <summary>Applies a chosen texture name (from the browser) to the current 3D target and remembers it.</summary>
    public void ApplyChosenTexture(string name)
    {
        _texClipboard3D = name;
        ApplyTextureToTarget(name);
    }

    // Applies the 3D texture clipboard onto the targeted surface (undoable).
    private void ApplyTexture3D()
    {
        if (string.IsNullOrEmpty(_texClipboard3D)) { Target3DChanged?.Invoke("no copied texture (use C or T)"); return; }
        ApplyTextureToTarget(_texClipboard3D!);
    }

    private void ApplyTextureToTarget(string tex)
    {
        if (_map == null) return;
        var targets = EditTargets3D();
        if (targets.Count == 0) return;
        EditBegun?.Invoke("Apply texture");
        foreach (var h in targets) ApplyTextureToHit(h, tex);
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke($"applied texture {tex} to {targets.Count} surface(s)");
    }

    private static void ApplyTextureToHit(VisualHit h, string tex)
    {
        if (h.Kind == VisualHitKind.Floor && h.Sector is { } fs) fs.FloorTexture = tex;
        else if (h.Kind == VisualHitKind.Ceiling && h.Sector is { } cs) cs.CeilTexture = tex;
        else if (h.Kind == VisualHitKind.Wall)
        {
            var sd = h.Front ? h.Line?.Front : h.Line?.Back;
            if (sd == null) return;
            sd.SetTexture(h.Part, tex);
        }
    }

    // The sidedef on the camera-facing side of the targeted wall, or null.
    private Sidedef? TargetSidedef3D()
    {
        var h = _target3D;
        if (h == null || h.Kind != VisualHitKind.Wall || h.Line == null) return null;
        return h.Front ? h.Line.Front : h.Line.Back;
    }

    // Nudges the targeted wall's texture offset by 8 (Shift+arrows), undoable.
    private void NudgeTargetOffset3D(Key key)
    {
        if (TargetSidedef3D() is not { } sd) { Target3DChanged?.Invoke("aim at a wall to offset its texture"); return; }
        EditBegun?.Invoke("Texture offset");
        const int step = 8;
        switch (key)
        {
            case Key.Left: sd.OffsetX -= step; break;
            case Key.Right: sd.OffsetX += step; break;
            case Key.Up: sd.OffsetY -= step; break;
            case Key.Down: sd.OffsetY += step; break;
        }
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke($"offset {sd.OffsetX}, {sd.OffsetY}");
    }

    // Resets the targeted wall's texture offsets to zero, undoable.
    private void ResetTargetOffsets3D()
    {
        if (TargetSidedef3D() is not { } sd) { Target3DChanged?.Invoke("aim at a wall to reset offsets"); return; }
        EditBegun?.Invoke("Reset offsets");
        sd.OffsetX = 0;
        sd.OffsetY = 0;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke("reset offsets");
    }

    // Deletes the targeted thing, undoable.
    private void DeleteTargetThing3D()
    {
        if (_map == null || _target3D?.Thing is not { } t) { Target3DChanged?.Invoke("aim at a thing to delete it"); return; }
        EditBegun?.Invoke("Delete thing");
        _map.RemoveThing(t);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke("deleted thing");
    }

    // Auto-aligns textures along the targeted wall's run (A = X, Shift+A = Y), undoable.
    private void AutoAlignTarget3D(bool vertical)
    {
        if (TargetSidedef3D() is not { } sd) { Target3DChanged?.Invoke("aim at a wall to align textures"); return; }
        string tex = SidedefTextureAlignment.PrimaryTexture(sd);
        var img = _resources?.GetWallTexture(tex);
        EditBegun?.Invoke(vertical ? "Auto-align (Y)" : "Auto-align (X)");
        int n = vertical
            ? SidedefTextureAlignment.AutoAlignY(sd, img?.Height ?? 0)
            : SidedefTextureAlignment.AutoAlignX(sd, img?.Width ?? 0);
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke($"aligned {n} sidedef{(n == 1 ? "" : "s")} {(vertical ? "vertically" : "horizontally")}");
    }

    // Adjusts the selected (or targeted) sectors' brightness ([ darker / ] brighter), undoable.
    private void AdjustTargetBrightness3D(int delta)
    {
        if (_map == null) return;
        var done = new System.Collections.Generic.HashSet<Sector>();
        foreach (var h in EditTargets3D())
        {
            if (h.Sector is not { } s || !done.Add(s)) continue; // each sector once
            if (done.Count == 1) EditBegun?.Invoke("Change brightness");
            s.Brightness = Math.Clamp(s.Brightness + delta, 0, 255);
        }
        if (done.Count == 0) return;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
    }

    // Whether two hits refer to the same editable surface (ignoring distance/point).
    private static bool SameSurface3D(VisualHit a, VisualHit b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            VisualHitKind.Thing => ReferenceEquals(a.Thing, b.Thing),
            VisualHitKind.Wall => ReferenceEquals(a.Line, b.Line) && a.Front == b.Front && a.Part == b.Part,
            _ => ReferenceEquals(a.Sector, b.Sector), // floor/ceiling
        };
    }

    // Toggles the current crosshair target in/out of the multi-surface selection (left-click in 3D).
    private void ToggleSelection3D()
    {
        if (_target3D is not { } h) return;
        int idx = _sel3D.FindIndex(s => SameSurface3D(s, h));
        if (idx >= 0) _sel3D.RemoveAt(idx); else _sel3D.Add(h);
        Target3DChanged?.Invoke($"{_sel3D.Count} surface(s) selected");
        RequestNextFrameRendering();
    }

    private void ClearSelection3D()
    {
        if (_sel3D.Count == 0) return;
        _sel3D.Clear();
        Target3DChanged?.Invoke("selection cleared");
        RequestNextFrameRendering();
    }

    // The surfaces a discrete edit affects: the selection if any, otherwise just the crosshair target.
    private System.Collections.Generic.List<VisualHit> EditTargets3D()
        => _sel3D.Count > 0 ? _sel3D : (_target3D != null ? new() { _target3D } : new());

    // Selects the targeted element and opens its property dialog (reuses the 2D edit flow).
    private void OpenTargetDialog3D()
    {
        if (_map == null || _target3D is not { } h) return;
        _map.ClearAllSelected();
        switch (h.Kind)
        {
            case VisualHitKind.Floor:
            case VisualHitKind.Ceiling: if (h.Sector is { } s) s.Selected = true; break;
            case VisualHitKind.Wall: if (h.Line is { } l) l.Selected = true; break;
            case VisualHitKind.Thing: if (h.Thing is { } t) t.Selected = true; break;
        }
        EditRequested?.Invoke();
    }

    // The picking box size for a thing, from the game config (falls back to a default).
    private (double radius, double height) ThingSize3D(Thing t)
    {
        var info = _gameConfig?.GetThing(t.Type);
        return (info?.Width > 0 ? info.Width : 16, info?.Height > 0 ? info.Height : 16);
    }

    // Wheel: raises/lowers the selected (or targeted) floors/ceilings/things by the given step (undoable).
    private void AdjustTarget3D(int step)
    {
        if (_map == null) return;
        bool any = false;
        foreach (var h in EditTargets3D())
        {
            if (HeightEditLabel(h) == null) continue;
            if (!any) { EditBegun?.Invoke("Change height"); any = true; }
            ApplyHeightDelta(h, step);
        }
        if (!any) return;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
    }

    private static string? HeightEditLabel(VisualHit h) => h.Kind switch
    {
        VisualHitKind.Floor => "Change floor height",
        VisualHitKind.Ceiling => "Change ceiling height",
        VisualHitKind.Thing => "Change thing height",
        _ => null, // walls are not directly height-editable
    };

    private static void ApplyHeightDelta(VisualHit h, int delta)
    {
        switch (h.Kind)
        {
            case VisualHitKind.Floor: if (h.Sector is { } fs) fs.FloorHeight += delta; break;
            case VisualHitKind.Ceiling: if (h.Sector is { } cs) cs.CeilHeight += delta; break;
            case VisualHitKind.Thing: if (h.Thing is { } t) t.Height += delta; break;
        }
    }

    // The surfaces a right-drag affects: the selection if any, otherwise the captured target.
    private System.Collections.Generic.List<VisualHit> DragTargets3D()
        => _sel3D.Count > 0 ? _sel3D : (_drag3DTarget != null ? new() { _drag3DTarget } : new());

    // Routes a right-drag by the captured target's kind: a thing moves on the plane; a surface changes height.
    private void Drag3D(double dx, double dy)
    {
        if (_map == null || _drag3DTarget is not { } cap) return;
        var targets = DragTargets3D();

        if (cap.Kind == VisualHitKind.Thing)
        {
            // Move in the camera's horizontal basis; scale by hit distance so it feels consistent at any range.
            double scale = Math.Max(0.05, cap.Distance * 0.0015);
            var right = new Vec2D(Math.Sin(_yaw), -Math.Cos(_yaw));
            var fwd = new Vec2D(Math.Cos(_yaw), Math.Sin(_yaw));
            var delta = right * (dx * scale) + fwd * (-dy * scale); // up = forward
            foreach (var h in targets) if (h.Thing is { } t) t.Position += delta;
        }
        else
        {
            _drag3DAccum += -dy * 0.5; // dragging up raises; ~0.5 map units per pixel
            int d = (int)_drag3DAccum;
            if (d == 0) return;
            _drag3DAccum -= d;
            foreach (var h in targets) if (h.Kind != VisualHitKind.Thing && HeightEditLabel(h) != null) ApplyHeightDelta(h, d);
        }
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
    }

    private void DrawBuckets3D(System.Collections.Generic.List<(GlVertexBuffer Vb, int Tris, string Name)> buckets, bool wall)
    {
        if (_device is null) return;
        foreach (var b in buckets)
        {
            if (b.Tris == 0) continue;
            var tex = wall ? ResolveWallBucket(b.Name) : ResolveFlatBucket(b.Name);
            _device.SetUniform("useTexture", tex != null ? 1f : 0f);
            _device.SetTexture(0, tex ?? _placeholderTex);
            _device.SetVertexBuffer(b.Vb);
            _device.Draw(DBPrimitiveType.TriangleList, 0, b.Tris);
        }
    }

    // Resolves a flat/wall bucket's current GL texture, applying ANIMDEFS animation by name (empty name = none).
    private DBTexture? ResolveFlatBucket(string name)
        => name.Length == 0 ? null : GetFlatTexture(_resources != null ? _resources.CurrentFlatFrame(name, _clock.Elapsed.TotalSeconds) : name);

    private DBTexture? ResolveWallBucket(string name)
        => name.Length == 0 ? null : GetWallTexture(_resources != null ? _resources.CurrentTextureFrame(name, _clock.Elapsed.TotalSeconds) : name);

    private void Rebuild3D()
    {
        foreach (var b in _floor3D) b.Vb.Dispose();
        foreach (var b in _ceil3D) b.Vb.Dispose();
        foreach (var b in _wall3D) b.Vb.Dispose();
        _floor3D.Clear(); _ceil3D.Clear(); _wall3D.Clear();
        if (_map == null || _device is null || _gl is null) return;

        var floorB = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);
        var ceilB = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);
        var wallB = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);

        static System.Collections.Generic.List<FlatVertex> Bucket(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>> d, string k)
        { if (!d.TryGetValue(k, out var l)) { l = new(); d[k] = l; } return l; }

        static int Gray(int brightness, double scale)
        {
            double b = Math.Clamp(brightness / 255.0, 0.15, 1.0) * scale;
            byte g = (byte)Math.Clamp(b * 255, 0, 255);
            return unchecked((int)(0xff000000u | ((uint)g << 16) | ((uint)g << 8) | g));
        }

        // Floors + ceilings at real heights, textured by their flats.
        foreach (var s in _map.Sectors)
        {
            if (s.Sidedefs.Count == 0) continue;
            Triangulation tri;
            try { tri = Triangulation.Create(s); }
            catch { continue; }
            if (tri.Vertices.Count == 0) continue;

            string fName = s.FloorTexture ?? "-";
            string cName = s.CeilTexture ?? "-";
            string fKey = GetFlatTexture(fName) != null ? fName : "";
            string cKey = GetFlatTexture(cName) != null ? cName : "";
            int fc = Gray(s.Brightness, 1.0);
            int cc = Gray(s.Brightness, 0.85);
            var fl = Bucket(floorB, fKey);
            var cl = Bucket(ceilB, cKey);
            for (int i = 0; i < tri.Vertices.Count; i++)
            {
                var p = tri.Vertices[i];
                fl.Add(new FlatVertex { x = (float)p.x, y = (float)p.y, z = (float)s.GetFloorZ(p), w = 1, c = fc, u = (float)(p.x / 64.0), v = (float)(p.y / 64.0) });
                cl.Add(new FlatVertex { x = (float)p.x, y = (float)p.y, z = (float)s.GetCeilZ(p), w = 1, c = cc, u = (float)(p.x / 64.0), v = (float)(p.y / 64.0) });
            }
        }

        // Walls: one-sided full height; two-sided lower/upper steps. Pegging follows the Doom flags.
        foreach (var l in _map.Linedefs)
        {
            var a = l.Start.Position; var b = l.End.Position;
            var front = l.Front; var back = l.Back;
            var fs = front?.Sector; var bs = back?.Sector;
            bool unpegTop = (l.Flags & 0x0008) != 0;     // ML_DONTPEGTOP
            bool unpegBottom = (l.Flags & 0x0010) != 0;  // ML_DONTPEGBOTTOM
            if (fs != null && bs == null && front != null)
            {
                // One-sided middle: top-pegged by default, floor-pegged when lower-unpegged.
                var peg = unpegBottom ? WallPeg.BottomUp : WallPeg.Top;
                PushWall(wallB, a, b, fs.GetFloorZ(a), fs.GetFloorZ(b), fs.GetCeilZ(a), fs.GetCeilZ(b), front.MidTexture, fs.Brightness, front.OffsetX, front.OffsetY, peg, 0, 0, Gray);
            }
            else if (fs != null && bs != null && front != null)
            {
                double fFa = fs.GetFloorZ(a), fFb = fs.GetFloorZ(b), bFa = bs.GetFloorZ(a), bFb = bs.GetFloorZ(b);
                if (fFa != bFa || fFb != bFb)
                {
                    // Lower step: top-pegged at the higher floor by default; pegged to the ceiling when lower-unpegged.
                    var peg = unpegBottom ? WallPeg.Custom : WallPeg.Top;
                    PushWall(wallB, a, b, Math.Min(fFa, bFa), Math.Min(fFb, bFb), Math.Max(fFa, bFa), Math.Max(fFb, bFb), front.LowTexture, fs.Brightness, front.OffsetX, front.OffsetY, peg, fs.GetCeilZ(a), fs.GetCeilZ(b), Gray);
                }
                double fCa = fs.GetCeilZ(a), fCb = fs.GetCeilZ(b), bCa = bs.GetCeilZ(a), bCb = bs.GetCeilZ(b);
                if (fCa != bCa || fCb != bCb)
                {
                    // Upper step: bottom-pegged at the lower ceiling by default; top-pegged when upper-unpegged.
                    var peg = unpegTop ? WallPeg.Top : WallPeg.BottomUp;
                    PushWall(wallB, a, b, Math.Min(fCa, bCa), Math.Min(fCb, bCb), Math.Max(fCa, bCa), Math.Max(fCb, bCb), front.HighTexture, fs.Brightness, front.OffsetX, front.OffsetY, peg, 0, 0, Gray);
                }
            }
        }

        // GZDoom 3D floors: render each control sector's slab (top + bottom flats and side walls) into target sectors.
        var tdf = DBuilder.Map.ThreeDFloors.Resolve(_map);
        foreach (var (sector, floors) in tdf)
        {
            if (sector.Sidedefs.Count == 0) continue;
            Triangulation tri;
            try { tri = Triangulation.Create(sector); }
            catch { continue; }
            if (tri.Vertices.Count == 0) continue;

            foreach (var f in floors)
            {
                if (f.Alpha == 0 || f.Top <= f.Bottom) continue;
                int tc = Gray(f.Brightness, 1.0), bc = Gray(f.Brightness, 0.85);
                var topL = Bucket(floorB, GetFlatTexture(f.TopFlat) != null ? f.TopFlat : "");
                var botL = Bucket(ceilB, GetFlatTexture(f.BottomFlat) != null ? f.BottomFlat : "");
                for (int i = 0; i < tri.Vertices.Count; i++)
                {
                    var p = tri.Vertices[i];
                    topL.Add(new FlatVertex { x = (float)p.x, y = (float)p.y, z = (float)f.Top, w = 1, c = tc, u = (float)(p.x / 64.0), v = (float)(p.y / 64.0) });
                    botL.Add(new FlatVertex { x = (float)p.x, y = (float)p.y, z = (float)f.Bottom, w = 1, c = bc, u = (float)(p.x / 64.0), v = (float)(p.y / 64.0) });
                }

                // Slab side walls around the target sector's perimeter.
                foreach (var sd in sector.Sidedefs)
                {
                    if (sd.Line == null) continue;
                    PushWall(wallB, sd.Line.Start.Position, sd.Line.End.Position,
                        f.Bottom, f.Bottom, f.Top, f.Top, f.SideTexture, f.Brightness, 0, 0, WallPeg.Top, 0, 0, Gray);
                }
            }
        }

        UploadBuckets(floorB, _floor3D);
        UploadBuckets(ceilB, _ceil3D);
        UploadBuckets(wallB, _wall3D);
    }

    // How a wall texture is vertically pegged: Top = texture top at the quad top; BottomUp = texture bottom at the
    // quad bottom; Custom = texture top at a caller-supplied world Z (used for lower-unpegged, pegged to the ceiling).
    private enum WallPeg { Top, BottomUp, Custom }

    private void PushWall(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>> wallB,
        Vec2D a, Vec2D b, double botZa, double botZb, double topZa, double topZb, string texName, int brightness,
        int offsetX, int offsetY, WallPeg peg, double customPegA, double customPegB, Func<int, double, int> gray)
    {
        if (topZa <= botZa && topZb <= botZb) return;
        bool textured = GetWallTexture(texName) != null;
        string key = textured ? texName : "";
        int texW = textured ? (_resources!.GetWallTexture(texName)!.Width) : 64;
        int texH = textured ? (_resources!.GetWallTexture(texName)!.Height) : 64;
        double len = (b - a).GetLength();
        // U runs by world distance along the wall + the sidedef X offset.
        double u0 = offsetX / (double)texW;
        double u1 = (len + offsetX) / texW;
        int c = textured ? gray(brightness, 1.0) : gray(brightness, 0.6);

        // pegZ is the world Z that maps to v=0 (texture top), per endpoint, per pegging mode.
        double pegA = peg switch { WallPeg.Top => topZa, WallPeg.BottomUp => botZa + texH, _ => customPegA };
        double pegB = peg switch { WallPeg.Top => topZb, WallPeg.BottomUp => botZb + texH, _ => customPegB };
        float Vat(double pegZ, double z) => (float)((pegZ - z + offsetY) / texH);

        if (!wallB.TryGetValue(key, out var list)) { list = new(); wallB[key] = list; }
        FlatVertex V(Vec2D p, double z, double u, float vv) => new FlatVertex { x = (float)p.x, y = (float)p.y, z = (float)z, w = 1, c = c, u = (float)u, v = vv };
        var bl = V(a, botZa, u0, Vat(pegA, botZa));
        var br = V(b, botZb, u1, Vat(pegB, botZb));
        var tr = V(b, topZb, u1, Vat(pegB, topZb));
        var tl = V(a, topZa, u0, Vat(pegA, topZa));
        list.Add(bl); list.Add(br); list.Add(tr);
        list.Add(bl); list.Add(tr); list.Add(tl);
    }

    private void UploadBuckets(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>> src,
        System.Collections.Generic.List<(GlVertexBuffer Vb, int Tris, string Name)> dest)
    {
        foreach (var (key, verts) in src)
        {
            if (verts.Count == 0) continue;
            var vb = new GlVertexBuffer(_gl!);
            _device!.SetBufferData(vb, verts.ToArray());
            dest.Add((vb, verts.Count / 3, key)); // texture resolved (and animated) at draw time
        }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_device is null || _shader is null) return;

        double scaling = VisualRoot?.RenderScaling ?? 1.0;
        int pw = Math.Max(1, (int)(Bounds.Width * scaling));
        int ph = Math.Max(1, (int)(Bounds.Height * scaling));
        _device.SetViewport(pw, ph);

        _device.StartRendering(clear: true, clearColorArgb: 0xff10131a);
        _device.SetShader(_shader);

        // GL resource disposal must happen here (context current), never from input handlers.
        if (_invalidateTextures) { InvalidateTextures(); _invalidateTextures = false; }

        if (_mode3D && _map != null)
        {
            Render3D(pw, ph);
            RequestNextFrameRendering(); // keep the fly loop animating
            return;
        }

        _device.SetZEnable(false); // 2D overlay draws back-to-front, no depth test

        if (_map != null)
        {
            if (_needsFit && Bounds.Width > 1 && Bounds.Height > 1) { FitToMap(); _needsFit = false; }
            if (_geometryDirty) { RebuildGeometry(); _geometryDirty = false; }

            double halfW = Bounds.Width * 0.5 * _zoom;
            double halfH = Bounds.Height * 0.5 * _zoom;
            var proj = Matrix4x4.CreateOrthographicOffCenter(
                (float)(_camX - halfW), (float)(_camX + halfW),
                (float)(_camY - halfH), (float)(_camY + halfH),
                -1, 1);
            _device.SetUniform("projection", proj);
            _device.SetUniform("tex0", 0);

            // Grid behind everything (visible in the void between sectors).
            DrawGrid();

            // Draw order: sector fills (textured) -> lines -> things/sprites -> selection markers.
            if (_showFills)
            {
                foreach (var bucket in _fillBuckets)
                {
                    if (bucket.Tris == 0) continue;
                    var tex = ResolveFlatBucket(bucket.Name);
                    _device.SetUniform("useTexture", tex != null ? 1f : 0f);
                    _device.SetTexture(0, tex ?? _placeholderTex);
                    _device.SetVertexBuffer(bucket.Vb);
                    _device.Draw(DBPrimitiveType.TriangleList, 0, bucket.Tris);
                }
                // Keep redrawing so animated flats cycle (2D already idles otherwise).
                if (_resources?.HasAnimations == true) RequestNextFrameRendering();
            }

            _device.SetUniform("useTexture", 0f);
            _device.SetTexture(0, _placeholderTex);
            if (_showThings && _thingTris > 0 && _thingsVb != null)
            {
                _device.SetVertexBuffer(_thingsVb);
                _device.Draw(DBPrimitiveType.TriangleList, 0, _thingTris);
            }
            if (_lineCount > 0 && _linesVb != null)
            {
                _device.SetVertexBuffer(_linesVb);
                _device.Draw(DBPrimitiveType.LineList, 0, _lineCount);
            }

            // Thing sprites (alpha-blended, above lines).
            if (_showThings && _spriteBuckets.Count > 0)
            {
                _device.SetAlphaBlendEnable(true);
                _device.SetSourceBlend(Blend.SourceAlpha);
                _device.SetDestinationBlend(Blend.InverseSourceAlpha);
                _device.SetUniform("useTexture", 1f);
                foreach (var b in _spriteBuckets)
                {
                    if (b.Tris == 0) continue;
                    _device.SetTexture(0, b.Tex ?? _placeholderTex);
                    _device.SetVertexBuffer(b.Vb);
                    _device.Draw(DBPrimitiveType.TriangleList, 0, b.Tris);
                }
                _device.SetAlphaBlendEnable(false);
                _device.SetUniform("useTexture", 0f);
                _device.SetTexture(0, _placeholderTex);
            }

            // Thing direction ticks above sprites so the facing stays visible over the sprite art.
            if (_showThings && _thingDirCount > 0 && _thingDirVb != null)
            {
                _device.SetVertexBuffer(_thingDirVb);
                _device.Draw(DBPrimitiveType.LineList, 0, _thingDirCount);
            }

            if (_selVertTris > 0 && _selVertsVb != null)
            {
                _device.SetVertexBuffer(_selVertsVb);
                _device.Draw(DBPrimitiveType.TriangleList, 0, _selVertTris);
            }

            DrawBlockmap(); // debug overlay on top of geometry
            DrawNodes();    // BSP partition lines overlay

            // In-progress draw-tool polyline on top. Rebuild its buffer here (render thread) when dirty.
            if (_drawDirty) { RebuildDrawPreview(); _drawDirty = false; }
            if (_drawLineCount > 0 && _drawVb != null)
            {
                _device.SetVertexBuffer(_drawVb);
                _device.Draw(DBPrimitiveType.LineList, 0, _drawLineCount);
            }

            DrawSelectionBox();
        }
    }

    // Draws the rubber-band selection rectangle outline while a box drag is in progress.
    private void DrawSelectionBox()
    {
        if (_drag != DragKind.Box || _device is null || _boxVb is null) return;
        double x0 = Math.Min(_boxStartWorld.x, _boxCurWorld.x), x1 = Math.Max(_boxStartWorld.x, _boxCurWorld.x);
        double y0 = Math.Min(_boxStartWorld.y, _boxCurWorld.y), y1 = Math.Max(_boxStartWorld.y, _boxCurWorld.y);
        const int c = unchecked((int)0xffffee00);
        var p00 = new Vec2D(x0, y0); var p10 = new Vec2D(x1, y0); var p11 = new Vec2D(x1, y1); var p01 = new Vec2D(x0, y1);
        var v = new[]
        {
            FV(p00, c), FV(p10, c), FV(p10, c), FV(p11, c),
            FV(p11, c), FV(p01, c), FV(p01, c), FV(p00, c),
        };
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetBufferData(_boxVb, v);
        _device.SetVertexBuffer(_boxVb);
        _device.Draw(DBPrimitiveType.LineList, 0, 4);
    }

    private void RebuildGeometry()
    {
        if (_map == null || _device is null) return;

        // Sector fills, bucketed by floor flat texture (when resolvable) else an untextured gray bucket.
        RebuildFills();

        // Lines (each linedef segment plus a short front-side tick at its midpoint showing orientation).
        if (_linesVb != null)
        {
            const double frontTick = 6;
            var lv = new System.Collections.Generic.List<FlatVertex>(_map.Linedefs.Count * 4);
            foreach (var l in _map.Linedefs)
            {
                int c = LineColor(l);
                lv.Add(FV(l.Start.Position, c));
                lv.Add(FV(l.End.Position, c));

                var dir = l.End.Position - l.Start.Position;
                double len = dir.GetLength();
                if (len > 0.0001)
                {
                    // Front side is to the right of start->end (Doom convention): right-hand normal (dy, -dx).
                    var mid = new Vec2D((l.Start.Position.x + l.End.Position.x) * 0.5, (l.Start.Position.y + l.End.Position.y) * 0.5);
                    var nrm = new Vec2D(dir.y / len, -dir.x / len);
                    lv.Add(FV(mid, c));
                    lv.Add(FV(new Vec2D(mid.x + nrm.x * frontTick, mid.y + nrm.y * frontTick), c));
                }
            }
            _device.SetBufferData(_linesVb, lv.ToArray());
            _lineCount = lv.Count / 2;
        }

        // Thing direction ticks (own buffer, drawn above sprites). Skipped in arrow mode, which draws its own arrow.
        if (_thingDirVb != null)
        {
            var dv = new System.Collections.Generic.List<FlatVertex>();
            if (!_thingArrows)
            {
                const double tickLen = 18;
                foreach (var t in _map.Things)
                {
                    if (ThingHidden2D(t)) continue;
                    double a = t.Angle * Math.PI / 180.0;
                    int c = t.Selected ? unchecked((int)0xffffee00) : unchecked((int)0xffd0d8e0);
                    var p = t.Position;
                    var tip = new Vec2D(p.x + Math.Cos(a) * tickLen, p.y + Math.Sin(a) * tickLen);
                    dv.Add(FV(p, c));
                    dv.Add(FV(tip, c));
                }
            }
            if (dv.Count > 0) _device.SetBufferData(_thingDirVb, dv.ToArray());
            _thingDirCount = dv.Count / 2;
        }

        // Things: render real sprites where the config + resources resolve one (alpha-blended quads,
        // bucketed by sprite lump); the rest fall back to colored diamond markers in _thingsVb.
        foreach (var b in _spriteBuckets) b.Vb.Dispose();
        _spriteBuckets.Clear();
        var spriteVerts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);

        if (_thingsVb != null)
        {
            var tv = new System.Collections.Generic.List<FlatVertex>();
            const double s = 10;
            foreach (var t in _map.Things)
            {
                if (ThingHidden2D(t)) continue;
                // Arrow mode: Doom-Builder-style colored disc + direction arrow (no sprites).
                if (_thingArrows)
                {
                    BuildThingDisc(tv, t);
                    continue;
                }

                string? sprite = _gameConfig?.GetThing(t.Type)?.Sprite;
                if (!string.IsNullOrEmpty(sprite) && GetSpriteTexture(sprite!) is { } && _resources?.GetSprite(sprite!) is { } img)
                {
                    int sc = t.Selected ? unchecked((int)0xfffff080) : unchecked((int)0xffffffff);
                    double hw = img.Width * 0.5, hh = img.Height * 0.5;
                    var p = t.Position;
                    if (!spriteVerts.TryGetValue(sprite!, out var list)) { list = new(); spriteVerts[sprite!] = list; }
                    // Image top (v=0) maps to higher world-y so the sprite stands upright on screen.
                    FlatVertex SV(double x, double y, float u, float v) => new FlatVertex { x = (float)x, y = (float)y, z = 0, w = 1, c = sc, u = u, v = v };
                    var tl = SV(p.x - hw, p.y + hh, 0, 0);
                    var tr = SV(p.x + hw, p.y + hh, 1, 0);
                    var brv = SV(p.x + hw, p.y - hh, 1, 1);
                    var bl = SV(p.x - hw, p.y - hh, 0, 1);
                    list.Add(tl); list.Add(tr); list.Add(brv);
                    list.Add(tl); list.Add(brv); list.Add(bl);
                    continue;
                }

                int c = t.Selected ? unchecked((int)0xffffee00) : ThingColor(t.Type);
                var pp = t.Position;
                var n = new Vec2D(pp.x, pp.y + s);
                var e = new Vec2D(pp.x + s, pp.y);
                var so = new Vec2D(pp.x, pp.y - s);
                var w = new Vec2D(pp.x - s, pp.y);
                tv.Add(FV(pp, c)); tv.Add(FV(n, c)); tv.Add(FV(e, c));
                tv.Add(FV(pp, c)); tv.Add(FV(e, c)); tv.Add(FV(so, c));
                tv.Add(FV(pp, c)); tv.Add(FV(so, c)); tv.Add(FV(w, c));
                tv.Add(FV(pp, c)); tv.Add(FV(w, c)); tv.Add(FV(n, c));
            }
            var arr = tv.ToArray();
            if (arr.Length > 0) _device.SetBufferData(_thingsVb, arr);
            _thingTris = arr.Length / 3;
        }

        foreach (var (name, verts) in spriteVerts)
        {
            if (verts.Count == 0) continue;
            var vb = new GlVertexBuffer(_gl!);
            _device.SetBufferData(vb, verts.ToArray());
            _spriteBuckets.Add((vb, verts.Count / 3, GetSpriteTexture(name)));
        }

        // Selected-vertex highlight markers.
        if (_selVertsVb != null)
        {
            var vv = new System.Collections.Generic.List<FlatVertex>();
            const int mc = unchecked((int)0xffffee00);
            double s = 6;
            foreach (var v in _map.Vertices)
            {
                if (!v.Selected) continue;
                var p = v.Position;
                var n = new Vec2D(p.x, p.y + s);
                var e = new Vec2D(p.x + s, p.y);
                var so = new Vec2D(p.x, p.y - s);
                var w = new Vec2D(p.x - s, p.y);
                vv.Add(FV(p, mc)); vv.Add(FV(n, mc)); vv.Add(FV(e, mc));
                vv.Add(FV(p, mc)); vv.Add(FV(e, mc)); vv.Add(FV(so, mc));
                vv.Add(FV(p, mc)); vv.Add(FV(so, mc)); vv.Add(FV(w, mc));
                vv.Add(FV(p, mc)); vv.Add(FV(w, mc)); vv.Add(FV(n, mc));
            }
            var arr = vv.ToArray();
            if (arr.Length > 0) _device.SetBufferData(_selVertsVb, arr);
            _selVertTris = arr.Length / 3;
        }
    }

    // Builds sector fills bucketed by floor flat texture (when resolvable) else a single untextured gray bucket.
    private void RebuildFills()
    {
        foreach (var b in _fillBuckets) b.Vb.Dispose();
        _fillBuckets.Clear();
        if (_map == null || _device is null || _gl is null) return;

        var buckets = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);
        foreach (var sector in _map.Sectors)
        {
            if (sector.Sidedefs.Count == 0) continue;
            Triangulation tri;
            try { tri = Triangulation.Create(sector); }
            catch { continue; }
            if (tri.Vertices.Count == 0) continue;

            string flatName = sector.FloorTexture ?? "-";
            bool textured = GetFlatTexture(flatName) != null;
            string key = textured ? flatName : "";
            int c = textured ? TexturedFillColor(sector) : SectorFillColor(sector);

            if (!buckets.TryGetValue(key, out var list)) { list = new(); buckets[key] = list; }
            for (int i = 0; i < tri.Vertices.Count; i++)
            {
                var p = tri.Vertices[i];
                list.Add(new FlatVertex { x = (float)p.x, y = (float)p.y, z = 0, w = 1, c = c, u = (float)(p.x / 64.0), v = (float)(p.y / 64.0) });
            }
        }

        foreach (var (key, verts) in buckets)
        {
            if (verts.Count == 0) continue;
            var vb = new GlVertexBuffer(_gl);
            _device.SetBufferData(vb, verts.ToArray());
            _fillBuckets.Add((vb, verts.Count / 3, key)); // flat resolved (and animated) at draw time
        }
    }

    // Brightness-shaded color used to modulate a textured flat (selected sectors tint cyan).
    private static int TexturedFillColor(Sector s)
    {
        double b = Math.Clamp(s.Brightness / 255.0, 0.2, 1.0);
        byte g = (byte)(b * 255);
        if (s.Selected)
            return unchecked((int)(0xff000000u | ((uint)(g / 2) << 16) | ((uint)g << 8) | g));
        return unchecked((int)(0xff000000u | ((uint)g << 16) | ((uint)g << 8) | g));
    }

    // Untextured fallback fill: dim gray so the line/thing overlays stay legible (selected -> cyan).
    private static int SectorFillColor(Sector s)
    {
        double br = Math.Clamp(s.Brightness / 255.0, 0.12, 1.0) * 0.45;
        byte g = (byte)Math.Clamp(br * 255, 0, 255);
        if (s.Selected)
            return unchecked((int)(0xff000000u | ((uint)(g / 2) << 16) | ((uint)Math.Min(255, g + 60) << 8) | (uint)Math.Min(255, g + 80)));
        return unchecked((int)(0xff000000u | ((uint)g << 16) | ((uint)g << 8) | g));
    }

    private static int ThingColor(int type) => type switch
    {
        1 or 2 or 3 or 4 => unchecked((int)0xff40ff40),     // player starts - green
        >= 3000 and < 3100 => unchecked((int)0xffff5050),   // monsters - red
        2014 => unchecked((int)0xff60d0ff),                 // health bonus - cyan
        2018 => unchecked((int)0xff8080ff),                 // armor - blue
        _ => unchecked((int)0xffd0d0d0),
    };

    // Classic 16-colour console palette used by the .cfg thing category "color" index.
    private static int Color16(int i)
    {
        uint rgb = (i & 15) switch
        {
            0 => 0x000000, 1 => 0x0000AA, 2 => 0x00AA00, 3 => 0x00AAAA,
            4 => 0xAA0000, 5 => 0xAA00AA, 6 => 0xAA5500, 7 => 0xAAAAAA,
            8 => 0x555555, 9 => 0x5555FF, 10 => 0x55FF55, 11 => 0x55FFFF,
            12 => 0xFF5555, 13 => 0xFF55FF, 14 => 0xFFFF55, _ => 0xFFFFFF,
        };
        return unchecked((int)(0xff000000u | rgb));
    }

    // Appends a Doom-Builder-style colored disc + direction arrow for a thing into the (untextured) list.
    private void BuildThingDisc(System.Collections.Generic.List<FlatVertex> list, Thing t)
    {
        const double radius = 11;
        const int segments = 14;
        var p = t.Position;
        int catColor = _gameConfig?.GetThing(t.Type) is { } info ? Color16(info.Color) : ThingColor(t.Type);
        int disc = t.Selected ? Brighten(catColor) : catColor;

        // Disc as a triangle fan around the center.
        for (int i = 0; i < segments; i++)
        {
            double a0 = i * 2 * Math.PI / segments;
            double a1 = (i + 1) * 2 * Math.PI / segments;
            var v0 = new Vec2D(p.x + Math.Cos(a0) * radius, p.y + Math.Sin(a0) * radius);
            var v1 = new Vec2D(p.x + Math.Cos(a1) * radius, p.y + Math.Sin(a1) * radius);
            list.Add(FV(p, disc)); list.Add(FV(v0, disc)); list.Add(FV(v1, disc));
        }

        // Direction arrow (a dark wedge from center toward the thing's angle).
        double rad = t.Angle * Math.PI / 180.0;
        var dir = new Vec2D(Math.Cos(rad), Math.Sin(rad));
        var perp = new Vec2D(-dir.y, dir.x);
        const int arrow = unchecked((int)0xff101010);
        var tip = new Vec2D(p.x + dir.x * radius * 1.0, p.y + dir.y * radius * 1.0);
        var baseL = new Vec2D(p.x + perp.x * radius * 0.45, p.y + perp.y * radius * 0.45);
        var baseR = new Vec2D(p.x - perp.x * radius * 0.45, p.y - perp.y * radius * 0.45);
        list.Add(FV(tip, arrow)); list.Add(FV(baseL, arrow)); list.Add(FV(baseR, arrow));
    }

    private static int Brighten(int argb)
    {
        uint u = unchecked((uint)argb);
        byte r = (byte)Math.Min(255, ((u >> 16) & 0xFF) + 80);
        byte g = (byte)Math.Min(255, ((u >> 8) & 0xFF) + 80);
        byte b = (byte)Math.Min(255, (u & 0xFF) + 80);
        return unchecked((int)(0xff000000u | ((uint)r << 16) | ((uint)g << 8) | b));
    }

    private static int LineColor(Linedef l)
    {
        if (l.Selected) return unchecked((int)0xffffee00);                       // yellow
        if ((l.Front?.Sector?.Selected ?? false) || (l.Back?.Sector?.Selected ?? false))
            return unchecked((int)0xff00ccff);                                   // cyan
        bool twoSided = l.Front != null && l.Back != null;
        return twoSided ? unchecked((int)0xff8090a0) : unchecked((int)0xffe0e0e0);
    }

    private static FlatVertex FV(Vec2D p, int color)
        => new FlatVertex { x = (float)p.x, y = (float)p.y, z = 0, w = 1, c = color, u = 0, v = 0 };

    // ---- Input ----

    private Vec2D ToWorld(Point p)
        => new Vec2D(_camX + (p.X - Bounds.Width * 0.5) * _zoom,
                     _camY - (p.Y - Bounds.Height * 0.5) * _zoom);

    // Rounds a world point to the nearest grid intersection (identity when snapping is off).
    private Vec2D SnapToGrid(Vec2D w)
    {
        if (!_snapToGrid || _gridSize <= 0) return w;
        return new Vec2D(Math.Round(w.x / _gridSize) * _gridSize, Math.Round(w.y / _gridSize) * _gridSize);
    }

    // Builds and draws the visible grid as a line list. Skips when cells would be denser than a few pixels.
    private void DrawGrid()
    {
        if (_device is null || _gridVb is null || _gridSize <= 0) { _gridLineCount = 0; return; }
        if (_gridSize / _zoom < 4) { _gridLineCount = 0; return; }

        double halfW = Bounds.Width * 0.5 * _zoom;
        double halfH = Bounds.Height * 0.5 * _zoom;
        double left = _camX - halfW, right = _camX + halfW;
        double bottom = _camY - halfH, top = _camY + halfH;

        const int col = unchecked((int)0xff20242c);   // dim grid
        const int axis = unchecked((int)0xff3a4654);   // brighter x=0 / y=0 axes
        var verts = new System.Collections.Generic.List<FlatVertex>();
        int x0 = (int)Math.Floor(left / _gridSize), x1 = (int)Math.Ceiling(right / _gridSize);
        int y0 = (int)Math.Floor(bottom / _gridSize), y1 = (int)Math.Ceiling(top / _gridSize);
        for (int gx = x0; gx <= x1; gx++)
        {
            double x = gx * (double)_gridSize; int c = gx == 0 ? axis : col;
            verts.Add(FV(new Vec2D(x, bottom), c)); verts.Add(FV(new Vec2D(x, top), c));
        }
        for (int gy = y0; gy <= y1; gy++)
        {
            double y = gy * (double)_gridSize; int c = gy == 0 ? axis : col;
            verts.Add(FV(new Vec2D(left, y), c)); verts.Add(FV(new Vec2D(right, y), c));
        }

        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetBufferData(_gridVb, verts.ToArray());
        _gridLineCount = verts.Count / 2;
        _device.SetVertexBuffer(_gridVb);
        _device.Draw(DBPrimitiveType.LineList, 0, _gridLineCount);
    }

    // Draws the 128-unit blockmap grid over the map (occupied blocks brighter), as a debug overlay.
    private void DrawBlockmap()
    {
        _blockmapLineCount = 0;
        if (!_showBlockmap || _device is null || _blockmapVb is null || _map is null || _map.Vertices.Count == 0) return;

        _blockmapCache ??= new DBuilder.Map.BlockMap(_map);
        var bm = _blockmapCache;
        double bs = bm.BlockSize;
        if (bs / _zoom < 3) return; // too dense to be useful

        // Visible block range (clamped to the blockmap extents).
        double halfW = Bounds.Width * 0.5 * _zoom, halfH = Bounds.Height * 0.5 * _zoom;
        int c0 = Math.Max(0, (int)Math.Floor((_camX - halfW - bm.OriginX) / bs));
        int c1 = Math.Min(bm.Columns, (int)Math.Ceiling((_camX + halfW - bm.OriginX) / bs));
        int r0 = Math.Max(0, (int)Math.Floor((_camY - halfH - bm.OriginY) / bs));
        int r1 = Math.Min(bm.Rows, (int)Math.Ceiling((_camY + halfH - bm.OriginY) / bs));
        if (c1 <= c0 || r1 <= r0) return;

        // Note: the renderer swaps R<->B, so these read blue-ish on screen (consistent with the app palette).
        const int grid = unchecked((int)0xff403028);      // dim grid for empty blocks (kept subtle)
        const int occupied = unchecked((int)0xffffc040);  // bright highlight for blocks containing linedefs
        var verts = new System.Collections.Generic.List<FlatVertex>();

        double x0 = bm.OriginX + c0 * bs, x1 = bm.OriginX + c1 * bs;
        double y0 = bm.OriginY + r0 * bs, y1 = bm.OriginY + r1 * bs;
        for (int c = c0; c <= c1; c++) { double x = bm.OriginX + c * bs; verts.Add(FV(new Vec2D(x, y0), grid)); verts.Add(FV(new Vec2D(x, y1), grid)); }
        for (int r = r0; r <= r1; r++) { double y = bm.OriginY + r * bs; verts.Add(FV(new Vec2D(x0, y), grid)); verts.Add(FV(new Vec2D(x1, y), grid)); }

        // Outline blocks that contain linedefs.
        for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                if (bm.LinedefCountAt(c, r) == 0) continue;
                double bx0 = bm.OriginX + c * bs, by0 = bm.OriginY + r * bs, bx1 = bx0 + bs, by1 = by0 + bs;
                var p00 = new Vec2D(bx0, by0); var p10 = new Vec2D(bx1, by0);
                var p11 = new Vec2D(bx1, by1); var p01 = new Vec2D(bx0, by1);
                verts.Add(FV(p00, occupied)); verts.Add(FV(p10, occupied));
                verts.Add(FV(p10, occupied)); verts.Add(FV(p11, occupied));
                verts.Add(FV(p11, occupied)); verts.Add(FV(p01, occupied));
                verts.Add(FV(p01, occupied)); verts.Add(FV(p00, occupied));
            }

        if (verts.Count == 0) return;
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetBufferData(_blockmapVb, verts.ToArray());
        _blockmapLineCount = verts.Count / 2;
        _device.SetVertexBuffer(_blockmapVb);
        _device.Draw(DBPrimitiveType.LineList, 0, _blockmapLineCount);
    }

    // Draws the BSP node partition lines (set via SetNodeLines) as a green overlay.
    private void DrawNodes()
    {
        if (!_showNodes || _device is null || _nodesVb is null || _nodeLines.Length == 0) return;

        if (_nodesDirty)
        {
            var verts = new System.Collections.Generic.List<FlatVertex>(_nodeLines.Length * 2);
            const int col = unchecked((int)0xff40ff80); // green (R<->B swap -> greenish on screen)
            foreach (var (a, b) in _nodeLines) { verts.Add(FV(a, col)); verts.Add(FV(b, col)); }
            _device.SetBufferData(_nodesVb, verts.ToArray());
            _nodesLineCount = verts.Count / 2;
            _nodesDirty = false;
        }
        if (_nodesLineCount == 0) return;

        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetVertexBuffer(_nodesVb);
        _device.Draw(DBPrimitiveType.LineList, 0, _nodesLineCount);
    }

    // Projects a world point onto a linedef's segment (clamped to its endpoints).
    private static Vec2D NearestPointOnLine(Linedef l, Vec2D p)
    {
        var a = l.Start.Position;
        var b = l.End.Position;
        double u = Math.Clamp(Line2D.GetNearestOnLine(a, b, p), 0.0, 1.0);
        return a + u * (b - a);
    }

    private bool _selectionDoneOnPress;

    private static bool IsFlyKey(Key k) => k is Key.W or Key.A or Key.S or Key.D or Key.Q or Key.E
        or Key.Up or Key.Down or Key.Left or Key.Right;

    private static bool IsArrow(Key k) => k is Key.Left or Key.Right or Key.Up or Key.Down;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            _mode3D = !_mode3D;
            if (_mode3D)
            {
                if (!_cam3DInit) { Reset3DCamera(); _cam3DInit = true; }
                _lastTime = _clock.Elapsed.TotalSeconds;
            }
            else _sel3D.Clear(); // drop the 3D selection when returning to 2D
            e.Handled = true;
            RequestNextFrameRendering();
            return;
        }
        if (_mode3D && e.Key == Key.G) { _walkMode = !_walkMode; e.Handled = true; RequestNextFrameRendering(); return; }
        // 3D brightness on the targeted sector: [ darker, ] brighter.
        if (_mode3D && e.Key == Key.OemOpenBrackets) { AdjustTargetBrightness3D(-8); e.Handled = true; return; }
        if (_mode3D && e.Key == Key.OemCloseBrackets) { AdjustTargetBrightness3D(8); e.Handled = true; return; }
        // 3D texture copy/apply (plain C/V; Ctrl/Cmd+C/V stay 2D selection copy/paste at the window level).
        bool mod = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (_mode3D && !mod && e.Key == Key.C) { CopyTexture3D(); e.Handled = true; return; }
        if (_mode3D && !mod && e.Key == Key.V) { ApplyTexture3D(); e.Handled = true; return; }
        if (_mode3D && !mod && e.Key == Key.A) { AutoAlignTarget3D(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled = true; return; }
        if (_mode3D && e.Key == Key.Enter) { OpenTargetDialog3D(); e.Handled = true; return; }
        if (_mode3D && e.Key == Key.Escape) { ClearSelection3D(); e.Handled = true; return; }
        if (_mode3D && !mod && e.Key == Key.O) { ResetTargetOffsets3D(); e.Handled = true; return; }
        if (_mode3D && (e.Key == Key.Delete || e.Key == Key.Back)) { DeleteTargetThing3D(); e.Handled = true; return; }
        // Open the texture/flat browser for the current target (flats for floor/ceiling, textures for walls).
        if (_mode3D && !mod && e.Key == Key.T && _target3D != null)
        {
            BrowseTexturesRequested?.Invoke(_target3D.Kind != VisualHitKind.Wall);
            e.Handled = true;
            return;
        }
        // Shift+arrows nudge the targeted wall's texture offset (plain arrows remain camera look).
        if (_mode3D && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && IsArrow(e.Key)) { NudgeTargetOffset3D(e.Key); e.Handled = true; return; }
        if (_mode3D && IsFlyKey(e.Key)) { _heldKeys.Add(e.Key); e.Handled = true; return; }

        // Let Ctrl/Cmd shortcuts (undo/redo/save) bubble up to the window instead of triggering view toggles.
        bool accel = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!_mode3D && !accel)
        {
            switch (e.Key)
            {
                case Key.S: _showFills = !_showFills; e.Handled = true; RequestNextFrameRendering(); return;
                case Key.T: _showThings = !_showThings; e.Handled = true; RequestNextFrameRendering(); return;
                case Key.Y: ThingArrows = !ThingArrows; e.Handled = true; return; // sprites <-> arrows
                case Key.D: ToggleDrawMode(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled = true; return;
                case Key.M: MakeSectorAtCursor(); e.Handled = true; return;
                case Key.I or Key.Insert: InsertAtCursor(); e.Handled = true; return;
                case Key.D1 or Key.NumPad1: SetEditMode(EditMode.Vertices); e.Handled = true; return;
                case Key.D2 or Key.NumPad2: SetEditMode(EditMode.Linedefs); e.Handled = true; return;
                case Key.D3 or Key.NumPad3: SetEditMode(EditMode.Sectors); e.Handled = true; return;
                case Key.D4 or Key.NumPad4: SetEditMode(EditMode.Things); e.Handled = true; return;
                case Key.F: FlipSelected(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled = true; return;
                case Key.A: AutoAlignSelected(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled = true; return;
                case Key.G: _snapToGrid = !_snapToGrid; Picked?.Invoke($"snap {(_snapToGrid ? "on" : "off")} (grid {_gridSize})"); e.Handled = true; return;
                case Key.OemOpenBrackets: _gridSize = Math.Max(8, _gridSize / 2); Picked?.Invoke($"grid {_gridSize}"); MarkGeometryDirty(); e.Handled = true; return;
                case Key.OemCloseBrackets: _gridSize = Math.Min(1024, _gridSize * 2); Picked?.Invoke($"grid {_gridSize}"); MarkGeometryDirty(); e.Handled = true; return;
                case Key.Enter when _drawMode: FinishDraw(); e.Handled = true; return;
                case Key.Escape when InDrawMode: ExitDrawModes(); e.Handled = true; return;
                case Key.R: FitToMap(); MarkGeometryDirty(); e.Handled = true; return;
                case Key.OemPlus or Key.Add: ZoomBy(0.8); e.Handled = true; return;     // zoom in
                case Key.OemMinus or Key.Subtract: ZoomBy(1.25); e.Handled = true; return; // zoom out
            }
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (_heldKeys.Remove(e.Key)) e.Handled = true;
        else base.OnKeyUp(e);
    }

    private void Reset3DCamera()
    {
        if (_map != null)
        {
            var (minX, minY, maxX, maxY) = _map.Bounds();
            _cam3DPos = new Vector3((float)((minX + maxX) * 0.5), (float)((minY + maxY) * 0.5), 200f);
        }
        else _cam3DPos = new Vector3(0, 0, 200f);
        _yaw = 0; _pitch = -0.3;
    }

    private Vector3 Cam3DForward()
    {
        float cp = (float)Math.Cos(_pitch);
        return new Vector3(cp * (float)Math.Cos(_yaw), cp * (float)Math.Sin(_yaw), (float)Math.Sin(_pitch));
    }

    private void UpdateFlyCamera()
    {
        double now = _clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(0.05, now - _lastTime); // clamp to avoid jumps after a stall
        _lastTime = now;

        float move = dt * 320f;
        float look = dt * 1.6f;
        if (_heldKeys.Contains(Key.Left)) _yaw += look;
        if (_heldKeys.Contains(Key.Right)) _yaw -= look;
        if (_heldKeys.Contains(Key.Up)) _pitch += look;
        if (_heldKeys.Contains(Key.Down)) _pitch -= look;
        _pitch = Math.Clamp(_pitch, -1.5, 1.5);

        var flatFwd = new Vector3((float)Math.Cos(_yaw), (float)Math.Sin(_yaw), 0);
        var right = new Vector3((float)Math.Sin(_yaw), -(float)Math.Cos(_yaw), 0);
        if (_heldKeys.Contains(Key.W)) _cam3DPos += flatFwd * move;
        if (_heldKeys.Contains(Key.S)) _cam3DPos -= flatFwd * move;
        if (_heldKeys.Contains(Key.D)) _cam3DPos += right * move;
        if (_heldKeys.Contains(Key.A)) _cam3DPos -= right * move;

        if (_walkMode)
        {
            // Stand on the floor of the sector under the camera, at eye height (no free vertical movement).
            var pos = new Vec2D(_cam3DPos.X, _cam3DPos.Y);
            var sector = _blockmapCache?.GetSectorAt(pos) ?? _map?.GetSectorAt(pos);
            if (sector != null)
                _cam3DPos.Z = (float)(sector.GetFloorZ(pos) + EyeHeight);
        }
        else
        {
            if (_heldKeys.Contains(Key.E)) _cam3DPos.Z += move;
            if (_heldKeys.Contains(Key.Q)) _cam3DPos.Z -= move;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(); // take keyboard focus so the 3D fly camera receives WASD/arrows
        var pt = e.GetCurrentPoint(this);
        if (_mode3D)
        {
            // Left-drag is mouse-look (a left click without dragging toggles selection); right-drag edits height.
            if (pt.Properties.IsLeftButtonPressed) { _look3D = true; _lookMoved = false; _lastPointer = pt.Position; }
            else if (pt.Properties.IsRightButtonPressed && _target3D is { } h && HeightEditLabel(h) != null)
            {
                _drag3DTarget = h; _drag3DAccum = 0; _lastPointer = pt.Position;
                EditBegun?.Invoke(h.Kind == VisualHitKind.Thing ? "Move thing" : HeightEditLabel(h)!);
            }
            return;
        }

        // Draw mode: left-click places loop points (or closes); a drag still pans. Right-click cancels.
        if (_drawMode)
        {
            if (pt.Properties.IsRightButtonPressed) { CancelDraw(); return; }
            if (pt.Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount >= 2) { FinishDraw(); return; }
                _pressed = true; _drag = DragKind.None;
                _dragStart = pt.Position; _lastPointer = pt.Position;
            }
            return;
        }

        // Right button: a drag pans, a click (no drag) splits the nearest line. Decide on release.
        if (pt.Properties.IsRightButtonPressed)
        {
            _rightPressed = true; _rightDragging = false;
            _dragStart = pt.Position; _lastPointer = pt.Position;
            return;
        }

        if (pt.Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount >= 2)
            {
                // Double-click: select exactly one element under the cursor and request a property edit.
                SelectSingleAt(ToWorld(pt.Position));
                EditRequested?.Invoke();
                _pressed = false;
                return;
            }
            _pressed = true;
            _drag = DragKind.None;
            _dragStart = pt.Position;
            _lastPointer = pt.Position;
            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            _boxAdditive = shift;
            // Select a vertex/thing immediately on press so a single press-drag moves it.
            _selectionDoneOnPress = SelectPointElementAt(ToWorld(pt.Position), shift);
            _moveCandidate = _selectionDoneOnPress;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_mode3D)
        {
            if (e.InitialPressMouseButton == MouseButton.Left && _look3D && !_lookMoved) ToggleSelection3D();
            _look3D = false; _drag3DTarget = null;
            return;
        }
        var pos = e.GetCurrentPoint(this).Position;

        // Right button up: split the nearest line if it was a click, otherwise it was a pan (do nothing).
        if (e.InitialPressMouseButton == MouseButton.Right && _rightPressed)
        {
            bool wasDrag = _rightDragging;
            _rightPressed = false; _rightDragging = false;
            if (!wasDrag && !_drawMode && _map != null)
            {
                var world = ToWorld(pos);
                var l = _map.NearestLinedef(world, 8 * _zoom);
                if (l != null)
                {
                    EditBegun?.Invoke("Split linedef");
                    _map.SplitLinedef(l, NearestPointOnLine(l, world));
                    _map.BuildIndexes();
                    MarkGeometryDirty();
                    Changed?.Invoke();
                }
            }
            return;
        }

        if (!_pressed) return;
        _pressed = false;

        if (_drawMode)
        {
            if (_drag == DragKind.None) PlaceDrawPoint(ToWorld(pos)); // a click adds/closes a loop point
            _drag = DragKind.None;
            return;
        }

        if (_drag == DragKind.None)
        {
            // A click. Point elements were already handled on press; otherwise pick a line/sector (or clear).
            if (!_selectionDoneOnPress)
                Pick(ToWorld(pos), additive: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
        else if (_drag == DragKind.Move)
        {
            MergeDraggedVertices(); // snap+merge dragged vertices dropped onto stationary ones
            Changed?.Invoke();
        }
        else if (_drag == DragKind.Box)
        {
            if (_shapeKind != ShapeKind.None) CreateShapeFromBox(_boxStartWorld, ToWorld(pos));
            else ApplyBoxSelection(_boxStartWorld, ToWorld(pos), _boxAdditive);
        }
        _drag = DragKind.None;
        _moveVerts = null;
    }

    // Builds a rectangle/ellipse sector inscribed in the dragged box (corners snapped to grid), undoable.
    private void CreateShapeFromBox(Vec2D a, Vec2D b)
    {
        if (_map == null) return;
        var p0 = SnapToGrid(a);
        var p1 = SnapToGrid(b);
        var loop = _shapeKind == ShapeKind.Rectangle
            ? ShapeGenerator.Rectangle(p0, p1)
            : ShapeGenerator.Ellipse(p0, p1, _shapeSides);
        if (loop.Count < 3) return;

        EditBegun?.Invoke(_shapeKind == ShapeKind.Rectangle ? "Draw rectangle" : "Draw ellipse");
        var verts = new System.Collections.Generic.List<Vertex>(loop.Count);
        foreach (var p in loop)
        {
            var existing = _map.NearestVertex(p, 0.01);
            verts.Add(existing ?? _map.AddVertex(p));
        }
        SectorBuilder.CreateSector(_map, verts);
        _map.MergeOverlappingVertices(0.01);
        _map.SplitLinedefsAtVertices(0.5);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
    }

    // Selects all elements of the active mode whose geometry falls inside the rubber-band box.
    private void ApplyBoxSelection(Vec2D a, Vec2D b, bool additive)
    {
        if (_map == null) return;
        double minX = Math.Min(a.x, b.x), maxX = Math.Max(a.x, b.x);
        double minY = Math.Min(a.y, b.y), maxY = Math.Max(a.y, b.y);
        if (!additive) _map.ClearAllSelected();

        int n = 0;
        switch (_editMode)
        {
            case EditMode.Vertices:
                foreach (var v in _map.GetVerticesInBox(minX, minY, maxX, maxY)) { v.Selected = true; n++; }
                break;
            case EditMode.Things:
                foreach (var t in _map.GetThingsInBox(minX, minY, maxX, maxY))
                {
                    if (ThingHidden2D(t)) continue;
                    t.Selected = true;
                    n++;
                }
                break;
            case EditMode.Sectors:
                foreach (var s in _map.GetSectorsInBox(minX, minY, maxX, maxY)) { s.Selected = true; n++; }
                break;
            default:
                foreach (var l in _map.GetLinedefsInBox(minX, minY, maxX, maxY)) { l.Selected = true; n++; }
                break;
        }
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke($"box-selected {n} {_editMode}");
    }

    // After a vertex drag, merge any selected vertex dropped within snap range of a stationary (unselected)
    // vertex into that target. Part of the same gesture, so the move's undo snapshot already covers it.
    private void MergeDraggedVertices()
    {
        if (_map == null) return;
        double r2 = (8 * _zoom) * (8 * _zoom);
        bool merged = false;
        foreach (var v in _map.GetSelectedVertices())
        {
            Vertex? target = null;
            double best = r2;
            foreach (var o in _map.Vertices)
            {
                if (o.Selected || ReferenceEquals(o, v)) continue;
                double dx = o.Position.x - v.Position.x, dy = o.Position.y - v.Position.y;
                double d = dx * dx + dy * dy;
                if (d <= best) { best = d; target = o; }
            }
            if (target != null)
            {
                v.Position = target.Position; // snap exactly onto the target
                _map.JoinVertices(target, v);
                merged = true;
            }
        }
        if (merged)
        {
            _map.BuildIndexes();
            MarkGeometryDirty();
        }
    }

    // Process-wide clipboard buffer for copy/paste of a selection.
    private static byte[]? _clipboard;

    /// <summary>Copies the current selection (with its dependency closure) to the clipboard.</summary>
    public void CopySelection()
    {
        if (_map == null) return;
        var buf = SelectionClipboard.CopySelection(_map);
        if (buf == null) { Picked?.Invoke("nothing selected to copy"); return; }
        _clipboard = buf;
        Picked?.Invoke("copied selection");
    }

    /// <summary>Pastes the clipboard one grid cell from the originals and selects the result (undoable).</summary>
    public void PasteClipboard()
    {
        if (_map == null || _clipboard == null) { Picked?.Invoke("clipboard empty"); return; }
        EditBegun?.Invoke("Paste");
        var res = SelectionClipboard.Paste(_map, _clipboard, new Vec2D(_gridSize, _gridSize));
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke($"pasted {res.LinedefCount} lines, {res.SectorCount} sectors, {res.ThingCount} things");
    }

    /// <summary>Serializes the current selection for saving as a prefab, or null when nothing is selected.</summary>
    public byte[]? GetSelectionPrefab() => _map == null ? null : SelectionClipboard.CopySelection(_map);

    /// <summary>Inserts a prefab buffer with its lower-left corner anchored at the grid-snapped cursor (undoable).</summary>
    public void InsertPrefab(byte[] data)
    {
        if (_map == null) return;
        EditBegun?.Invoke("Insert prefab");
        var res = SelectionClipboard.PasteAtAnchor(_map, data, SnapToGrid(_cursorWorld));
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke($"inserted prefab: {res.LinedefCount} lines, {res.SectorCount} sectors, {res.ThingCount} things");
    }

    // Auto-aligns textures along the wall run from the first selected linedef's front side (A = X, Shift+A = Y), undoable.
    private void AutoAlignSelected(bool vertical)
    {
        if (_map == null) return;
        Linedef? start = null;
        foreach (var l in _map.Linedefs) if (l.Selected && l.Front != null) { start = l; break; }
        if (start?.Front == null) { Picked?.Invoke("select a linedef with a front sidedef to align"); return; }

        string tex = SidedefTextureAlignment.PrimaryTexture(start.Front);
        var img = _resources?.GetWallTexture(tex);

        EditBegun?.Invoke(vertical ? "Auto-align textures (Y)" : "Auto-align textures (X)");
        int n = vertical
            ? SidedefTextureAlignment.AutoAlignY(start.Front, img?.Height ?? 0)
            : SidedefTextureAlignment.AutoAlignX(start.Front, img?.Width ?? 0);
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke($"aligned {n} sidedef{(n == 1 ? "" : "s")} {(vertical ? "vertically" : "horizontally")} (tex {tex})");
    }

    // Flips selected linedefs (F = reverse direction, Shift+F = swap front/back sidedefs), undoable.
    private void FlipSelected(bool sidedefs)
    {
        if (_map == null || _map.SelectedLinedefsCount == 0) { Picked?.Invoke("no linedefs selected"); return; }
        EditBegun?.Invoke(sidedefs ? "Flip sidedefs" : "Flip linedefs");
        int n = sidedefs ? _map.FlipSelectedSidedefs() : _map.FlipSelectedLinedefs();
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke($"flipped {n} {(sidedefs ? "sidedef" : "linedef")}{(n == 1 ? "" : "s")}");
    }

    /// <summary>The thing type used by the insert tool; remembers the last value edited via the dialog.</summary>
    public int InsertThingType { get; set; } = 1;

    // Insert tool (I): in Things mode drops a thing at the snapped cursor; otherwise inserts a vertex,
    // splitting the nearest line if the cursor is close to one, else placing a free vertex. Undoable.
    private void InsertAtCursor()
    {
        if (_map == null) return;
        var pos = SnapToGrid(_cursorWorld);

        if (_editMode == EditMode.Things)
        {
            EditBegun?.Invoke("Insert thing");
            _map.ClearAllSelected();
            var t = _map.AddThing(pos, InsertThingType);
            t.Selected = true;
            _map.BuildIndexes();
            MarkGeometryDirty();
            Changed?.Invoke();
            Picked?.Invoke($"inserted thing type {InsertThingType} at ({pos.x:0}, {pos.y:0})");
            return;
        }

        var line = _map.NearestLinedef(_cursorWorld, 8 * _zoom);
        if (line != null)
        {
            EditBegun?.Invoke("Insert vertex (split)");
            _map.SplitLinedef(line, NearestPointOnLine(line, _cursorWorld));
            _map.BuildIndexes();
            MarkGeometryDirty();
            Changed?.Invoke();
            Picked?.Invoke("split linedef");
        }
        else
        {
            EditBegun?.Invoke("Insert vertex");
            _map.ClearAllSelected();
            var v = _map.AddVertex(pos);
            v.Selected = true;
            _map.BuildIndexes();
            MarkGeometryDirty();
            Changed?.Invoke();
            Picked?.Invoke($"inserted vertex at ({pos.x:0}, {pos.y:0})");
        }
    }

    // Traces the line loop enclosing the cursor and creates a sector from it (undoable).
    private void MakeSectorAtCursor()
    {
        if (_map == null || _map.Linedefs.Count == 0) return;
        var path = Tools.FindPotentialSectorAt(_map, _cursorWorld); // hole-aware
        if (path == null || path.Count < 3) { Picked?.Invoke("no enclosing loop here"); return; }

        EditBegun?.Invoke("Make sector");
        SectorBuilder.CreateSectorFromSides(_map, path);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke($"made sector from {path.Count} lines");
    }

    // ---- Draw-geometry tool ----

    /// <summary>True when the draw-geometry tool is active (host can reflect it in the status bar).</summary>
    public bool DrawMode => _drawMode;
    public event Action? DrawModeChanged;

    private void ToggleDrawMode(bool linesOnly = false)
    {
        // Re-pressing the same draw key exits; switching kind restarts with the new kind.
        if (_drawMode && _drawLinesOnly == linesOnly) _drawMode = false;
        else { _drawMode = true; _drawLinesOnly = linesOnly; _shapeKind = ShapeKind.None; } // draw and shape are exclusive
        _drawPoints.Clear();
        _drawClosed = false;
        _drawDirty = true;
        DrawModeChanged?.Invoke();
        RequestNextFrameRendering();
    }

    /// <summary>True while any draw tool (polyline or shape) is active.</summary>
    public bool InDrawMode => _drawMode || _shapeKind != ShapeKind.None;

    /// <summary>Fully exits the polyline and shape draw tools and clears any in-progress preview.</summary>
    public void ExitDrawModes()
    {
        bool was = InDrawMode;
        _drawMode = false;
        _shapeKind = ShapeKind.None;
        _drawPoints.Clear();
        _drawClosed = false;
        _drawDirty = true;
        _drawLineCount = 0; // drop the green preview immediately
        if (was) DrawModeChanged?.Invoke();
        RequestNextFrameRendering();
    }

    private void CancelDraw()
    {
        _drawPoints.Clear();
        _drawClosed = false;
        _drawDirty = true;
        RequestNextFrameRendering();
    }

    // Snaps a world point to a nearby existing vertex or the first draw point (for closing the loop).
    private Vec2D SnapWorld(Vec2D world)
    {
        double r2 = (10 * _zoom) * (10 * _zoom);
        if (_drawPoints.Count >= 3)
        {
            var f = _drawPoints[0];
            double dx0 = f.x - world.x, dy0 = f.y - world.y;
            if (dx0 * dx0 + dy0 * dy0 <= r2) return f;
        }
        if (_map != null)
        {
            var v = _map.NearestVertex(world, 10 * _zoom);
            if (v != null) return v.Position;
        }
        return SnapToGrid(world);
    }

    private void PlaceDrawPoint(Vec2D world)
    {
        var p = SnapWorld(world);
        // Clicking the first point (when we have a polygon) closes the loop.
        if (_drawPoints.Count >= 3 && p.x == _drawPoints[0].x && p.y == _drawPoints[0].y)
        {
            _drawClosed = true;
            FinishDraw();
            return;
        }
        _drawPoints.Add(p);
        _drawDirty = true;
        RequestNextFrameRendering();
    }

    private void FinishDraw()
    {
        if (_map == null) { CancelDraw(); return; }
        int min = _drawLinesOnly ? 2 : 3;
        if (_drawPoints.Count < min) { CancelDraw(); return; }

        // Materialize the drawn points as vertices, reusing any that snapped exactly onto existing ones.
        var verts = new System.Collections.Generic.List<Vertex>(_drawPoints.Count);
        EditBegun?.Invoke(_drawLinesOnly ? "Draw lines" : "Draw sector");
        foreach (var p in _drawPoints)
        {
            var existing = _map.NearestVertex(p, 0.01);
            verts.Add(existing ?? _map.AddVertex(p));
        }

        if (_drawLinesOnly)
        {
            for (int i = 0; i < verts.Count - 1; i++) _map.AddLinedef(verts[i], verts[i + 1]);
            if (_drawClosed && verts.Count >= 3) _map.AddLinedef(verts[^1], verts[0]);
        }
        else
        {
            SectorBuilder.CreateSector(_map, verts);
        }

        _map.MergeOverlappingVertices(0.01);
        _map.SplitLinedefsAtVertices(0.5); // weld drawn vertices that landed on existing walls (T-junctions)
        _map.BuildIndexes();

        _drawPoints.Clear();
        _drawClosed = false;
        _drawDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
    }

    // Builds the in-progress draw overlay: placed-point segments + a preview segment to the cursor.
    private void RebuildDrawPreview()
    {
        _drawLineCount = 0;
        if (_device is null || _drawVb is null || !_drawMode || _drawPoints.Count == 0) return;

        const int col = unchecked((int)0xff40ff80);   // bright green polyline
        const int preview = unchecked((int)0xff80ff40);
        var verts = new System.Collections.Generic.List<FlatVertex>();
        for (int i = 0; i < _drawPoints.Count - 1; i++)
        {
            verts.Add(FV(_drawPoints[i], col));
            verts.Add(FV(_drawPoints[i + 1], col));
        }
        // Preview segment from the last placed point to the (snapped) cursor.
        verts.Add(FV(_drawPoints[^1], preview));
        verts.Add(FV(_drawCursor, preview));

        _device.SetBufferData(_drawVb, verts.ToArray());
        _drawLineCount = verts.Count / 2;
    }

    // Selects the nearest vertex/thing under the cursor on press. Returns true if one ended up selected
    // (so a subsequent drag should move it). Non-additive clicks replace the selection only when the hit
    // element wasn't already selected, preserving an existing multi-selection for dragging.
    private bool SelectPointElementAt(Vec2D world, bool additive)
    {
        if (_map == null) return false;

        if (_editMode == EditMode.Vertices)
        {
            var v = _map.NearestVertex(world, 10 * _zoom);
            if (v != null)
            {
                if (additive) v.Selected = !v.Selected;
                else if (!v.Selected) { _map.ClearAllSelected(); v.Selected = true; }
                MarkGeometryDirty();
                Picked?.Invoke($"vertex ({v.Position.x:0.#}, {v.Position.y:0.#})");
                return v.Selected;
            }
        }
        else if (_editMode == EditMode.Things)
        {
            var t = _map.NearestThing(world, 12 * _zoom);
            if (t != null)
            {
                if (additive) t.Selected = !t.Selected;
                else if (!t.Selected) { _map.ClearAllSelected(); t.Selected = true; }
                MarkGeometryDirty();
                Picked?.Invoke($"thing type {t.Type} ({t.Position.x:0.#}, {t.Position.y:0.#})");
                return t.Selected;
            }
        }

        // Linedef/Sector modes select on release; a press on empty space (point modes) selects nothing.
        return false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetCurrentPoint(this).Position;

        // 3D: left-drag rotates the camera; right-drag changes the captured surface/thing height.
        if (_mode3D)
        {
            if (_look3D)
            {
                if (Math.Abs(pos.X - _lastPointer.X) + Math.Abs(pos.Y - _lastPointer.Y) > 2) _lookMoved = true;
                const double sens = 0.005;
                _yaw -= (pos.X - _lastPointer.X) * sens;
                _pitch = Math.Clamp(_pitch - (pos.Y - _lastPointer.Y) * sens, -1.5, 1.5);
                _lastPointer = pos;
                RequestNextFrameRendering();
            }
            else if (_drag3DTarget != null)
            {
                Drag3D(pos.X - _lastPointer.X, pos.Y - _lastPointer.Y);
                _lastPointer = pos;
            }
            return;
        }

        _cursorWorld = ToWorld(pos);
        CursorWorldMoved?.Invoke(_cursorWorld);

        if (_drawMode)
        {
            _drawCursor = SnapWorld(ToWorld(pos));
            _drawDirty = true; // GL buffer rebuilt on the render thread
            RequestNextFrameRendering();
        }

        // Right-drag pans the view (decided once the cursor moves past the click threshold).
        if (_rightPressed)
        {
            if (!_rightDragging && Math.Abs(pos.X - _dragStart.X) + Math.Abs(pos.Y - _dragStart.Y) < 4) return;
            _rightDragging = true;
            _camX -= (pos.X - _lastPointer.X) * _zoom;
            _camY += (pos.Y - _lastPointer.Y) * _zoom;
            _lastPointer = pos;
            RequestNextFrameRendering();
            return;
        }

        if (!_pressed) return;

        if (_drag == DragKind.None)
        {
            double moved = Math.Abs(pos.X - _dragStart.X) + Math.Abs(pos.Y - _dragStart.Y);
            if (moved < 4) return;
            // Draw mode pans; a press on a vertex/thing moves it; otherwise rubber-band box select.
            _drag = _shapeKind != ShapeKind.None ? DragKind.Box
                  : _drawMode ? DragKind.Pan
                  : (_moveCandidate ? DragKind.Move : DragKind.Box);
            if (_drag == DragKind.Move)
            {
                EditBegun?.Invoke("Move selection");
                // Capture the vertices implied by the selection once, so dragging a selected line or sector
                // moves its geometry (not just directly-selected vertices).
                _moveVerts = _map?.SelectedGeometryVertices();
            }
            else if (_drag == DragKind.Box) { _boxStartWorld = ToWorld(_dragStart); _boxCurWorld = ToWorld(pos); }
        }

        if (_drag == DragKind.Pan)
        {
            _camX -= (pos.X - _lastPointer.X) * _zoom;
            _camY += (pos.Y - _lastPointer.Y) * _zoom;
            RequestNextFrameRendering();
        }
        else if (_drag == DragKind.Box)
        {
            _boxCurWorld = ToWorld(pos);
            RequestNextFrameRendering();
        }
        else if (_drag == DragKind.Move && _map != null)
        {
            // With snapping, step by grid increments: diff the snapped previous/current pointer so the
            // selection only advances when the cursor crosses a grid cell boundary.
            var delta = _snapToGrid
                ? SnapToGrid(ToWorld(pos)) - SnapToGrid(ToWorld(_lastPointer))
                : ToWorld(pos) - ToWorld(_lastPointer);
            if (delta.x != 0 || delta.y != 0)
            {
                if (_moveVerts != null) foreach (var v in _moveVerts) v.Position += delta;
                _map.MoveSelectedThingsBy(delta);
                MarkGeometryDirty();
                Changed?.Invoke();
            }
        }
        _lastPointer = pos;
    }


    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // Trackpads report scroll on either axis; use whichever has the larger magnitude.
        double delta = Math.Abs(e.Delta.Y) >= Math.Abs(e.Delta.X) ? e.Delta.Y : e.Delta.X;
        if (delta == 0) return;

        // In 3D, the wheel raises/lowers the targeted floor/ceiling (Shift = fine 1-unit step).
        if (_mode3D)
        {
            int step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1 : 8;
            AdjustTarget3D(delta > 0 ? step : -step);
            e.Handled = true;
            return;
        }

        ZoomBy(delta > 0 ? 0.85 : 1.0 / 0.85);
        e.Handled = true;
    }

    private void ZoomBy(double factor)
    {
        _zoom = Math.Clamp(_zoom * factor, 0.02, 200);
        RequestNextFrameRendering();
    }

    // Clears the selection and selects the single nearest element (vertex -> thing -> linedef -> sector).
    private void SelectSingleAt(Vec2D world)
    {
        if (_map == null) return;
        _map.ClearAllSelected();
        switch (_editMode)
        {
            case EditMode.Vertices:
                if (_map.NearestVertex(world, 10 * _zoom) is { } v) v.Selected = true;
                break;
            case EditMode.Things:
                if (NearestVisibleThing(world, 12 * _zoom) is { } t) t.Selected = true;
                break;
            case EditMode.Sectors:
                if (_map.GetSectorAt(world) is { } s) s.Selected = true;
                break;
            default:
                if (_map.NearestLinedef(world, 8 * _zoom) is { } l) l.Selected = true;
                break;
        }
        MarkGeometryDirty();
        Changed?.Invoke();
    }

    private Thing? NearestVisibleThing(Vec2D pos, double maxRange)
    {
        if (_map == null) return null;

        Thing? closest = null;
        double bestSq = maxRange * maxRange;
        foreach (var t in _map.Things)
        {
            if (ThingHidden2D(t)) continue;
            double dx = t.Position.x - pos.x;
            double dy = t.Position.y - pos.y;
            double d = dx * dx + dy * dy;
            if (d < bestSq) { bestSq = d; closest = t; }
        }
        return closest;
    }

    private void Pick(Vec2D world, bool additive)
    {
        if (_map == null) return;
        if (!additive) _map.ClearAllSelected();

        // Vertices/things are handled on press; here Linedefs/Sectors modes pick their element (else just clear).
        string desc = "nothing";
        if (_editMode == EditMode.Linedefs)
        {
            var l = _map.NearestLinedef(world, 8 * _zoom);
            if (l != null) { l.Selected = !l.Selected; desc = $"linedef {_map.Linedefs.IndexOf(l)}"; }
        }
        else if (_editMode == EditMode.Sectors)
        {
            var s = _map.GetSectorAt(world);
            if (s != null) { s.Selected = !s.Selected; desc = $"sector {s.Index}"; }
        }
        MarkGeometryDirty();
        Picked?.Invoke(desc);
    }
}
