// ABOUTME: Avalonia OpenGlControlBase that renders a MapSet's 2D line overlay via the DBuilder.Rendering stack.
// ABOUTME: Bridges Avalonia's GL context to Silk.NET so RenderDevice/Shader/VertexBuffer work unchanged; supports pan/zoom and click-pick.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Avalonia.Threading;
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
using DBIndexBuffer = DBuilder.Rendering.IndexBuffer;
using Vec2D = DBuilder.Geometry.Vector2D;
using AvaloniaContextMenu = Avalonia.Controls.ContextMenu;
using AvaloniaControl = Avalonia.Controls.Control;
using AvaloniaMenuItem = Avalonia.Controls.MenuItem;
using AvaloniaSeparator = Avalonia.Controls.Separator;

namespace DBuilder.Editor;

public class MapControl : OpenGlControlBase, ICustomHitTest
{
    private static readonly int ThreeDFloorLineColor = new ColorCollection().ThreeDFloor.ToArgb();
    private const double AutoPanBorderSize = 100.0;
    private const double AutoPanScale = 0.0001;

    public enum ClassicViewMode
    {
        Wireframe = 0,
        Brightness = 1,
        FloorTextures = 2,
        CeilingTextures = 3,
    }

    public enum VisualSlopePickingMode
    {
        Default,
        SidedefSlopeHandles,
        VertexSlopeHandles,
    }

    public IReadOnlyList<EditorShortcutBinding> ShortcutBindings { get; set; } = EditorCommandCatalog.DefaultShortcuts;
    public PasteOptions PasteOptions { get; set; } = new();
    public event Action? ActionStateChanged;
    public event Action? VisplaneExplorerRequested;
    private readonly PastePropertiesClipboard _pastePropertiesClipboard = new();

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
    private readonly System.Collections.Generic.Dictionary<string, DBTexture?> _modelTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Generic.Dictionary<string, DBTexture?> _voxelTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Generic.Dictionary<string, GzLoadedModel?> _loadedModelCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Generic.Dictionary<string, GzLoadedModel?> _loadedVoxelCache = new(StringComparer.OrdinalIgnoreCase);
    private DBTexture? _imageExampleTex;
    private GlVertexBuffer? _imageExampleVb;
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
    private GlVertexBuffer? _commentIconsVb;
    private int _commentIconTris;
    private bool _renderComments = true;
    public bool RenderComments => _renderComments;
    private bool _geometryDirty = true;

    // 3D fly-mode state (toggled with Tab). Geometry built lazily into textured buckets.
    private bool _mode3D;
    public bool In3DMode => _mode3D;
    public TestMapFromViewPlacement CurrentTestMapFromViewPlacement()
        => _mode3D
            ? new TestMapFromViewPlacement(
                new Vec2D(_cam3DPos.X, _cam3DPos.Y),
                _cam3DPos.Z,
                _yaw,
                VisualMode: true)
            : new TestMapFromViewPlacement(_cursorWorld, 0, null, VisualMode: false);
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
    private (int X, int Y)? _texOffsetClipboard3D; // sidedef texture offsets copied off a wall for paste
    private bool _look3D;            // left-drag mouse-look active in 3D
    private bool _lookMoved;         // whether the left-drag actually moved (vs a click to select)
    private VisualHit? _drag3DTarget; // surface/thing captured for a right-drag height change
    private DBuilder.Geometry.Vector3D? _orbit3DPoint; // target point captured while orbiting the 3D camera
    private double _drag3DAccum;       // accumulated sub-unit drag movement
    private VisualHit? _visualPaintSelectHighlight;
    private KeyModifiers _visualPaintSelectModifiers;
    private readonly System.Collections.Generic.List<VisualHit> _sel3D = new(); // multi-surface selection
    private GlVertexBuffer? _pick3DVb;
    private GlVertexBuffer? _things3DVb; // reused per-frame for camera-facing thing billboards
    private GlVertexBuffer? _model3DVb; // reused per-batch for prepared model vertices
    private DBIndexBuffer? _model3DIb; // reused per-batch for prepared model indices
    /// <summary>Raised when the 3D crosshair target changes (for the status bar).</summary>
    public event Action<string>? Target3DChanged;

    public MapControl()
    {
        Focusable = true; // required to receive keyboard input for the 3D fly camera
        _autoPanTimer.Tick += (_, _) => ApplyAutoPanTimerTick();
    }

    private ResourceManager? _resources;
    /// <summary>Texture and model source for map rendering. Setting it invalidates resource-backed caches and geometry.</summary>
    public ResourceManager? MapResources
    {
        get => _resources;
        set { _resources = value; _loadedModelCache.Clear(); _loadedVoxelCache.Clear(); _invalidateTextures = true; _geometryDirty = true; _geo3DDirty = true; RequestNextFrameRendering(); }
    }

    private GameConfiguration? _gameConfig;
    /// <summary>Game config used to resolve a thing's sprite name for 2D sprite rendering.</summary>
    public GameConfiguration? GameConfig
    {
        get => _gameConfig;
        set { _gameConfig = value; _activeThingsFilter = null; _thingsFilterResult = null; _thingsFilterHidden.Clear(); _geometryDirty = true; RequestNextFrameRendering(); }
    }

    private MapOptions? _mapOptions;
    public MapOptions? MapOptions
    {
        get => _mapOptions;
        set => _mapOptions = value;
    }

    private MapFormat _mapFormat = MapFormat.Doom;
    public MapFormat MapFormat
    {
        get => _mapFormat;
        set
        {
            if (_mapFormat == value) return;
            _mapFormat = value;
            _geometryDirty = true;
            RequestNextFrameRendering();
        }
    }

    private bool _needsFit;
    private MapSet? _map;
    public MapSet? Map
    {
        get => _map;
        // Defer the fit: when a map is set at startup the control isn't laid out yet (Bounds == 0),
        // so fitting now would compute a bogus zoom. Fit on the first render that has real dimensions.
        set { _map = value; _thingsFilterResult = null; _thingsFilterHidden.Clear(); _sel3D.Clear(); _rejectOverlayColors = System.Array.Empty<int>(); _rejectOverlayAlpha = 96; _rejectOverlayDirty = true; _visplaneOverlay = System.Array.Empty<VisplaneOverlayRectangle>(); _visplaneOverlayDirty = true; _soundLeakPath = System.Array.Empty<Vec2D>(); _soundLeakDirty = true; _wadAuthorHighlight = WadAuthorHighlight.None; _wadAuthorDirty = true; _geometryDirty = true; _geo3DDirty = true; _needsFit = true; _cam3DInit = false; _blockmapCache = null; _blockmapExplorerData = null; _blockmapExplorerColumn = null; _blockmapExplorerRow = null; RequestNextFrameRendering(); }
    }

    // 2D view-layer visibility toggles.
    private bool _showFills = true;
    private bool _showThings = true;
    private bool _synchronizedThingEditing;
    private int _showVisualThings = 2;
    private bool _fixedThingsScale;
    private bool _alwaysShowVertices = true;
    private bool _fullBrightness = true;
    private bool _useHighlight = true;
    private ThingModelRenderMode _modelRenderMode = ThingModelRenderMode.All;
    private ThingLightRenderMode _lightRenderMode = ThingLightRenderMode.All;
    private bool _enhancedRenderingEffects = true;
    private bool _classicRendering;
    private bool _drawFog;
    private bool _drawSky = true;
    private bool _showEventLines = true;
    private bool _showVisualVertices = true;
    private byte _doubleSidedAlphaByte = Settings.DefaultDoubleSidedAlphaByte;
    private int _visualFovDegrees = Settings.DefaultVisualFov;
    private int _viewDistance = Settings.DefaultViewDistance;
    private int _moveSpeed = Settings.DefaultMoveSpeed;
    private int _mouseSpeed = Settings.DefaultMouseSpeed;
    private int _highlightRange = Settings.DefaultHighlightRange;
    private int _thingHighlightRange = Settings.DefaultThingHighlightRange;
    private int _splitLinedefsRange = Settings.DefaultSplitLinedefsRange;
    private int _autoScrollSpeed;
    private bool _alphaBasedTextureHighlighting = true;
    private bool _selectAdjacentVisualVertexSlopeHandles;
    private bool _useOppositeSmartPivotHandle = true;
    private bool _markExtraFloors = true;
    private VisualSlopePickingMode _visualSlopePickingMode;
    private ClassicViewMode _classicViewMode = ClassicViewMode.Wireframe;

    public bool ShowSectorFills => _showFills;
    public bool ShowThings => _showThings;
    public bool SynchronizedThingEditing => _synchronizedThingEditing;
    public int ShowVisualThings => _showVisualThings;
    public bool FixedThingsScale => _fixedThingsScale;
    public bool AlwaysShowVertices => _alwaysShowVertices;
    public bool FullBrightness => _fullBrightness;
    public bool UseHighlight => _useHighlight;
    public ThingModelRenderMode ModelRenderMode => _modelRenderMode;
    public ThingLightRenderMode LightRenderMode => _lightRenderMode;
    public bool EnhancedRenderingEffects => _enhancedRenderingEffects;
    public bool ClassicRendering => _classicRendering;
    public bool DrawFog => _drawFog;
    public bool DrawSky => _drawSky;
    public bool ShowEventLines => _showEventLines;
    public bool ShowVisualVertices => _showVisualVertices;
    public byte DoubleSidedAlphaByte
    {
        get => _doubleSidedAlphaByte;
        set
        {
            if (_doubleSidedAlphaByte == value) return;
            _doubleSidedAlphaByte = value;
            _geometryDirty = true;
            RequestNextFrameRendering();
        }
    }
    public int VisualFovDegrees
    {
        get => _visualFovDegrees;
        set
        {
            int clamped = Math.Clamp(value, Settings.MinVisualFov, Settings.MaxVisualFov);
            if (_visualFovDegrees == clamped) return;
            _visualFovDegrees = clamped;
            RequestNextFrameRendering();
        }
    }
    public int ViewDistance
    {
        get => _viewDistance;
        set
        {
            int clamped = Math.Clamp(value, Settings.MinViewDistance, Settings.MaxViewDistance);
            if (_viewDistance == clamped) return;
            _viewDistance = clamped;
            RequestNextFrameRendering();
        }
    }
    public int MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = Math.Clamp(value, Settings.MinMoveSpeed, Settings.MaxMoveSpeed);
    }
    public int MouseSpeed
    {
        get => _mouseSpeed;
        set => _mouseSpeed = Math.Clamp(value, Settings.MinMouseSpeed, Settings.MaxMouseSpeed);
    }
    public int HighlightRange
    {
        get => _highlightRange;
        set => _highlightRange = Math.Max(0, value);
    }
    public int ThingHighlightRange
    {
        get => _thingHighlightRange;
        set => _thingHighlightRange = Math.Max(0, value);
    }
    public int SplitLinedefsRange
    {
        get => _splitLinedefsRange;
        set => _splitLinedefsRange = Math.Max(0, value);
    }
    public bool AlphaBasedTextureHighlighting => _alphaBasedTextureHighlighting;
    public bool SelectAdjacentVisualVertexSlopeHandles => _selectAdjacentVisualVertexSlopeHandles;
    public bool UseOppositeSmartPivotHandle => _useOppositeSmartPivotHandle;
    public bool MarkExtraFloors => _markExtraFloors;
    public VisualSlopePickingMode CurrentVisualSlopePickingMode => _visualSlopePickingMode;
    public ClassicViewMode ViewMode2D => _classicViewMode;
    public int AutoScrollSpeed
    {
        get => _autoScrollSpeed;
        set
        {
            _autoScrollSpeed = Math.Clamp(value, Settings.MinAutoScrollSpeed, Settings.MaxAutoScrollSpeed);
            if (_autoScrollSpeed == 0) StopAutoPan();
        }
    }
    public bool ImageExampleMode { get; private set; }
    public bool AutomapMode { get; private set; }
    public bool WadAuthorMode { get; private set; }

    public bool ToggleSectorFills()
    {
        _showFills = !_showFills;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _showFills;
    }

    public bool ToggleThings()
    {
        _showThings = !_showThings;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _showThings;
    }

    public bool ToggleSynchronizedThingEditing()
    {
        _synchronizedThingEditing = !_synchronizedThingEditing;
        ActionStateChanged?.Invoke();
        return _synchronizedThingEditing;
    }

    public int CycleVisualThings()
    {
        _showVisualThings++;
        if (_showVisualThings > 2) _showVisualThings = 0;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _showVisualThings;
    }

    public bool ToggleFixedThingsScale()
    {
        _fixedThingsScale = !_fixedThingsScale;
        _geometryDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _fixedThingsScale;
    }

    public bool SetFixedThingsScale(bool enabled)
    {
        if (_fixedThingsScale == enabled) return _fixedThingsScale;
        _fixedThingsScale = enabled;
        _geometryDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _fixedThingsScale;
    }

    public bool ToggleAlwaysShowVertices()
    {
        _alwaysShowVertices = !_alwaysShowVertices;
        _geometryDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _alwaysShowVertices;
    }

    public bool SetAlwaysShowVertices(bool enabled)
    {
        if (_alwaysShowVertices == enabled) return _alwaysShowVertices;
        _alwaysShowVertices = enabled;
        _geometryDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _alwaysShowVertices;
    }

    public bool ToggleFullBrightness()
    {
        _fullBrightness = !_fullBrightness;
        _geometryDirty = true;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _fullBrightness;
    }

    public bool SetUseHighlight(bool enabled)
    {
        if (_useHighlight == enabled) return _useHighlight;
        _useHighlight = enabled;
        _geometryDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _useHighlight;
    }

    public bool ToggleHighlight()
    {
        SetUseHighlight(!_useHighlight);
        return _useHighlight;
    }

    public ThingModelRenderMode SetModelRenderMode(ThingModelRenderMode mode)
    {
        if (_modelRenderMode == mode) return _modelRenderMode;
        _modelRenderMode = mode;
        _geometryDirty = true;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _modelRenderMode;
    }

    public ThingModelRenderMode CycleModelRenderMode()
        => SetModelRenderMode(ThingModelRenderPlanner.NextMode(_modelRenderMode));

    public ThingLightRenderMode SetLightRenderMode(ThingLightRenderMode mode)
    {
        if (_lightRenderMode == mode) return _lightRenderMode;
        _lightRenderMode = mode;
        _geometryDirty = true;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _lightRenderMode;
    }

    public ThingLightRenderMode CycleLightRenderMode()
        => SetLightRenderMode(ThingLightRenderPlanner.NextMode(_lightRenderMode));

    public bool SetEnhancedRenderingEffects(bool enabled)
    {
        if (!enabled)
        {
            ApplyRenderingEffectsState(VisualRenderingEffectsPlanner.Disabled());
            return _enhancedRenderingEffects;
        }

        if (_enhancedRenderingEffects == enabled) return _enhancedRenderingEffects;
        _enhancedRenderingEffects = enabled;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _enhancedRenderingEffects;
    }

    public VisualRenderingEffectsState ToggleEnhancedRenderingEffects()
    {
        VisualRenderingEffectsState state = VisualRenderingEffectsPlanner.Toggle(new VisualRenderingEffectsState(
            _enhancedRenderingEffects,
            _drawFog,
            _drawSky,
            _lightRenderMode,
            _modelRenderMode,
            _show3DFloors));
        ApplyRenderingEffectsState(state);
        return state;
    }

    public bool SetClassicRendering(bool enabled)
    {
        if (_classicRendering == enabled) return _classicRendering;
        _classicRendering = enabled;
        _geometryDirty = true;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _classicRendering;
    }

    public bool ToggleClassicRendering()
        => SetClassicRendering(!_classicRendering);

    public bool SetDrawFog(bool enabled)
    {
        if (_drawFog == enabled) return _drawFog;
        _drawFog = enabled;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _drawFog;
    }

    public bool ToggleDrawFog()
        => SetDrawFog(!_drawFog);

    public bool SetDrawSky(bool enabled)
    {
        if (_drawSky == enabled) return _drawSky;
        _drawSky = enabled;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _drawSky;
    }

    public bool ToggleDrawSky()
        => SetDrawSky(!_drawSky);

    public bool SetShowEventLines(bool enabled)
    {
        if (_showEventLines == enabled) return _showEventLines;
        _showEventLines = enabled;
        _geometryDirty = true;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _showEventLines;
    }

    public bool ToggleEventLines()
        => SetShowEventLines(!_showEventLines);

    public bool ToggleComments()
    {
        _renderComments = !_renderComments;
        _geometryDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _renderComments;
    }

    public bool SetShowVisualVertices(bool enabled)
    {
        if (_showVisualVertices == enabled) return _showVisualVertices;
        _showVisualVertices = enabled;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _showVisualVertices;
    }

    public bool ToggleVisualVertices()
        => SetShowVisualVertices(!_showVisualVertices);

    private void ApplyRenderingEffectsState(VisualRenderingEffectsState state)
    {
        bool changed = _enhancedRenderingEffects != state.EnhancedRenderingEffects
            || _drawFog != state.DrawFog
            || _drawSky != state.DrawSky
            || _lightRenderMode != state.LightRenderMode
            || _modelRenderMode != state.ModelRenderMode
            || _show3DFloors != state.Show3DFloors;
        if (!changed) return;

        _enhancedRenderingEffects = state.EnhancedRenderingEffects;
        _drawFog = state.DrawFog;
        _drawSky = state.DrawSky;
        _lightRenderMode = state.LightRenderMode;
        _modelRenderMode = state.ModelRenderMode;
        _show3DFloors = state.Show3DFloors;
        _geometryDirty = true;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
    }

    public bool SetAlphaBasedTextureHighlighting(bool enabled)
    {
        if (_alphaBasedTextureHighlighting == enabled) return _alphaBasedTextureHighlighting;
        _alphaBasedTextureHighlighting = enabled;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _alphaBasedTextureHighlighting;
    }

    public bool ToggleAlphaBasedTextureHighlighting()
    {
        SetAlphaBasedTextureHighlighting(!_alphaBasedTextureHighlighting);
        Target3DChanged?.Invoke(AlphaBasedTextureHighlightingStatusText(_alphaBasedTextureHighlighting));
        return _alphaBasedTextureHighlighting;
    }

    public static string AlphaBasedTextureHighlightingStatusText(bool enabled)
        => "Alpha-based textures highlighting is " + (enabled ? "ENABLED" : "DISABLED");

    public VisualSlopePickingMode ToggleVisualSidedefSlopePicking()
    {
        if (!CanUseVisualSlopePicking()) return _visualSlopePickingMode;

        SetVisualSlopePickingMode(
            _visualSlopePickingMode == VisualSlopePickingMode.SidedefSlopeHandles
                ? VisualSlopePickingMode.Default
                : VisualSlopePickingMode.SidedefSlopeHandles);
        return _visualSlopePickingMode;
    }

    public VisualSlopePickingMode ToggleVisualVertexSlopePicking()
    {
        if (!CanUseVisualSlopePicking()) return _visualSlopePickingMode;

        SetVisualSlopePickingMode(
            _visualSlopePickingMode == VisualSlopePickingMode.VertexSlopeHandles
                ? VisualSlopePickingMode.Default
                : VisualSlopePickingMode.VertexSlopeHandles);
        return _visualSlopePickingMode;
    }

    public VisualSlopePickingMode SetVisualSlopePickingMode(VisualSlopePickingMode mode)
    {
        if (_visualSlopePickingMode == mode) return _visualSlopePickingMode;
        _visualSlopePickingMode = mode;
        _geo3DDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _visualSlopePickingMode;
    }

    public bool SetSelectAdjacentVisualVertexSlopeHandles(bool enabled)
    {
        if (_selectAdjacentVisualVertexSlopeHandles == enabled) return _selectAdjacentVisualVertexSlopeHandles;
        _selectAdjacentVisualVertexSlopeHandles = enabled;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _selectAdjacentVisualVertexSlopeHandles;
    }

    public bool SetUseOppositeSmartPivotHandle(bool enabled)
    {
        if (_useOppositeSmartPivotHandle == enabled) return _useOppositeSmartPivotHandle;
        _useOppositeSmartPivotHandle = enabled;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _useOppositeSmartPivotHandle;
    }

    public bool SetMarkExtraFloors(bool enabled)
    {
        if (_markExtraFloors == enabled) return _markExtraFloors;
        _markExtraFloors = enabled;
        _geometryDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _markExtraFloors;
    }

    public bool ToggleVisualVertexSlopeAdjacentSelection()
    {
        if (!CanToggleAdjacentVisualVertexSlopeSelection()) return _selectAdjacentVisualVertexSlopeHandles;

        SetSelectAdjacentVisualVertexSlopeHandles(!_selectAdjacentVisualVertexSlopeHandles);
        Target3DChanged?.Invoke(VisualSlopePickingPolicy.AdjacentVertexSlopeSelectionStatus(_selectAdjacentVisualVertexSlopeHandles));
        return _selectAdjacentVisualVertexSlopeHandles;
    }

    private bool CanUseVisualSlopePicking()
    {
        if (VisualSlopePickingPolicy.CanUse(_mapFormat, _gameConfig, out string warning)) return true;
        Target3DChanged?.Invoke(warning);
        return false;
    }

    private bool CanToggleAdjacentVisualVertexSlopeSelection()
    {
        if (VisualSlopePickingPolicy.CanToggleAdjacentVertexSelection(_mapFormat, _gameConfig, out string warning)) return true;
        Target3DChanged?.Invoke(warning);
        return false;
    }

    public bool MoveCameraToCursor()
    {
        if (!_mode3D || _map == null) return false;

        _blockmapCache ??= new DBuilder.Map.BlockMap(_map);
        UpdateTarget3D();
        if (_target3D is not { } target) return false;

        var current = new DBuilder.Geometry.Vector3D(_cam3DPos.X, _cam3DPos.Y, _cam3DPos.Z);
        if (!VisualCameraMovement.TryMoveCameraToCursor(current, target.Point, out DBuilder.Geometry.Vector3D next)) return false;

        _cam3DPos = new Vector3((float)next.x, (float)next.y, (float)next.z);
        RequestNextFrameRendering();
        return true;
    }

    public ClassicViewMode SetViewMode2D(ClassicViewMode mode)
    {
        _classicViewMode = mode;
        _geometryDirty = true;
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _classicViewMode;
    }

    public ClassicViewMode NextViewMode2D()
    {
        int next = ((int)_classicViewMode + 1) % 4;
        return SetViewMode2D((ClassicViewMode)next);
    }

    public ClassicViewMode PreviousViewMode2D()
    {
        int previous = (int)_classicViewMode == 0 ? 3 : (int)_classicViewMode - 1;
        return SetViewMode2D((ClassicViewMode)previous);
    }

    public bool ToggleImageExampleMode()
    {
        SetImageExampleMode(!ImageExampleMode);
        return ImageExampleMode;
    }

    public void SetImageExampleMode(bool enabled)
    {
        if (ImageExampleMode == enabled) return;
        ImageExampleMode = enabled;
        if (ImageExampleMode)
        {
            AutomapMode = false;
            ClearAutomapState();
            if (WadAuthorMode) LeaveWadAuthorMode();
        }
        if (ImageExampleMode && _mode3D) _mode3D = false;
        ActionStateChanged?.Invoke();
        ModeChanged?.Invoke();
        RequestNextFrameRendering();
    }

    private bool _thingArrows;
    /// <summary>When true, things draw as Doom-Builder-style colored discs with a direction arrow instead of sprites.</summary>
    public bool ThingArrows
    {
        get => _thingArrows;
        set { _thingArrows = value; _geometryDirty = true; ActionStateChanged?.Invoke(); RequestNextFrameRendering(); }
    }

    private bool _show3DFloors = true;
    /// <summary>When true, visual mode renders resolved GZDoom 3D floor slabs.</summary>
    public bool Show3DFloors
    {
        get => _show3DFloors;
        set
        {
            if (_show3DFloors == value) return;
            _show3DFloors = value;
            _geo3DDirty = true;
            ActionStateChanged?.Invoke();
            RequestNextFrameRendering();
        }
    }

    // Draw-geometry tool state. While active, left-clicks place loop vertices; closing builds a sector
    // (or, in lines-only mode, just the linedefs of the drawn polyline).
    private bool _drawMode;
    private bool _drawLinesOnly; // Shift+D: lay plain linedefs instead of building a sector
    private bool _drawCurve;     // Curve mode smooths the placed control points into linedefs
    private bool _drawClosed;    // set when the user closes the polyline by clicking the first point
    private readonly System.Collections.Generic.List<Vec2D> _drawPoints = new();
    private ThreeDFloorSlopeDrawingMode _threeDFloorSlopeDrawingMode = ThreeDFloorSlopeDrawingMode.FloorAndCeiling;
    private bool _threeDFloorSlopeFlipped;
    private Vec2D _drawCursor;
    private Vec2D _cursorWorld; // last known cursor position in world space (for cursor-targeted actions)
    private AutomapHighlightResult? _automapHighlight;
    private bool _automapEditSectors;
    private bool _automapInvertLineVisibility;
    private WadAuthorHighlight _wadAuthorHighlight = WadAuthorHighlight.None;
    private bool _wadAuthorDirty;
    private GlVertexBuffer? _drawVb;
    private int _drawLineCount;
    private bool _drawDirty; // rebuild the preview buffer on the render thread, not from input handlers

    // Shape-draw tool: while active, a left-drag defines a bounding box that becomes generated geometry.
    public enum ShapeKind { None, Rectangle, Ellipse, Grid }
    private ShapeKind _shapeKind = ShapeKind.None;
    private DrawLineModeSettings _drawLineSettings = new();
    private DrawRectangleModeSettings _drawRectangleSettings = new();
    private DrawEllipseModeSettings _drawEllipseSettings = new();
    private DrawCurveModeSettings _drawCurveSettings = new();
    private CurveLinedefsOptions _curveLinedefsSettings = new();
    private MergeGeometryMode _mergeGeometryMode = MergeGeometryMode.Replace;
    private DrawGridModeSettings _drawGridSettings = new();
    private AutomapModeSettings _automapSettings = new();
    private IReadOnlyList<LinedefColorPreset> _linedefColorPresets = LinedefColorPresetModel.DefaultPresets;
    private ThreeDFloorControlSectorAreaSettings _threeDFloorControlSectorAreaSettings = new();
    private int _defaultSectorFloorHeight = Settings.DefaultSectorFloorHeight;
    private int _defaultSectorCeilingHeight = Settings.DefaultSectorCeilingHeight;
    private int _defaultSectorBrightness = Settings.DefaultSectorBrightness;
    public ShapeKind CurrentShape => _shapeKind;

    public DrawLineModeSettings DrawLineSettings
    {
        get => _drawLineSettings;
        set => _drawLineSettings = (value ?? new DrawLineModeSettings()).Normalized();
    }

    public DrawRectangleModeSettings DrawRectangleSettings
    {
        get => _drawRectangleSettings;
        set => _drawRectangleSettings = (value ?? new DrawRectangleModeSettings()).Normalized();
    }

    public DrawEllipseModeSettings DrawEllipseSettings
    {
        get => _drawEllipseSettings;
        set => _drawEllipseSettings = (value ?? new DrawEllipseModeSettings()).Normalized();
    }

    public DrawCurveModeSettings DrawCurveSettings
    {
        get => _drawCurveSettings;
        set => _drawCurveSettings = (value ?? new DrawCurveModeSettings()).Normalized();
    }

    public CurveLinedefsOptions CurveLinedefsSettings
    {
        get => _curveLinedefsSettings;
        set => _curveLinedefsSettings = (value ?? new CurveLinedefsOptions()).Normalized();
    }

    public MergeGeometryMode MergeGeometryMode
    {
        get => Enum.IsDefined(_mergeGeometryMode) ? _mergeGeometryMode : MergeGeometryMode.Replace;
        set => _mergeGeometryMode = Enum.IsDefined(value) ? value : MergeGeometryMode.Replace;
    }

    public DrawGridModeSettings DrawGridSettings
    {
        get => _drawGridSettings;
        set => _drawGridSettings = (value ?? new DrawGridModeSettings()).Normalized();
    }

    public AutomapModeSettings AutomapSettings
    {
        get => _automapSettings;
        set
        {
            _automapSettings = (value ?? new AutomapModeSettings()).Normalized();
            if (!AutomapMode) return;
            UpdateAutomapHighlight(_cursorWorld, CurrentAutomapModifiers());
            _geometryDirty = true;
            RequestNextFrameRendering();
        }
    }

    public IReadOnlyList<LinedefColorPreset> LinedefColorPresets
    {
        get => _linedefColorPresets;
        set
        {
            _linedefColorPresets = LinedefColorPresetModel.NormalizedPresets(value);
            _geometryDirty = true;
            RequestNextFrameRendering();
        }
    }

    public ThreeDFloorControlSectorAreaSettings ThreeDFloorControlSectorAreaSettings
    {
        get => _threeDFloorControlSectorAreaSettings;
        set => _threeDFloorControlSectorAreaSettings = value ?? new ThreeDFloorControlSectorAreaSettings();
    }

    public int DefaultSectorFloorHeight
    {
        get => _defaultSectorFloorHeight;
        set => _defaultSectorFloorHeight = value;
    }

    public int DefaultSectorCeilingHeight
    {
        get => _defaultSectorCeilingHeight;
        set => _defaultSectorCeilingHeight = value;
    }

    public int DefaultSectorBrightness
    {
        get => _defaultSectorBrightness;
        set => _defaultSectorBrightness = Math.Clamp(value, 0, 255);
    }

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
        _rejectOverlayDirty = true;
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
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
    }
    private bool _invalidateTextures; // dispose cached GL textures on the render thread (context current)

    // Camera: world-space center + zoom in world-units-per-DIP.
    private double _camX, _camY, _zoom = 1.0;

    public Vec2D ViewCenter => new(_camX, _camY);
    public double ViewScale => _zoom;

    /// <summary>Which element class clicks select. Switched with the number keys 1-4.</summary>
    public enum EditMode { Vertices, Linedefs, Sectors, Things }
    public enum ThreeDFloorEditMode { None, Floor, Slope, DrawSlopes, StairSectorBuilder }
    private EditMode _editMode = EditMode.Linedefs;
    private ThreeDFloorEditMode _threeDFloorEditMode;
    private int? _highlightedThreeDFloorCycleIndex;
    public EditMode CurrentEditMode => _editMode;
    public ThreeDFloorEditMode CurrentThreeDFloorEditMode => _threeDFloorEditMode;
    public int? HighlightedThreeDFloorCycleIndex => _highlightedThreeDFloorCycleIndex;
    public string Current2DModeStatusText => _threeDFloorEditMode switch
    {
        ThreeDFloorEditMode.Floor => ThreeDFloors.ModeDescriptor.DisplayName,
        ThreeDFloorEditMode.Slope => ThreeDFloors.SlopeModeDescriptor.DisplayName,
        ThreeDFloorEditMode.DrawSlopes => ThreeDFloors.DrawSlopesModeDescriptor.DisplayName,
        ThreeDFloorEditMode.StairSectorBuilder => "Stair Sector Builder Mode",
        _ => _editSelectionMode ? "Edit Selection" : _editMode.ToString(),
    };
    private bool _editSelectionMode;
    public bool EditSelectionModeActive => _editSelectionMode;
    /// <summary>Raised when the active selection mode changes (for the status bar).</summary>
    public event Action? ModeChanged;

    private void SetEditMode(EditMode m)
    {
        if (AutomapMode)
        {
            AutomapMode = false;
            ClearAutomapState();
            _geometryDirty = true;
            ActionStateChanged?.Invoke();
        }
        if (WadAuthorMode)
        {
            LeaveWadAuthorMode();
            _geometryDirty = true;
            ActionStateChanged?.Invoke();
        }
        SetImageExampleMode(false);
        bool changedThreeDFloorMode = _threeDFloorEditMode != ThreeDFloorEditMode.None;
        if (_threeDFloorEditMode == ThreeDFloorEditMode.DrawSlopes) ClearThreeDFloorSlopeDraw();
        _threeDFloorEditMode = ThreeDFloorEditMode.None;
        SetEditSelectionMode(false);
        if (_editMode == m && !changedThreeDFloorMode) return;
        _editMode = m;
        ModeChanged?.Invoke();
        Picked?.Invoke($"mode: {m}");
        RequestNextFrameRendering();
    }

    public void SetCurrentEditMode(EditMode mode) => SetEditMode(mode);

    private void SetThreeDFloorEditMode(ThreeDFloorEditMode mode)
    {
        if (AutomapMode)
        {
            AutomapMode = false;
            ClearAutomapState();
            _geometryDirty = true;
            ActionStateChanged?.Invoke();
        }
        if (WadAuthorMode)
        {
            LeaveWadAuthorMode();
            _geometryDirty = true;
            ActionStateChanged?.Invoke();
        }
        SetImageExampleMode(false);
        SetEditSelectionMode(false);
        if (_threeDFloorEditMode == mode) return;
        if (_threeDFloorEditMode == ThreeDFloorEditMode.DrawSlopes || mode == ThreeDFloorEditMode.DrawSlopes)
            ClearThreeDFloorSlopeDraw();
        _threeDFloorEditMode = mode;
        ModeChanged?.Invoke();
        Picked?.Invoke($"mode: {Current2DModeStatusText}");
        RequestNextFrameRendering();
    }

    public void BeginEditSelectionMode() => SetEditSelectionMode(true);

    private void SetEditSelectionMode(bool enabled)
    {
        if (_editSelectionMode == enabled) return;
        if (enabled) _threeDFloorEditMode = ThreeDFloorEditMode.None;
        _editSelectionMode = enabled;
        if (enabled)
        {
            SetImageExampleMode(false);
            if (AutomapMode)
            {
                AutomapMode = false;
                ClearAutomapState();
            }
            if (WadAuthorMode) LeaveWadAuthorMode();
        }
        ModeChanged?.Invoke();
        ActionStateChanged?.Invoke();
        Picked?.Invoke(enabled ? "mode: Edit Selection" : $"mode: {_editMode}");
        RequestNextFrameRendering();
    }

    public bool HasCopiedPropertiesForCurrentMode => CurrentPropertyKind() switch
    {
        PastePropertiesElementKind.Vertex => _pastePropertiesClipboard.CopiedState.Vertex,
        PastePropertiesElementKind.Linedef => _pastePropertiesClipboard.CopiedState.Linedef,
        PastePropertiesElementKind.Sector => _pastePropertiesClipboard.CopiedState.Sector,
        PastePropertiesElementKind.Thing => _pastePropertiesClipboard.CopiedState.Thing,
        _ => false,
    };

    public bool HasCurrentPropertyTarget => _map != null &&
        (CurrentPropertySelectionCount() > 0 || CurrentPropertyHighlight() != null);

    public string CopyPropertiesSelection()
    {
        if (_map == null) return "No map loaded.";

        PastePropertiesElementKind kind = CurrentPropertyKind();
        PastePropertiesCopyResult result;
        if (CurrentPropertySelectionCount() > 0)
        {
            result = _pastePropertiesClipboard.CopySelected(_map, kind);
        }
        else if (CurrentPropertyHighlight() is { } highlight)
        {
            result = WithTemporarySelection(highlight, () => _pastePropertiesClipboard.CopySelected(_map, kind));
        }
        else
        {
            result = new PastePropertiesCopyResult(false, kind, "This action requires highlight or selection!");
        }

        Picked?.Invoke(result.StatusMessage);
        ActionStateChanged?.Invoke();
        return result.StatusMessage;
    }

    public PastePropertiesOptionsResult BuildPastePropertiesOptionsForCurrentMode()
    {
        if (_map == null)
            return new PastePropertiesOptionsResult(false, "No map loaded.", []);

        return _pastePropertiesClipboard.BuildOptions(
            [CurrentPropertyKind()],
            supportsUdmf: _mapFormat == MapFormat.Udmf);
    }

    public string PastePropertiesSelection(ISet<string>? enabledKeys = null)
    {
        if (_map == null) return "No map loaded.";

        PastePropertiesElementKind kind = CurrentPropertyKind();
        if (!HasCopiedPropertiesForCurrentMode)
        {
            string missing = $"Copy {PropertyKindText(kind)} properties first!";
            Picked?.Invoke(missing);
            return missing;
        }
        ISelectable? highlight = null;
        if (CurrentPropertySelectionCount() == 0)
        {
            highlight = CurrentPropertyHighlight();
            if (highlight == null)
            {
                const string required = "This action requires highlight or selection!";
                Picked?.Invoke(required);
                return required;
            }
        }
        if (enabledKeys is { Count: 0 })
        {
            const string none = "No paste properties selected.";
            Picked?.Invoke(none);
            return none;
        }

        EditBegun?.Invoke($"Paste {PropertyKindText(kind)} properties");
        PastePropertiesApplyResult result = highlight == null
            ? _pastePropertiesClipboard.ApplySelected(
                _map,
                kind,
                supportsUdmf: _mapFormat == MapFormat.Udmf,
                enabledKeys)
            : WithTemporarySelection(
                highlight,
                () => _pastePropertiesClipboard.ApplySelected(
                    _map,
                    kind,
                    supportsUdmf: _mapFormat == MapFormat.Udmf,
                    enabledKeys));
        if (result.Applied)
        {
            MarkGeometryDirty();
            Changed?.Invoke();
        }

        Picked?.Invoke(result.StatusMessage);
        return result.StatusMessage;
    }

    private PastePropertiesElementKind CurrentPropertyKind() => _editMode switch
    {
        EditMode.Vertices => PastePropertiesElementKind.Vertex,
        EditMode.Linedefs => PastePropertiesElementKind.Linedef,
        EditMode.Sectors => PastePropertiesElementKind.Sector,
        EditMode.Things => PastePropertiesElementKind.Thing,
        _ => PastePropertiesElementKind.Linedef,
    };

    private int CurrentPropertySelectionCount() => _map == null ? 0 : _editMode switch
    {
        EditMode.Vertices => _map.SelectedVerticesCount,
        EditMode.Linedefs => _map.SelectedLinedefsCount,
        EditMode.Sectors => _map.SelectedSectorsCount,
        EditMode.Things => _map.SelectedThingsCount,
        _ => 0,
    };

    private ISelectable? CurrentPropertyHighlight()
    {
        if (_map == null) return null;

        return _editMode switch
        {
            EditMode.Vertices => _map.NearestVertex(_cursorWorld, HighlightRangeWorld()),
            EditMode.Linedefs => _map.NearestLinedef(_cursorWorld, HighlightRangeWorld()),
            EditMode.Sectors => _map.GetSectorAt(_cursorWorld),
            EditMode.Things => NearestVisibleThing(_cursorWorld, ThingHighlightRangeWorld()),
            _ => null,
        };
    }

    private static T WithTemporarySelection<T>(ISelectable target, Func<T> action)
    {
        bool wasSelected = target.Selected;
        target.Selected = true;
        try
        {
            return action();
        }
        finally
        {
            target.Selected = wasSelected;
        }
    }

    private static string PropertyKindText(PastePropertiesElementKind kind) => kind switch
    {
        PastePropertiesElementKind.Vertex => "vertex",
        PastePropertiesElementKind.Linedef => "linedef",
        PastePropertiesElementKind.Sidedef => "sidedef",
        PastePropertiesElementKind.Sector => "sector",
        PastePropertiesElementKind.Thing => "thing",
        _ => "element",
    };

    public bool HasVisualPropertyTarget => _map != null && (_target3D != null || _sel3D.Count > 0);

    public string CopyVisualPropertiesTarget()
    {
        if (_map == null) return "No map loaded.";
        if (_target3D is not { } target)
        {
            const string required = "This action requires highlight or selection!";
            Target3DChanged?.Invoke(required);
            return required;
        }

        string status = WithTemporaryVisualPropertySelection(
            [target],
            () =>
            {
                if (target.Kind == VisualHitKind.Wall)
                {
                    PastePropertiesCopyResult line = _pastePropertiesClipboard.CopySelected(_map, PastePropertiesElementKind.Linedef);
                    PastePropertiesCopyResult side = _pastePropertiesClipboard.CopySelected(_map, PastePropertiesElementKind.Sidedef);
                    return line.Copied && side.Copied ? "Copied linedef and sidedef properties." : line.StatusMessage;
                }

                PastePropertiesElementKind kind = VisualPropertyKind(target);
                return _pastePropertiesClipboard.CopySelected(_map, kind).StatusMessage;
            });
        Target3DChanged?.Invoke(status);
        ActionStateChanged?.Invoke();
        return status;
    }

    public PastePropertiesOptionsResult BuildVisualPastePropertiesOptions()
    {
        if (_map == null)
            return new PastePropertiesOptionsResult(false, "No map loaded.", []);
        if (!HasVisualPropertyTarget)
            return new PastePropertiesOptionsResult(false, "This action requires highlight or selection!", []);

        return _pastePropertiesClipboard.BuildOptions(
            VisualPropertyKinds(EditTargets3D()),
            supportsUdmf: _mapFormat == MapFormat.Udmf);
    }

    public string PasteVisualPropertiesTargets(ISet<string>? enabledKeys = null)
    {
        if (_map == null) return "No map loaded.";
        List<VisualHit> targets = EditTargets3D();
        if (targets.Count == 0)
        {
            const string required = "This action requires highlight or selection!";
            Target3DChanged?.Invoke(required);
            return required;
        }
        if (enabledKeys is { Count: 0 })
        {
            const string none = "No paste properties selected.";
            Target3DChanged?.Invoke(none);
            return none;
        }

        IReadOnlyList<PastePropertiesElementKind> targetKinds = VisualPropertyKinds(targets);
        PastePropertiesOptionsResult availableOptions = _pastePropertiesClipboard.BuildOptions(
            targetKinds,
            supportsUdmf: _mapFormat == MapFormat.Udmf);
        if (!availableOptions.IsAvailable)
        {
            string missing = availableOptions.StatusMessage ?? PastePropertiesOptionsModel.NoCopiedPropertiesMessage;
            Target3DChanged?.Invoke(missing);
            return missing;
        }

        var statuses = new List<string>();
        var appliedKinds = new List<PastePropertiesElementKind>();
        bool applied = false;
        bool hasWallTargets = targets.Any(hit => hit.Kind == VisualHitKind.Wall);
        EditBegun?.Invoke(VisualPropertiesPaste3DEditName(availableOptions.Tabs.Select(tab => tab.Kind)));
        WithTemporaryVisualPropertySelection(
            targets,
            () =>
            {
                if (hasWallTargets)
                {
                    PastePropertiesApplyResult lines = _pastePropertiesClipboard.ApplySelected(
                        _map,
                        PastePropertiesElementKind.Linedef,
                        supportsUdmf: _mapFormat == MapFormat.Udmf,
                        enabledKeys);
                    statuses.Add(lines.StatusMessage);
                    applied |= lines.Applied;
                    if (lines.Applied) appliedKinds.Add(PastePropertiesElementKind.Linedef);

                    PastePropertiesApplyResult sides = _pastePropertiesClipboard.ApplySelected(
                        _map,
                        PastePropertiesElementKind.Sidedef,
                        supportsUdmf: _mapFormat == MapFormat.Udmf,
                        enabledKeys);
                    statuses.Add(sides.StatusMessage);
                    applied |= sides.Applied;
                    if (sides.Applied) appliedKinds.Add(PastePropertiesElementKind.Sidedef);
                }

                foreach (PastePropertiesElementKind kind in targetKinds.Where(kind => kind != PastePropertiesElementKind.Linedef))
                {
                    if (hasWallTargets && kind == PastePropertiesElementKind.Sidedef) continue;

                    PastePropertiesApplyResult result = _pastePropertiesClipboard.ApplySelected(
                        _map,
                        kind,
                        supportsUdmf: _mapFormat == MapFormat.Udmf,
                        enabledKeys);
                    statuses.Add(result.StatusMessage);
                    applied |= result.Applied;
                    if (result.Applied) appliedKinds.Add(kind);
                }

                return true;
            });

        if (applied)
        {
            _geo3DDirty = true;
            MarkGeometryDirty();
            Changed?.Invoke();
            RequestNextFrameRendering();
        }

        string status = applied
            ? VisualPropertiesPasted3DStatusText(appliedKinds)
            : string.Join(" ", statuses.Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.Ordinal));
        Target3DChanged?.Invoke(status);
        return status;
    }

    public static string VisualPropertiesPasted3DStatusText(IEnumerable<PastePropertiesElementKind> appliedKinds)
    {
        var kinds = appliedKinds.Distinct().ToArray();
        if (kinds.Length == 0) return string.Empty;
        if (kinds.Contains(PastePropertiesElementKind.Linedef) && kinds.Contains(PastePropertiesElementKind.Sidedef))
            return "Pasted linedef and sidedef properties.";

        PastePropertiesElementKind kind = kinds.LastOrDefault();
        return kind switch
        {
            PastePropertiesElementKind.Vertex => "Pasted vertex properties.",
            PastePropertiesElementKind.Linedef => "Pasted linedef properties.",
            PastePropertiesElementKind.Sidedef => "Pasted sidedef properties.",
            PastePropertiesElementKind.Sector => "Pasted sector properties.",
            PastePropertiesElementKind.Thing => "Pasted thing properties.",
            _ => string.Empty,
        };
    }

    public static string VisualPropertiesPaste3DEditName(IEnumerable<PastePropertiesElementKind> availableKinds)
    {
        var kinds = availableKinds.Distinct().ToArray();
        if (kinds.Length == 0) return "Paste properties";
        if (kinds.Contains(PastePropertiesElementKind.Linedef) && kinds.Contains(PastePropertiesElementKind.Sidedef))
            return "Paste linedef and sidedef properties";

        PastePropertiesElementKind kind = kinds.LastOrDefault();
        return kind switch
        {
            PastePropertiesElementKind.Vertex => "Paste vertex properties",
            PastePropertiesElementKind.Linedef => "Paste linedef properties",
            PastePropertiesElementKind.Sidedef => "Paste sidedef properties",
            PastePropertiesElementKind.Sector => "Paste sector properties",
            PastePropertiesElementKind.Thing => "Paste thing properties",
            _ => "Paste properties",
        };
    }

    private static PastePropertiesElementKind VisualPropertyKind(VisualHit hit) => hit.Kind switch
    {
        VisualHitKind.Floor or VisualHitKind.Ceiling => PastePropertiesElementKind.Sector,
        VisualHitKind.Wall => PastePropertiesElementKind.Linedef,
        VisualHitKind.Thing => PastePropertiesElementKind.Thing,
        _ => PastePropertiesElementKind.Linedef,
    };

    private static IReadOnlyList<PastePropertiesElementKind> VisualPropertyKinds(IEnumerable<VisualHit> hits)
    {
        var result = new List<PastePropertiesElementKind>();
        foreach (VisualHit hit in hits)
        {
            PastePropertiesElementKind kind = VisualPropertyKind(hit);
            if (!result.Contains(kind)) result.Add(kind);
            if (hit.Kind == VisualHitKind.Wall && !result.Contains(PastePropertiesElementKind.Sidedef))
                result.Add(PastePropertiesElementKind.Sidedef);
        }

        return result;
    }

    private T WithTemporaryVisualPropertySelection<T>(IReadOnlyList<VisualHit> hits, Func<T> action)
    {
        if (_map == null) return action();

        var vertices = _map.GetSelectedVertices();
        var linedefs = _map.GetSelectedLinedefs();
        var sidedefs = _map.GetSelectedSidedefs();
        var sectors = _map.GetSelectedSectors();
        var things = _map.GetSelectedThings();
        _map.ClearAllSelected();
        try
        {
            foreach (VisualHit hit in hits)
            {
                switch (hit.Kind)
                {
                    case VisualHitKind.Floor:
                    case VisualHitKind.Ceiling:
                        if (hit.Sector is { } sector) sector.Selected = true;
                        break;
                    case VisualHitKind.Wall:
                        if (hit.Line is { } line)
                        {
                            line.Selected = true;
                            Sidedef? side = hit.Front ? line.Front : line.Back;
                            if (side != null) side.Selected = true;
                        }
                        break;
                    case VisualHitKind.Thing:
                        if (hit.Thing is { } thing) thing.Selected = true;
                        break;
                }
            }

            return action();
        }
        finally
        {
            _map.ClearAllSelected();
            foreach (Vertex vertex in vertices) vertex.Selected = true;
            foreach (Linedef linedef in linedefs) linedef.Selected = true;
            foreach (Sidedef sidedef in sidedefs) sidedef.Selected = true;
            foreach (Sector sector in sectors) sector.Selected = true;
            foreach (Thing thing in things) thing.Selected = true;
        }
    }

    public bool Toggle3DMode()
    {
        SetImageExampleMode(false);
        AutomapMode = false;
        ClearAutomapState();
        if (WadAuthorMode) LeaveWadAuthorMode();
        _mode3D = !_mode3D;
        if (_mode3D)
        {
            if (!_cam3DInit) { Reset3DCamera(); _cam3DInit = true; }
            _lastTime = _clock.Elapsed.TotalSeconds;
        }
        else
        {
            ApplyVisualCameraPoseToStartThing();
            _sel3D.Clear();
            _heldKeys.Clear();
            _heldMapCommands.Clear();
            _look3D = false;
            _orbit3DPoint = null;
            _drag3DTarget = null;
        }
        ModeChanged?.Invoke();
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return _mode3D;
    }

    private void ApplyVisualCameraPoseToStartThing()
    {
        var map = _map;
        int startThingType = _gameConfig?.Start3DModeThingType ?? 0;
        if (map == null || startThingType <= 0) return;

        Thing? start = map.Things.FirstOrDefault(thing => thing.Type == startThingType);
        if (start == null) return;

        var position = new DBuilder.Geometry.Vector3D(_cam3DPos.X, _cam3DPos.Y, _cam3DPos.Z);
        var beforePosition = start.Position;
        double beforeHeight = start.Height;
        int beforeAngle = start.Angle;

        var cameraSector = map.GetSectorAt(new Vec2D(_cam3DPos.X, _cam3DPos.Y));
        if (!VisualCameraMovement.TryApplyPoseToStartThing(
            map.Things,
            startThingType,
            new VisualCameraPose(position, _yaw, _pitch),
            cameraSector)) return;

        if (start.Position == beforePosition
            && start.Height == beforeHeight
            && start.Angle == beforeAngle) return;

        start.DetermineSector(map);
        map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
    }

    public bool ToggleAutomapMode()
    {
        SetImageExampleMode(false);
        if (WadAuthorMode) LeaveWadAuthorMode();
        bool enabled = !AutomapMode;
        AutomapMode = enabled;
        if (enabled)
        {
            _mode3D = false;
            _heldKeys.Clear();
            _heldMapCommands.Clear();
            _look3D = false;
            _orbit3DPoint = null;
            _drag3DTarget = null;
            ExitDrawModes();
            UpdateAutomapHighlight(_cursorWorld, KeyModifiers.None);
        }
        else
        {
            ClearAutomapState();
        }

        _geometryDirty = true;
        ModeChanged?.Invoke();
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
        return AutomapMode;
    }

    public bool ToggleWadAuthorMode()
    {
        SetImageExampleMode(false);
        if (AutomapMode)
        {
            AutomapMode = false;
            ClearAutomapState();
        }

        bool enabled = !WadAuthorMode;
        if (enabled)
        {
            _mode3D = false;
            _heldKeys.Clear();
            _look3D = false;
            _drag3DTarget = null;
            ExitDrawModes();
            WadAuthorMode = true;
            if (_map != null) WadAuthorModeModel.EnterMode(_map);
            UpdateWadAuthorHighlight(_cursorWorld);
        }
        else
        {
            LeaveWadAuthorMode();
        }

        _geometryDirty = true;
        ModeChanged?.Invoke();
        ActionStateChanged?.Invoke();
        Changed?.Invoke();
        RequestNextFrameRendering();
        return WadAuthorMode;
    }

    private void ClearAutomapState()
    {
        _automapHighlight = null;
        _automapEditSectors = false;
        _automapInvertLineVisibility = false;
    }

    private void LeaveWadAuthorMode()
    {
        if (_map != null) WadAuthorModeModel.LeaveMode(_map);
        WadAuthorMode = false;
        _wadAuthorHighlight = WadAuthorHighlight.None;
        _wadAuthorDirty = true;
    }

    // Grid setup is the UDB-compatible snap model; the visible grid renders the same transform.
    private readonly GridSetup _grid = new();
    private bool _snapToGrid = true;
    private bool _renderGrid = true;
    private bool _dynamicGridSize = true;
    private GlVertexBuffer? _gridVb;
    private int _gridLineCount;
    private GlVertexBuffer? _blockmapVb;
    private GlVertexBuffer? _blockmapFillVb;
    private int _blockmapLineCount;
    private int _blockmapFillTriCount;
    private DBuilder.Map.BlockMap? _blockmapCache; // built lazily while the overlay is shown
    private BlockmapLumpData? _blockmapExplorerData;
    private int? _blockmapExplorerColumn;
    private int? _blockmapExplorerRow;
    private bool _blockmapExplorerShared;
    private bool _blockmapExplorerQuestionable;
    private bool _showBlockmap;

    /// <summary>When true, draws the 128-unit blockmap grid (occupied blocks brighter) over the map.</summary>
    public bool ShowBlockmap
    {
        get => _showBlockmap;
        set { _showBlockmap = value; ActionStateChanged?.Invoke(); RequestNextFrameRendering(); }
    }

    public void SetBlockmapExplorerOverlay(
        BlockmapLumpData? blockmap,
        int? column,
        int? row,
        bool includeSharedBlocks,
        bool showQuestionableBlocks)
    {
        _blockmapExplorerData = blockmap;
        _blockmapExplorerColumn = column;
        _blockmapExplorerRow = row;
        _blockmapExplorerShared = includeSharedBlocks;
        _blockmapExplorerQuestionable = showQuestionableBlocks;
        RequestNextFrameRendering();
    }

    private GlVertexBuffer? _nodesVb;
    private GlVertexBuffer? _nodePolygonsVb;
    private GlVertexBuffer? _rejectOverlayVb;
    private GlVertexBuffer? _visplaneOverlayVb;
    private GlVertexBuffer? _soundLeakPathVb;
    private GlVertexBuffer? _soundLeakMarkerVb;
    private GlVertexBuffer? _wadAuthorVb;
    private GlVertexBuffer? _wadAuthorFillVb;
    private int _nodesLineCount;
    private int _nodePolygonTriCount;
    private int _rejectOverlayTriCount;
    private int _visplaneOverlayTriCount;
    private int _soundLeakLineCount;
    private int _soundLeakMarkerTriCount;
    private int _wadAuthorLineCount;
    private int _wadAuthorTriCount;
    private bool _showNodes;
    private (Vec2D a, Vec2D b)[] _nodeLines = System.Array.Empty<(Vec2D, Vec2D)>();
    private Vec2D[][] _nodePolygons = System.Array.Empty<Vec2D[]>();
    private int[] _rejectOverlayColors = System.Array.Empty<int>();
    private byte _rejectOverlayAlpha = 96;
    private bool _rejectOverlayDirty;
    private VisplaneOverlayRectangle[] _visplaneOverlay = System.Array.Empty<VisplaneOverlayRectangle>();
    private bool _visplaneOverlayDirty;
    private Vec2D[] _soundLeakPath = System.Array.Empty<Vec2D>();
    private bool _soundLeakDirty;

    /// <summary>When true, overlays the BSP node partition lines (set via <see cref="SetNodeLines"/>).</summary>
    public bool ShowNodes
    {
        get => _showNodes;
        set { _showNodes = value; _nodesDirty = true; ActionStateChanged?.Invoke(); RequestNextFrameRendering(); }
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

    /// <summary>Supplies the NodesViewer subsector polygons used for the nodes overlay fill.</summary>
    public void SetNodePolygons(System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<Vec2D>> polygons)
    {
        _nodePolygons = new Vec2D[polygons.Count][];
        for (int i = 0; i < polygons.Count; i++)
        {
            _nodePolygons[i] = new Vec2D[polygons[i].Count];
            for (int p = 0; p < polygons[i].Count; p++) _nodePolygons[i][p] = polygons[i][p];
        }
        _nodesDirty = true;
        RequestNextFrameRendering();
    }

    public void SetRejectOverlayColors(System.Collections.Generic.IReadOnlyList<int>? sectorColors)
        => SetSectorOverlayColors(sectorColors, 96);

    public void SetSoundLeakPath(SoundLeakPath? path)
    {
        if (path == null || path.Points.Count < 2)
        {
            _soundLeakPath = System.Array.Empty<Vec2D>();
        }
        else
        {
            _soundLeakPath = new Vec2D[path.Points.Count];
            for (int i = 0; i < path.Points.Count; i++) _soundLeakPath[i] = path.Points[i];
        }

        _soundLeakDirty = true;
        RequestNextFrameRendering();
    }

    public void SetSectorOverlayColors(System.Collections.Generic.IReadOnlyList<uint>? sectorColors, byte alpha = 96)
    {
        if (sectorColors == null || sectorColors.Count == 0)
        {
            _rejectOverlayColors = System.Array.Empty<int>();
        }
        else
        {
            _rejectOverlayColors = new int[sectorColors.Count];
            for (int i = 0; i < sectorColors.Count; i++) _rejectOverlayColors[i] = unchecked((int)sectorColors[i]);
        }

        _rejectOverlayAlpha = alpha;
        _rejectOverlayDirty = true;
        RequestNextFrameRendering();
    }

    public void SetVisplaneExplorerOverlay(System.Collections.Generic.IReadOnlyList<VisplaneOverlayRectangle>? rectangles)
    {
        if (rectangles == null || rectangles.Count == 0)
        {
            _visplaneOverlay = System.Array.Empty<VisplaneOverlayRectangle>();
        }
        else
        {
            _visplaneOverlay = new VisplaneOverlayRectangle[rectangles.Count];
            for (int i = 0; i < rectangles.Count; i++) _visplaneOverlay[i] = rectangles[i];
        }

        _visplaneOverlayDirty = true;
        RequestNextFrameRendering();
    }

    public void SetVisplaneExplorerOverlay(
        VisplaneTileScan? scan,
        VisplaneExplorerStat stat,
        VisplanePalette palette,
        bool showHeatmap,
        int configuredVisplaneLimit)
        => SetVisplaneExplorerOverlay(scan?.BuildOverlayRectangles(stat, palette, showHeatmap, configuredVisplaneLimit));

    private void SetSectorOverlayColors(System.Collections.Generic.IReadOnlyList<int>? sectorColors, byte alpha)
    {
        if (sectorColors == null || sectorColors.Count == 0)
        {
            _rejectOverlayColors = System.Array.Empty<int>();
        }
        else
        {
            _rejectOverlayColors = new int[sectorColors.Count];
            for (int i = 0; i < sectorColors.Count; i++) _rejectOverlayColors[i] = sectorColors[i];
        }

        _rejectOverlayAlpha = alpha;
        _rejectOverlayDirty = true;
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
    private bool _heldPanView, _heldPanViewWaitingForPointer;
    private readonly DispatcherTimer _autoPanTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _autoPanClock = new();
    private Point? _autoPanPointer;
    // Rubber-band box selection (left-drag over empty space).
    private bool _boxAdditive;
    private Vec2D _boxStartWorld, _boxCurWorld;
    private GlVertexBuffer? _boxVb;
    private bool _classicPaintSelectPressed;
    private ISelectable? _classicPaintSelectHighlight;

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
            _soundLeakDirty = true;
            _wadAuthorDirty = true;
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
            _soundLeakDirty = true;
            _wadAuthorDirty = true;
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
        _geometryDirty = true;
        _soundLeakDirty = true;
        _wadAuthorDirty = true;
    }

    public void CenterOn(double x, double y)
    {
        _camX = x;
        _camY = y;
        if (_mode3D && _map != null)
        {
            var coordinates = new Vec2D(x, y);
            DBuilder.Geometry.Vector3D position = VisualCameraMovement.PlanCenterOnCoordinatesPosition(coordinates, _map.GetSectorAt(coordinates));
            _cam3DPos = new Vector3((float)position.x, (float)position.y, (float)position.z);
        }

        RequestNextFrameRendering();
    }

    public void RestoreView(Vec2D center, double scale)
    {
        bool restored = false;
        if (!double.IsNaN(center.x) && !double.IsNaN(center.y))
        {
            _camX = center.x;
            _camY = center.y;
            restored = true;
        }

        if (!double.IsNaN(scale) && scale > 0)
        {
            _zoom = Math.Clamp(scale, 0.02, 200);
            _soundLeakDirty = true;
            _wadAuthorDirty = true;
            restored = true;
        }

        if (restored) _needsFit = false;
        if (restored) _geometryDirty = true;
        RequestNextFrameRendering();
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
        _commentIconsVb = new GlVertexBuffer(_gl);
        _drawVb = new GlVertexBuffer(_gl);
        _gridVb = new GlVertexBuffer(_gl);
        _blockmapVb = new GlVertexBuffer(_gl);
        _blockmapFillVb = new GlVertexBuffer(_gl);
        _nodesVb = new GlVertexBuffer(_gl);
        _nodePolygonsVb = new GlVertexBuffer(_gl);
        _rejectOverlayVb = new GlVertexBuffer(_gl);
        _visplaneOverlayVb = new GlVertexBuffer(_gl);
        _soundLeakPathVb = new GlVertexBuffer(_gl);
        _soundLeakMarkerVb = new GlVertexBuffer(_gl);
        _wadAuthorVb = new GlVertexBuffer(_gl);
        _wadAuthorFillVb = new GlVertexBuffer(_gl);
        _boxVb = new GlVertexBuffer(_gl);
        _pick3DVb = new GlVertexBuffer(_gl);
        _things3DVb = new GlVertexBuffer(_gl);
        _model3DVb = new GlVertexBuffer(_gl);
        _model3DIb = new DBIndexBuffer(_gl);
        _imageExampleVb = new GlVertexBuffer(_gl);
        // 1x1 white placeholder so the sampler is always complete during untextured draws.
        _placeholderTex = new DBTexture(_gl);
        _placeholderTex.SetPixelsRgba8(1, 1, new byte[] { 255, 255, 255, 255 }, generateMipmaps: false);
        _imageExampleTex = new DBTexture(_gl);
        _imageExampleTex.SetPixelsRgba8(428, 332, BuildImageExampleRgba(428, 332), generateMipmaps: false);
        _device.SetTexture(0, _placeholderTex);
        _device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
        _device.SetSamplerState(TextureAddress.Wrap);
        _device.SetCullMode(Cull.None);
        _device.SetZEnable(false);
        _device.SetAlphaBlendEnable(false);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        StopAutoPan();
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
        foreach (var t in _modelTextures.Values) t?.Dispose();
        _modelTextures.Clear();
        foreach (var t in _voxelTextures.Values) t?.Dispose();
        _voxelTextures.Clear();
        _loadedModelCache.Clear();
        _loadedVoxelCache.Clear();
        _placeholderTex?.Dispose();
        _linesVb?.Dispose();
        _thingDirVb?.Dispose();
        _thingsVb?.Dispose();
        _selVertsVb?.Dispose();
        _commentIconsVb?.Dispose();
        _drawVb?.Dispose();
        _gridVb?.Dispose();
        _blockmapVb?.Dispose();
        _blockmapFillVb?.Dispose();
        _nodesVb?.Dispose();
        _nodePolygonsVb?.Dispose();
        _rejectOverlayVb?.Dispose();
        _visplaneOverlayVb?.Dispose();
        _soundLeakPathVb?.Dispose();
        _soundLeakMarkerVb?.Dispose();
        _wadAuthorVb?.Dispose();
        _wadAuthorFillVb?.Dispose();
        _boxVb?.Dispose();
        _pick3DVb?.Dispose();
        _things3DVb?.Dispose();
        _model3DVb?.Dispose();
        _model3DIb?.Dispose();
        _imageExampleVb?.Dispose();
        _imageExampleTex?.Dispose();
        _shader?.Dispose();
        _device?.Dispose();
        _placeholderTex = null; _linesVb = null; _thingDirVb = null; _thingsVb = null; _selVertsVb = null; _commentIconsVb = null; _drawVb = null; _gridVb = null; _blockmapVb = null; _blockmapFillVb = null; _nodesVb = null; _nodePolygonsVb = null; _rejectOverlayVb = null; _visplaneOverlayVb = null; _soundLeakPathVb = null; _soundLeakMarkerVb = null; _wadAuthorVb = null; _wadAuthorFillVb = null; _boxVb = null; _pick3DVb = null; _things3DVb = null; _model3DVb = null; _model3DIb = null; _imageExampleVb = null; _imageExampleTex = null; _shader = null; _device = null; _gl = null;
    }

    private void InvalidateTextures()
    {
        foreach (var t in _flatTextures.Values) t?.Dispose();
        _flatTextures.Clear();
        foreach (var t in _wallTextures.Values) t?.Dispose();
        _wallTextures.Clear();
        foreach (var t in _spriteTextures.Values) t?.Dispose();
        _spriteTextures.Clear();
        foreach (var t in _modelTextures.Values) t?.Dispose();
        _modelTextures.Clear();
        foreach (var t in _voxelTextures.Values) t?.Dispose();
        _voxelTextures.Clear();
        _loadedVoxelCache.Clear();
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

    // Returns the cached GL texture for a model skin, using ResourceManager's UDB-style texture fallback.
    private DBTexture? GetModelTexture(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_modelTextures.TryGetValue(name, out var cached)) return cached;
        DBTexture? tex = null;
        ImageData? img = _resources?.GetModelTextureImage(name);
        if (img != null && _device != null && _gl != null)
        {
            tex = new DBTexture(_gl);
            tex.SetPixelsRgba8(img.Width, img.Height, img.Rgba, generateMipmaps: false);
            _device.SetTexture(0, tex);
            _device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
            _device.SetSamplerState(TextureAddress.Wrap);
        }

        _modelTextures[name] = tex;
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
        var persp = Matrix4x4.CreatePerspectiveFieldOfView((float)(_visualFovDegrees * Math.PI / 180.0), aspect, 1f, _viewDistance);
        _device.SetUniform("projection", view * persp);

        DrawBuckets3D(_floor3D, wall: false);
        DrawBuckets3D(_ceil3D, wall: false);
        DrawBuckets3D(_wall3D, wall: true);
        DrawThings3D();

        UpdateTarget3D();
        if (IsVisualPaintSelectionActive()) ApplyVisualPaintSelection();
        if (_useHighlight) DrawTargetHighlight3D();
    }

    // Draws things as upright camera-facing sprite billboards (rebuilt each frame; depth-tested against geometry).
    private void DrawThings3D()
    {
        if (_showVisualThings == 0 || _device is null || _map is null || _things3DVb is null) return;
        var right = new Vector3((float)Math.Sin(_yaw), -(float)Math.Cos(_yaw), 0);
        var up = new Vector3(0, 0, 1);

        var buckets = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);
        Gldefs? gldefs = _resources?.GetGldefs();
        foreach (var t in VisibleThings3D())
        {
            ThingTypeInfo? thingInfo = _gameConfig?.GetThing(t.Type);
            if (DrawModelThing3D(t, thingInfo)) continue;
            if (DrawVoxelThing3D(t, thingInfo)) continue;

            ThingBillboardDisplay? display = ThingBillboardDisplayPlanner.Plan(
                thingInfo,
                _resources,
                _modelRenderMode,
                new ThingModelRenderInput(Selected: t.Selected),
                visual3D: true);
            if (display == null || GetSpriteTexture(display.SpriteName) == null) continue;

            Sector? sector = _blockmapCache?.GetSectorAt(t.Position) ?? _map.GetSectorAt(t.Position);
            double floorZ = sector?.GetFloorZ(t.Position) ?? 0;
            ImageData img = display.Image;
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
            int col = VisualThingTint(t, sector, gldefs);
            FlatVertex V(Vector3 p, float u, float v) => new FlatVertex { x = p.X, y = p.Y, z = p.Z, w = 1, c = col, u = u, v = v };

            var bl = c - right * hw - up * hh; var br = c + right * hw - up * hh;
            var tr = c + right * hw + up * hh; var tl = c - right * hw + up * hh;
            if (!buckets.TryGetValue(display.SpriteName, out var list)) { list = new(); buckets[display.SpriteName] = list; }
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

    private bool DrawModelThing3D(Thing thing, ThingTypeInfo? thingInfo)
    {
        if (_device is null || _resources is null || _model3DVb is null || _model3DIb is null) return false;
        if (!ThingModelRenderPlanner.ShouldRender3D(_modelRenderMode, thing.Selected)) return false;
        if (thingInfo is null) return false;

        ThingModelDisplay? display = ThingDisplayResolver.ResolveModel(thingInfo, _resources);
        if (display is null) return false;
        GzLoadedModel? model = LoadModelDisplay(display);
        if (model is null || model.Meshes.Count == 0) return false;

        Sector? sector = _blockmapCache?.GetSectorAt(thing.Position) ?? _map?.GetSectorAt(thing.Position);
        double floorZ = sector?.GetFloorZ(thing.Position) ?? 0;
        ThingModelRenderPlan transform = ThingModelRenderPlanner.Plan3D(
            display,
            new ThingModelRenderInput(
                PositionX: thing.Position.x,
                PositionY: thing.Position.y,
                PositionZ: floorZ + thing.Height,
                ScaleX: thing.ScaleX,
                ScaleY: thing.ScaleY,
                ActorScaleWidth: thingInfo.SpriteScale,
                ActorScaleHeight: thingInfo.SpriteScale,
                AngleRadians: thing.Angle * Math.PI / 180.0,
                PitchRadians: thing.Pitch * Math.PI / 180.0,
                RollRadians: thing.Roll * Math.PI / 180.0,
                Selected: thing.Selected));
        int tint = VisualThingTint(thing, sector, _resources?.GetGldefs());
        IReadOnlyList<GzModelRenderBatch> batches = GzModelRenderPlanner.Plan(model, transform.World3D, tint);
        if (batches.Count == 0) return false;

        return DrawPreparedModelBatches(batches, GetModelTexture, TextureAddress.Wrap);
    }

    private bool DrawVoxelThing3D(Thing thing, ThingTypeInfo? thingInfo)
    {
        if (_device is null || _resources is null || _model3DVb is null || _model3DIb is null) return false;
        if (!ThingModelRenderPlanner.ShouldRender3D(_modelRenderMode, thing.Selected)) return false;
        if (thingInfo is null) return false;

        string? voxelName = ThingDisplayResolver.ResolveVoxel(thingInfo.Sprite, _resources);
        if (voxelName is null) return false;
        GzLoadedModel? model = LoadVoxelDisplay(voxelName);
        if (model is null || model.Meshes.Count == 0) return false;

        Sector? sector = _blockmapCache?.GetSectorAt(thing.Position) ?? _map?.GetSectorAt(thing.Position);
        double floorZ = sector?.GetFloorZ(thing.Position) ?? 0;
        Matrix4x4 transform = ThingModelRenderPlanner.PlanVoxel3D(new ThingModelRenderInput(
            PositionX: thing.Position.x,
            PositionY: thing.Position.y,
            PositionZ: floorZ + thing.Height,
            ScaleX: thing.ScaleX,
            ScaleY: thing.ScaleY,
            ActorScaleWidth: thingInfo.SpriteScale,
            ActorScaleHeight: thingInfo.SpriteScale,
            AngleRadians: thing.Angle * Math.PI / 180.0,
            PitchRadians: thing.Pitch * Math.PI / 180.0,
            RollRadians: thing.Roll * Math.PI / 180.0,
            Selected: thing.Selected));
        int tint = VisualThingTint(thing, sector, _resources?.GetGldefs());
        IReadOnlyList<GzModelRenderBatch> batches = GzModelRenderPlanner.Plan(model, transform, tint);
        if (batches.Count == 0) return false;

        return DrawPreparedModelBatches(batches, GetVoxelTexture, TextureAddress.Clamp);
    }

    private int VisualThingTint(Thing thing, Sector? sector, Gldefs? gldefs)
    {
        int billboardTint = ThingBillboardTint(thing, gldefs);
        return billboardTint == DynamicLightDisplay.DefaultBillboardTint
            ? VisualSurfaceLighting.ThingRenderTint(sector, _fullBrightness, 1.0, _classicRendering)
            : billboardTint;
    }

    private int ThingBillboardTint(Thing thing, Gldefs? gldefs)
    {
        if (thing.Selected) return DynamicLightDisplay.SelectedBillboardTint;
        return ThingLightColor(thing, gldefs) ?? DynamicLightDisplay.DefaultBillboardTint;
    }

    private int? ThingLightColor(Thing thing, Gldefs? gldefs)
        => !_classicRendering && ThingLightRenderPlanner.ShouldRender(_lightRenderMode)
            ? DynamicLightDisplay.ThingColor(thing, _gameConfig, gldefs)
            : null;

    private bool DrawPreparedModelBatches(
        IReadOnlyList<GzModelRenderBatch> batches,
        Func<string?, DBTexture?> textureResolver,
        TextureAddress textureAddress)
    {
        if (_device is null || _model3DVb is null || _model3DIb is null) return false;

        _device.SetAlphaBlendEnable(true);
        _device.SetSourceBlend(Blend.SourceAlpha);
        _device.SetDestinationBlend(Blend.InverseSourceAlpha);
        _device.SetUniform("useTexture", 1f);

        bool drew = false;
        foreach (GzModelRenderBatch batch in batches)
        {
            GzPreparedModelRenderBatch prepared = GzModelRenderPlanner.PrepareVertices(batch);
            if (prepared.Vertices.Count == 0 || prepared.Indices.Count == 0) continue;
            DBTexture? texture = textureResolver(prepared.TexturePath);
            _device.SetBufferData(_model3DVb, prepared.Vertices.ToArray());
            _device.SetBufferData(_model3DIb, prepared.Indices.ToArray());
            _device.SetVertexBuffer(_model3DVb);
            _device.SetIndexBuffer(_model3DIb);
            _device.SetTexture(0, texture ?? _placeholderTex);
            _device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
            _device.SetSamplerState(textureAddress);
            _device.SetUniform("useTexture", texture is null ? 0f : 1f);
            _device.DrawIndexed(DBPrimitiveType.TriangleList, 0, prepared.TriangleCount);
            drew = true;
        }

        _device.SetIndexBuffer(null);
        _device.SetAlphaBlendEnable(false);
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        return drew;
    }

    private DBTexture? GetVoxelTexture(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return _voxelTextures.TryGetValue(name, out DBTexture? cached) ? cached : null;
    }

    private GzLoadedModel? LoadModelDisplay(ThingModelDisplay display)
    {
        string key = ModelDisplayCacheKey(display);
        if (_loadedModelCache.TryGetValue(key, out GzLoadedModel? cached)) return cached;

        ThingModelData data = ThingModelData.FromDisplay(display);
        var request = new GzModelLoadRequest(
            data.Path,
            new ModelLoadVector(data.Scale.X, data.Scale.Y, data.Scale.Z),
            data.ModelNames,
            data.SkinNames,
            data.SurfaceSkinNames,
            data.FrameNames,
            data.FrameIndices);
        GzLoadedModel loaded = GzModelLoadCoordinator.Load(
            request,
            path => _resources?.GetModelResourceBytes(path),
            path => _resources?.GetModelTextureImage(path) != null);

        _loadedModelCache[key] = loaded.Meshes.Count == 0 ? null : loaded;
        return _loadedModelCache[key];
    }

    private GzLoadedModel? LoadVoxelDisplay(string voxelName)
    {
        if (_loadedVoxelCache.TryGetValue(voxelName, out GzLoadedModel? cached)) return cached;
        byte[]? bytes = _resources?.GetVoxelBytes(voxelName);
        if (bytes is null)
        {
            _loadedVoxelCache[voxelName] = null;
            return null;
        }

        KvxModelLoadResult result = KvxModelLoader.Load(bytes, CurrentVoxelPalette());
        if (!string.IsNullOrEmpty(result.Errors) || result.Meshes.Count == 0)
        {
            _loadedVoxelCache[voxelName] = null;
            return null;
        }

        _voxelTextures[voxelName] = UploadVoxelTexture(result);
        var loaded = new GzLoadedModel(
            result.Meshes,
            Enumerable.Repeat<string?>(voxelName, result.Meshes.Count).ToArray(),
            Array.Empty<string>(),
            result.Bounds,
            result.Radius);
        _loadedVoxelCache[voxelName] = loaded;
        return loaded;
    }

    private IReadOnlyList<PixelColor>? CurrentVoxelPalette()
    {
        DoomPalette? palette = _resources?.Palette;
        if (palette is null) return null;

        var colors = new PixelColor[palette.Colors.Length];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = PixelColor.FromArgb(unchecked((int)palette.Colors[i]));
        return colors;
    }

    private DBTexture? UploadVoxelTexture(KvxModelLoadResult result)
    {
        if (_gl is null || result.TextureWidth <= 0 || result.TextureHeight <= 0 || result.TexturePixels.Count == 0)
            return null;

        DBTexture texture = new(_gl);
        texture.SetPixelsRgba8(result.TextureWidth, result.TextureHeight, ToRgba8(result.TexturePixels), generateMipmaps: false);
        _device?.SetTexture(0, texture);
        _device?.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
        _device?.SetSamplerState(TextureAddress.Clamp);
        return texture;
    }

    private static byte[] ToRgba8(IReadOnlyList<PixelColor> pixels)
    {
        var rgba = new byte[pixels.Count * 4];
        for (int i = 0; i < pixels.Count; i++)
        {
            PixelColor pixel = pixels[i];
            int offset = i * 4;
            rgba[offset] = pixel.R;
            rgba[offset + 1] = pixel.G;
            rgba[offset + 2] = pixel.B;
            rgba[offset + 3] = pixel.A;
        }

        return rgba;
    }

    private static string ModelDisplayCacheKey(ThingModelDisplay display)
    {
        ThingModelData data = ThingModelData.FromDisplay(display);
        var parts = new System.Text.StringBuilder(data.Path);
        parts.Append('|')
            .Append(data.Scale.X.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(data.Scale.Y.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(data.Scale.Z.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < data.ModelNames.Count; i++)
        {
            parts.Append('|').Append(data.ModelNames[i])
                .Append('|').Append(data.SkinNames[i])
                .Append('|').Append(data.FrameNames[i])
                .Append('|').Append(data.FrameIndices[i]);
            foreach (var skin in data.SurfaceSkinNames[i].OrderBy(pair => pair.Key))
                parts.Append('|').Append(skin.Key).Append('=').Append(skin.Value);
        }

        return parts.ToString();
    }

    private void CycleVisualThings3D()
    {
        int state = CycleVisualThings();
        Target3DChanged?.Invoke(VisualThingVisibilityStatusText(state));
    }

    private IReadOnlyList<Thing> VisibleThings3D()
    {
        if (_map == null) return Array.Empty<Thing>();
        if (_blockmapCache == null)
            return _map.Things.Where(t => !ThingHidden3D(t)).ToArray();

        var frustum = VisualCulling.CreateFrustum(
            new Vec2D(_cam3DPos.X, _cam3DPos.Y),
            _yaw,
            _pitch,
            near: 1.0,
            far: _viewDistance,
            fovDegrees: _visualFovDegrees);

        return VisualCulling.BuildPlan(
            _blockmapCache,
            frustum,
            includeGeometry: false,
            includeThings: true,
            thingFilter: t => !ThingHidden3D(t)).Things;
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
            new VisualPickingOptions(
                ThingSize: ThingSize3D,
                WallTexture: VisualPickingWallTexture,
                FlatTexture: VisualPickingFlatTexture,
                AlphaBasedTextureHighlighting: _alphaBasedTextureHighlighting));

        string desc = _target3D == null ? "" : _target3D.Kind switch
        {
            VisualHitKind.Floor => $"floor (sector {_target3D.Sector?.Index}, z={_target3D.Sector?.FloorHeight})",
            VisualHitKind.Ceiling => $"ceiling (sector {_target3D.Sector?.Index}, z={_target3D.Sector?.CeilHeight})",
            VisualHitKind.Thing => $"thing {_target3D.Thing?.Type} (z={_target3D.Thing?.Height})",
            _ => $"wall (linedef {(_target3D.Line != null ? _map.Linedefs.IndexOf(_target3D.Line) : -1)})",
        };
        if (desc != _target3DDesc) { _target3DDesc = desc; Target3DChanged?.Invoke(desc); }
    }

    private VisualPickingTexture? VisualPickingWallTexture(string name)
    {
        ImageData? image = _resources?.GetWallTexture(name);
        return VisualPickingTextureFromImage(image);
    }

    private VisualPickingTexture? VisualPickingFlatTexture(string name)
    {
        ImageData? image = _resources?.GetFlat(name);
        return VisualPickingTextureFromImage(image);
    }

    private static VisualPickingTexture? VisualPickingTextureFromImage(ImageData? image)
    {
        if (image == null) return null;

        return new VisualPickingTexture(
            image.Width,
            image.Height,
            (x, y) =>
            {
                int index = (y * image.Width + x) * 4 + 3;
                return index >= 0 && index < image.Rgba.Length && image.Rgba[index] > 0;
            },
            image.ScaleX,
            image.ScaleY);
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
        Target3DChanged?.Invoke(TextureCopied3DStatusText(tex, _target3D?.Kind is VisualHitKind.Floor or VisualHitKind.Ceiling));
    }

    /// <summary>Raised when the 3D user requests the texture browser (true = flats for a floor/ceiling target).</summary>
    public event Action<bool>? BrowseTexturesRequested;

    /// <summary>Raised when the 3D user requests paste-properties options.</summary>
    public event Action? PastePropertiesOptionsRequested;

    /// <summary>Applies a chosen texture name (from the browser) to the current 3D target and remembers it.</summary>
    public void ApplyChosenTexture(string name)
    {
        _texClipboard3D = name;
        ApplyTextureToTarget(name, pasted: false);
    }

    // Applies the 3D texture clipboard onto the targeted surface (undoable).
    private void ApplyTexture3D()
    {
        if (string.IsNullOrEmpty(_texClipboard3D)) { Target3DChanged?.Invoke("no copied texture (use C or T)"); return; }
        ApplyTextureToTarget(_texClipboard3D!, pasted: true);
    }

    private void ApplyTextureToTarget(string tex, bool pasted)
    {
        if (_map == null) return;
        var targets = TextureApplyTargets3D();
        if (targets.Count == 0) { Target3DChanged?.Invoke("aim at a surface to apply texture"); return; }
        EditBegun?.Invoke(TextureApplied3DEditName(tex, targets[^1].Kind, pasted));
        foreach (var h in targets) ApplyTextureToHit(h, tex, _gameConfig?.UseLongTextureNames ?? false);
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(TexturePasted3DStatusText(tex, targets[^1].Kind));
    }

    public static string TexturePasted3DStatusText(string textureName, VisualHitKind kind)
        => kind switch
        {
            VisualHitKind.Floor => $"Pasted flat \"{textureName}\" on floor.",
            VisualHitKind.Ceiling => $"Pasted flat \"{textureName}\" on ceiling.",
            _ => $"Pasted texture \"{textureName}\".",
        };

    public static string TextureApplied3DEditName(string textureName, VisualHitKind kind, bool pasted)
        => pasted
            ? kind switch
            {
                VisualHitKind.Floor => $"Paste floor \"{textureName}\"",
                VisualHitKind.Ceiling => $"Paste ceiling \"{textureName}\"",
                _ => $"Paste texture \"{textureName}\"",
            }
            : kind is VisualHitKind.Floor or VisualHitKind.Ceiling
                ? $"Change flat \"{textureName}\""
                : "Change texture " + textureName;

    public static string TextureCopied3DStatusText(string textureName, bool flat)
        => flat ? $"Copied flat \"{textureName}\"." : $"Copied texture \"{textureName}\".";

    private System.Collections.Generic.List<VisualHit> TextureApplyTargets3D()
        => EditTargets3D()
            .Where(hit => hit.Kind is VisualHitKind.Floor or VisualHitKind.Ceiling or VisualHitKind.Wall)
            .ToList();

    public static void ApplyTextureToHit(VisualHit h, string tex, bool useLongTextureNames)
    {
        if (h.Kind == VisualHitKind.Floor && h.Sector is { } fs)
        {
            fs.SetFloorTexture(tex);
            fs.LongFloorTexture = Lump.MakeLongName(fs.FloorTexture, useLongTextureNames);
        }
        else if (h.Kind == VisualHitKind.Ceiling && h.Sector is { } cs)
        {
            cs.SetCeilTexture(tex);
            cs.LongCeilTexture = Lump.MakeLongName(cs.CeilTexture, useLongTextureNames);
        }
        else if (h.Kind == VisualHitKind.Wall)
        {
            var sd = h.Front ? h.Line?.Front : h.Line?.Back;
            if (sd == null) return;
            sd.SetTexture(h.Part, tex);
            SetLongTextureName(sd, h.Part, useLongTextureNames);
        }
    }

    private static void SetLongTextureName(Sidedef side, SidedefPart part, bool useLongTextureNames)
    {
        switch (part)
        {
            case SidedefPart.Upper:
                side.LongHighTexture = Lump.MakeLongName(side.HighTexture, useLongTextureNames);
                break;
            case SidedefPart.Middle:
                side.LongMiddleTexture = Lump.MakeLongName(side.MidTexture, useLongTextureNames);
                break;
            case SidedefPart.Lower:
                side.LongLowTexture = Lump.MakeLongName(side.LowTexture, useLongTextureNames);
                break;
        }
    }

    private void FloodFillTexture3D()
    {
        if (_map == null) return;
        if (string.IsNullOrEmpty(_texClipboard3D)) { Target3DChanged?.Invoke("no copied texture (use C or T)"); return; }
        if (_target3D is not { } hit) { Target3DChanged?.Invoke("aim at a surface to flood-fill"); return; }

        string fillTexture = _texClipboard3D!;
        if (hit.Kind == VisualHitKind.Floor && hit.Sector is { } floor)
        {
            if (floor.FloorTexture == fillTexture) { Target3DChanged?.Invoke("target already uses copied flat"); return; }
            if (_resources?.GetFlat(fillTexture) == null) { Target3DChanged?.Invoke("copied flat is not loaded"); return; }

            EditBegun?.Invoke(VisualTextureFloodFill3DEditName(hit.Kind, fillTexture));
            Tools.FloodfillFlats(_map, floor, fillCeilings: false, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { floor.FloorTexture }, fillTexture, resetSectorMarks: true);
            FinishFloodFill3D(VisualTextureFloodFill3DStatusText(hit.Kind, fillTexture));
        }
        else if (hit.Kind == VisualHitKind.Ceiling && hit.Sector is { } ceiling)
        {
            if (ceiling.CeilTexture == fillTexture) { Target3DChanged?.Invoke("target already uses copied flat"); return; }
            if (_resources?.GetFlat(fillTexture) == null) { Target3DChanged?.Invoke("copied flat is not loaded"); return; }

            EditBegun?.Invoke(VisualTextureFloodFill3DEditName(hit.Kind, fillTexture));
            Tools.FloodfillFlats(_map, ceiling, fillCeilings: true, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ceiling.CeilTexture }, fillTexture, resetSectorMarks: true);
            FinishFloodFill3D(VisualTextureFloodFill3DStatusText(hit.Kind, fillTexture));
        }
        else if (hit.Kind == VisualHitKind.Wall && hit.Line != null)
        {
            Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
            if (side == null) { Target3DChanged?.Invoke("aim at a wall to flood-fill"); return; }
            string oldTexture = side.GetTexture(hit.Part);
            if (oldTexture == fillTexture) { Target3DChanged?.Invoke("target already uses copied texture"); return; }
            if (_resources?.GetWallTexture(fillTexture) == null) { Target3DChanged?.Invoke("copied texture is not loaded"); return; }

            EditBegun?.Invoke(VisualTextureFloodFill3DEditName(hit.Kind, fillTexture));
            Tools.FloodfillTextures(_map, side, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { oldTexture }, fillTexture, resetSideMarks: true);
            FinishFloodFill3D(VisualTextureFloodFill3DStatusText(hit.Kind, fillTexture));
        }
        else
        {
            Target3DChanged?.Invoke("aim at a surface to flood-fill");
        }
    }

    private void FinishFloodFill3D(string status)
    {
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(status);
    }

    public static string VisualTextureFloodFill3DStatusText(VisualHitKind kind, string textureName)
        => kind switch
        {
            VisualHitKind.Floor => "Flood-filled floors with " + textureName + ".",
            VisualHitKind.Ceiling => "Flood-filled ceilings with " + textureName + ".",
            _ => "Flood-filled textures with " + textureName + ".",
        };

    public static string VisualTextureFloodFill3DEditName(VisualHitKind kind, string textureName)
        => kind switch
        {
            VisualHitKind.Floor => "Flood-fill floors with " + textureName,
            VisualHitKind.Ceiling => "Flood-fill ceilings with " + textureName,
            _ => "Flood-fill textures with " + textureName,
        };

    // The sidedef on the camera-facing side of the targeted wall, or null.
    private Sidedef? TargetSidedef3D()
    {
        var h = _target3D;
        if (h == null || h.Kind != VisualHitKind.Wall || h.Line == null) return null;
        return h.Front ? h.Line.Front : h.Line.Back;
    }

    private (Sidedef Side, SidedefPart Part)? TargetSidedefPart3D()
    {
        var h = _target3D;
        if (h == null || h.Kind != VisualHitKind.Wall || h.Line == null) return null;
        Sidedef? side = h.Front ? h.Line.Front : h.Line.Back;
        return side == null ? null : (side, h.Part);
    }

    private System.Collections.Generic.List<Sidedef> TextureOffsetTargets3D()
    {
        var targets = new System.Collections.Generic.List<Sidedef>();
        var seen = new System.Collections.Generic.HashSet<Sidedef>();
        foreach (VisualHit hit in EditTargets3D())
        {
            if (hit.Kind != VisualHitKind.Wall || hit.Line == null) continue;
            Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
            if (side != null && seen.Add(side)) targets.Add(side);
        }

        return targets;
    }

    private System.Collections.Generic.List<(Sidedef Side, SidedefPart Part)> TextureOffsetPartTargets3D()
    {
        var targets = new System.Collections.Generic.List<(Sidedef Side, SidedefPart Part)>();
        var seen = new System.Collections.Generic.HashSet<(Sidedef Side, SidedefPart Part)>();
        foreach (VisualHit hit in EditTargets3D())
        {
            if (hit.Kind != VisualHitKind.Wall || hit.Line == null) continue;
            Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
            if (side != null && seen.Add((side, hit.Part))) targets.Add((side, hit.Part));
        }

        return targets;
    }

    private System.Collections.Generic.List<(Sector Sector, bool Ceiling)> FlatTextureOffsetTargets3D()
    {
        var targets = new System.Collections.Generic.List<(Sector Sector, bool Ceiling)>();
        var seen = new System.Collections.Generic.HashSet<(Sector Sector, bool Ceiling)>();
        foreach (VisualHit hit in EditTargets3D())
        {
            if (hit.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling) || hit.Sector == null) continue;
            bool ceiling = hit.Kind == VisualHitKind.Ceiling;
            if (seen.Add((hit.Sector, ceiling))) targets.Add((hit.Sector, ceiling));
        }

        return targets;
    }

    private System.Collections.Generic.List<Linedef> WallLineTargets3D()
    {
        var targets = new System.Collections.Generic.List<Linedef>();
        var seen = new System.Collections.Generic.HashSet<Linedef>();
        foreach (VisualHit hit in EditTargets3D())
            if (hit.Kind == VisualHitKind.Wall && hit.Line != null && seen.Add(hit.Line)) targets.Add(hit.Line);
        return targets;
    }

    private System.Collections.Generic.List<(Sidedef Side, SidedefPart Part)> SelectedWallTextureParts3D()
    {
        var targets = new System.Collections.Generic.List<(Sidedef Side, SidedefPart Part)>();
        var seen = new System.Collections.Generic.HashSet<(Sidedef Side, SidedefPart Part)>();
        foreach (VisualHit hit in _sel3D)
        {
            if (hit.Kind != VisualHitKind.Wall || hit.Line == null || hit.Part == SidedefPart.None) continue;
            Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
            if (side != null && seen.Add((side, hit.Part))) targets.Add((side, hit.Part));
        }

        return targets;
    }

    // Nudges the targeted or selected walls and flats' texture offsets, undoable.
    private void NudgeTargetOffset3D(int deltaX, int deltaY)
    {
        bool localOffsets = _mapFormat == MapFormat.Udmf && _gameConfig?.UseLocalSidedefTextureOffsets == true;
        var wallPartTargets = TextureOffsetPartTargets3D();
        var flatTargets = FlatTextureOffsetTargets3D();
        if (wallPartTargets.Count == 0 && flatTargets.Count == 0) { Target3DChanged?.Invoke("aim at a surface to offset its texture"); return; }

        int changed = 0;
        bool begun = false;
        string offsetStatus = string.Empty;
        var seenGlobalSides = new System.Collections.Generic.HashSet<Sidedef>();
        foreach ((Sidedef side, SidedefPart part) in wallPartTargets)
        {
            if (!localOffsets && !seenGlobalSides.Add(side)) continue;
            if (!begun) { EditBegun?.Invoke(VisualTextureOffset3DEditName()); begun = true; }
            int? textureWidth = null;
            int? textureHeight = null;
            if (_resources?.GetWallTexture(side.GetTexture(part)) is { } image)
            {
                textureWidth = image.Width;
                textureHeight = image.Height;
            }

            if (VisualSidedefTextureOffsets.Nudge(side, part, deltaX, deltaY, localOffsets, textureWidth, textureHeight))
            {
                changed++;
                var offsets = VisualSidedefTextureOffsets.Copy(side, part, localOffsets);
                offsetStatus = VisualTextureOffset3DStatusText(VisualHitKind.Wall, offsets.X, offsets.Y);
            }
        }

        if (_mapFormat == MapFormat.Udmf)
        {
            foreach ((Sector sector, bool ceiling) in flatTargets)
            {
                string textureName = ceiling ? sector.CeilTexture : sector.FloorTexture;
                var image = _resources?.GetFlat(textureName);
                if (image == null)
                {
                    continue;
                }

                if (!begun) { EditBegun?.Invoke(VisualTextureOffset3DEditName()); begun = true; }
                if (VisualFlatOffset.Nudge(
                    sector,
                    ceiling,
                    deltaX,
                    deltaY,
                    image.Width * image.ScaleX,
                    image.Height * image.ScaleY,
                    _yaw))
                {
                    changed++;
                    offsetStatus = VisualTextureOffset3DStatusText(
                        ceiling ? VisualHitKind.Ceiling : VisualHitKind.Floor,
                        sector.GetFloatField(ceiling ? "xpanningceiling" : "xpanningfloor", 0.0),
                        sector.GetFloatField(ceiling ? "ypanningceiling" : "ypanningfloor", 0.0));
                }
            }
        }

        if (changed == 0)
        {
            Target3DChanged?.Invoke(_mapFormat == MapFormat.Udmf
                ? "no texture dimensions for selected flat offsets"
                : VisualFlatTextureOffsetUnsupportedMapFormatMessage());
            return;
        }

        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(offsetStatus);
    }

    public static string VisualTextureOffset3DStatusText(VisualHitKind kind, double x, double y)
    {
        string offsets = x.ToString(CultureInfo.InvariantCulture) + ", " + y.ToString(CultureInfo.InvariantCulture) + ".";
        return kind switch
        {
            VisualHitKind.Floor => "Changed floor texture offsets to " + offsets,
            VisualHitKind.Ceiling => "Changed ceiling texture offsets to " + offsets,
            _ => "Changed texture offsets to " + offsets,
        };
    }

    public static string VisualTextureOffset3DEditName()
        => "Change texture offsets";

    public static string VisualFlatTextureOffsetUnsupportedMapFormatMessage()
        => "Floor/ceiling texture offsets cannot be changed in this map format!";

    private static bool IsLineFlagSet3D(Linedef line, string flag)
    {
        if (string.IsNullOrWhiteSpace(flag) || flag == "0") return false;
        if (int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit))
            return bit != 0 && (line.Flags & bit) == bit;
        return line.IsFlagSet(flag);
    }

    private static void SetLineFlag3D(Linedef line, string flag, bool value)
    {
        if (string.IsNullOrWhiteSpace(flag) || flag == "0") return;
        if (int.TryParse(flag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit))
        {
            if (value) line.Flags |= bit;
            else line.Flags &= ~bit;
            return;
        }

        line.SetFlag(flag, value);
    }

    private void ToggleUnpegged3D(bool upper)
    {
        string flag = upper ? _gameConfig?.UpperUnpeggedFlag ?? "0" : _gameConfig?.LowerUnpeggedFlag ?? "0";
        if (string.IsNullOrWhiteSpace(flag) || flag == "0")
        {
            Target3DChanged?.Invoke($"{(upper ? "upper" : "lower")} unpegged flag is not configured");
            return;
        }

        Sidedef? targetSide = TargetSidedef3D();
        if (targetSide == null) { Target3DChanged?.Invoke("aim at a wall to toggle unpegged"); return; }

        var targets = WallLineTargets3D();
        if (targets.Count == 0) targets.Add(targetSide.Line);

        bool next = !IsLineFlagSet3D(targetSide.Line, flag);
        EditBegun?.Invoke(VisualUnpegged3DEditName(upper, next));
        foreach (Linedef line in targets) SetLineFlag3D(line, flag, next);
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(VisualUnpegged3DStatusText(upper, next));
    }

    public static string VisualUnpegged3DStatusText(bool upper, bool set)
        => (set ? "Set " : "Removed ") + (upper ? "upper" : "lower") + "-unpegged setting.";

    public static string VisualUnpegged3DEditName(bool upper, bool set)
        => (set ? "Set " : "Remove ") + (upper ? "upper" : "lower") + "-unpegged setting";

    private void CopyTextureOffsets3D()
    {
        if (TargetSidedefPart3D() is not { } target) { Target3DChanged?.Invoke("aim at a wall to copy offsets"); return; }
        bool localOffsets = _mapFormat == MapFormat.Udmf && _gameConfig?.UseLocalSidedefTextureOffsets == true;
        _texOffsetClipboard3D = VisualSidedefTextureOffsets.Copy(target.Side, target.Part, localOffsets);
        Target3DChanged?.Invoke(TextureOffsetsCopied3DStatusText(_texOffsetClipboard3D.Value.X, _texOffsetClipboard3D.Value.Y));
    }

    private void PasteTextureOffsets3D()
    {
        if (_texOffsetClipboard3D is not { } offsets) { Target3DChanged?.Invoke("no copied offsets"); return; }
        bool localOffsets = _mapFormat == MapFormat.Udmf && _gameConfig?.UseLocalSidedefTextureOffsets == true;
        var partTargets = localOffsets ? TextureOffsetPartTargets3D() : new System.Collections.Generic.List<(Sidedef Side, SidedefPart Part)>();
        var sideTargets = localOffsets ? new System.Collections.Generic.List<Sidedef>() : TextureOffsetTargets3D();
        int targetCount = localOffsets ? partTargets.Count : sideTargets.Count;
        if (targetCount == 0) { Target3DChanged?.Invoke("aim at a wall to paste offsets"); return; }

        EditBegun?.Invoke(TextureOffsetsPasted3DEditName());
        foreach ((Sidedef side, SidedefPart part) in partTargets)
            VisualSidedefTextureOffsets.Paste(side, part, offsets, localOffsets);
        foreach (Sidedef side in sideTargets)
            VisualSidedefTextureOffsets.Paste(side, SidedefPart.None, offsets, useLocalOffsets: false);

        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(TextureOffsetsPasted3DStatusText(offsets.X, offsets.Y));
    }

    public static string TextureOffsetsCopied3DStatusText(int x, int y)
        => $"Copied texture offsets {x}, {y}.";

    public static string TextureOffsetsPasted3DStatusText(int x, int y)
        => $"Pasted texture offsets {x}, {y}.";

    public static string TextureOffsetsPasted3DEditName()
        => "Paste texture offsets";

    private void FitSelectedVisualTextures3D()
    {
        var targets = SelectedWallTextureParts3D();
        if (targets.Count == 0) { Target3DChanged?.Invoke("Fit Textures action requires selected sidedefs."); return; }
        if (_resources == null) { Target3DChanged?.Invoke("no resources loaded for texture dimensions"); return; }

        int changed = 0;
        int skipped = 0;
        EditBegun?.Invoke(VisualFitTexture3DEditName(fitWidth: true, fitHeight: true));
        foreach ((Sidedef side, SidedefPart part) in targets)
        {
            string textureName = side.GetTexture(part);
            if (IsBlankTexture(textureName)) continue;

            var image = _resources.GetWallTexture(textureName);
            if (image == null)
            {
                skipped++;
                continue;
            }

            bool fitted = SidedefTextureFitting.Fit(
                side,
                part,
                new TextureFitImage(image.Width, image.Height, image.ScaleX, image.ScaleY));
            if (fitted) changed++;
        }

        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(changed == 0
            ? $"no selected wall textures fitted ({skipped} missing image{(skipped == 1 ? "" : "s")})"
            : $"fit {changed} wall texture{(changed == 1 ? "" : "s")} ({skipped} missing image{(skipped == 1 ? "" : "s")})");
    }

    public static string VisualFitTexture3DEditName(bool fitWidth, bool fitHeight)
    {
        string axis = fitWidth && fitHeight ? "width and height" : fitWidth ? "width" : "height";
        return "Fit texture (" + axis + ")";
    }

    private void ChangeVisualScale3D(int incrementX, int incrementY)
    {
        if (_map == null) return;
        if (_mapFormat != MapFormat.Udmf) return;

        int changed = 0;
        int skipped = 0;
        bool begun = false;
        string scaleStatus = string.Empty;
        var seenThings = new HashSet<Thing>();
        var seenWalls = new HashSet<(Sidedef Side, SidedefPart Part)>();
        var seenFlats = new HashSet<(Sector Sector, VisualHitKind Kind)>();

        foreach (VisualHit hit in EditTargets3D())
        {
            if (hit.Kind == VisualHitKind.Thing && hit.Thing is { } thing && seenThings.Add(thing))
            {
                var size = ThingSize3D(thing);
                if (!begun) { EditBegun?.Invoke(VisualScale3DEditName(hit.Kind)); begun = true; }
                bool adjusted = VisualScaleAdjustment.AdjustThing(
                    thing,
                    incrementX,
                    incrementY,
                    Math.Max(1, (int)Math.Round(size.radius * 2.0)),
                    Math.Max(1, (int)Math.Round(size.height)));
                if (adjusted)
                {
                    changed++;
                    scaleStatus = VisualScale3DStatusText(
                        VisualHitKind.Thing,
                        thing.ScaleX,
                        thing.ScaleY,
                        Math.Max(1, (int)Math.Round(size.radius * 2.0)),
                        Math.Max(1, (int)Math.Round(size.height)));
                }
            }
            else if (hit.Kind == VisualHitKind.Wall && hit.Line != null && hit.Part != SidedefPart.None)
            {
                Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
                if (side == null || !seenWalls.Add((side, hit.Part))) continue;
                string textureName = side.GetTexture(hit.Part);
                var image = _resources?.GetWallTexture(textureName);
                if (image == null)
                {
                    skipped++;
                    continue;
                }

                if (!begun) { EditBegun?.Invoke(VisualScale3DEditName(hit.Kind)); begun = true; }
                bool adjusted = VisualScaleAdjustment.AdjustWall(side, hit.Part, incrementX, incrementY, image.Width, image.Height);
                if (adjusted)
                {
                    changed++;
                    var scale = WallScale3D(side, hit.Part);
                    scaleStatus = VisualScale3DStatusText(hit.Kind, scale.X, scale.Y, image.Width, image.Height);
                }
            }
            else if (hit.Kind is VisualHitKind.Floor or VisualHitKind.Ceiling && hit.Sector is { } sector && seenFlats.Add((sector, hit.Kind)))
            {
                bool ceiling = hit.Kind == VisualHitKind.Ceiling;
                string textureName = ceiling ? sector.CeilTexture : sector.FloorTexture;
                var image = _resources?.GetFlat(textureName);
                if (image == null)
                {
                    skipped++;
                    continue;
                }

                if (!begun) { EditBegun?.Invoke(VisualScale3DEditName(hit.Kind)); begun = true; }
                bool adjusted = VisualScaleAdjustment.AdjustFlatForView(sector, ceiling, incrementX, incrementY, image.Width, image.Height, _yaw);
                if (adjusted)
                {
                    changed++;
                    var scale = FlatScale3D(sector, ceiling);
                    scaleStatus = VisualScale3DStatusText(hit.Kind, scale.X, scale.Y, image.Width, image.Height);
                }
            }
        }

        if (changed == 0)
        {
            Target3DChanged?.Invoke(skipped == 0 ? "aim at a surface or thing to scale" : "no texture dimensions for selected visual scale");
            return;
        }

        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(scaleStatus);
    }

    public static string VisualScale3DStatusText(VisualHitKind kind, double scaleX, double scaleY, int width, int height)
    {
        int displayWidth = kind == VisualHitKind.Thing
            ? (int)Math.Round(width * scaleX)
            : (int)Math.Round(width / scaleX);
        int displayHeight = kind == VisualHitKind.Thing
            ? (int)Math.Round(height * scaleY)
            : (int)Math.Round(height / scaleY);
        string scale = $"{scaleX.ToString("F03", CultureInfo.InvariantCulture)}, {scaleY.ToString("F03", CultureInfo.InvariantCulture)} ({displayWidth} x {displayHeight}).";

        return kind switch
        {
            VisualHitKind.Floor => "Floor scale changed to " + scale,
            VisualHitKind.Ceiling => "Ceiling scale changed to " + scale,
            VisualHitKind.Thing => "Changed thing scale to " + scale,
            _ => "Wall scale changed to " + scale,
        };
    }

    public static string VisualScale3DEditName(VisualHitKind kind)
        => kind switch
        {
            VisualHitKind.Thing => "Change thing scale",
            VisualHitKind.Wall => "Change wall scale",
            _ => "Change texture scale",
        };

    private static (double X, double Y) FlatScale3D(Sector sector, bool ceiling)
        => (
            sector.GetFloatField(ceiling ? "xscaleceiling" : "xscalefloor", 1.0),
            sector.GetFloatField(ceiling ? "yscaleceiling" : "yscalefloor", 1.0));

    private static (double X, double Y) WallScale3D(Sidedef side, SidedefPart part)
        => part switch
        {
            SidedefPart.Upper => (side.GetFloatField("scalex_top", 1.0), side.GetFloatField("scaley_top", 1.0)),
            SidedefPart.Lower => (side.GetFloatField("scalex_bottom", 1.0), side.GetFloatField("scaley_bottom", 1.0)),
            _ => (side.GetFloatField("scalex_mid", 1.0), side.GetFloatField("scaley_mid", 1.0)),
        };

    private void ResetVisualTexture3D(bool local)
    {
        if (_map == null) return;

        int changed = 0;
        bool begun = false;
        string resetStatus = VisualTextureReset3DStatusText(VisualHitKind.Wall, local);
        var seenSectors = new HashSet<(Sector Sector, VisualHitKind Kind)>();
        var seenSides = new HashSet<Sidedef>();
        var seenParts = new HashSet<(Sidedef Side, SidedefPart Part)>();
        var seenThings = new HashSet<Thing>();

        foreach (VisualHit hit in EditTargets3D())
        {
            if (hit.Kind is VisualHitKind.Floor or VisualHitKind.Ceiling && hit.Sector is { } sector && seenSectors.Add((sector, hit.Kind)))
            {
                if (!begun) { EditBegun?.Invoke(VisualTextureReset3DEditName(hit.Kind, local)); begun = true; }
                bool ceiling = hit.Kind == VisualHitKind.Ceiling;
                if (VisualTextureReset.ResetSectorFlat(sector, ceiling, local)) changed++;
                resetStatus = VisualTextureReset3DStatusText(hit.Kind, local);
            }
            else if (hit.Kind == VisualHitKind.Wall && hit.Line != null)
            {
                Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
                if (side == null) continue;

                if (local)
                {
                    if (hit.Part == SidedefPart.None || !seenParts.Add((side, hit.Part))) continue;
                    if (!begun) { EditBegun?.Invoke(VisualTextureReset3DEditName(hit.Kind, local)); begun = true; }
                    if (VisualTextureReset.ResetSidedefForCommand(side, hit.Part, local: true, isUdmf: _mapFormat == MapFormat.Udmf)) changed++;
                    resetStatus = VisualTextureReset3DStatusText(hit.Kind, local);
                }
                else if (seenSides.Add(side))
                {
                    if (!begun) { EditBegun?.Invoke(VisualTextureReset3DEditName(hit.Kind, local)); begun = true; }
                    if (VisualTextureReset.ResetSidedefOffsets(side)) changed++;
                    resetStatus = VisualTextureReset3DStatusText(hit.Kind, local);
                }
            }
            else if (hit.Kind == VisualHitKind.Thing && hit.Thing is { } thing && seenThings.Add(thing))
            {
                if (!begun) { EditBegun?.Invoke(VisualTextureReset3DEditName(hit.Kind, local)); begun = true; }
                if (VisualTextureReset.ResetThing(thing, local)) changed++;
                resetStatus = VisualTextureReset3DStatusText(hit.Kind, local);
            }
        }

        if (!begun)
        {
            Target3DChanged?.Invoke("aim at a surface or thing to reset");
            return;
        }

        if (changed > 0)
        {
            _geo3DDirty = true;
            MarkGeometryDirty();
            Changed?.Invoke();
            RequestNextFrameRendering();
        }

        Target3DChanged?.Invoke(resetStatus);
    }

    public static string VisualTextureReset3DStatusText(VisualHitKind kind, bool local)
        => kind switch
        {
            VisualHitKind.Thing => local ? "Thing scale, pitch and roll reset." : "Thing scale reset.",
            VisualHitKind.Floor or VisualHitKind.Ceiling => local ? "Texture offsets, scale, rotation and brightness reset." : "Texture offsets reset.",
            _ => local ? "Local texture offsets, scale and brightness reset." : "Texture offsets reset.",
        };

    public static string VisualTextureReset3DEditName(VisualHitKind kind, bool local)
        => kind switch
        {
            VisualHitKind.Thing => local ? "Reset thing scale, pitch and roll" : "Reset thing scale",
            VisualHitKind.Floor or VisualHitKind.Ceiling => local ? "Reset texture offsets, scale, rotation and brightness" : "Reset texture offsets",
            _ => local ? "Reset local texture offsets, scale and brightness" : "Reset texture offsets",
        };

    // Resets the targeted wall's texture offsets to zero, undoable.
    private void ResetTargetOffsets3D()
    {
        if (TargetSidedef3D() is not { } sd) { Target3DChanged?.Invoke("aim at a wall to reset offsets"); return; }
        EditBegun?.Invoke(VisualTextureReset3DEditName(VisualHitKind.Wall, false));
        sd.OffsetX = 0;
        sd.OffsetY = 0;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke("Texture offsets reset.");
    }

    private void ToggleSlope3D()
    {
        if (_map == null) return;

        var targets = new List<VisualSlopeToggleTarget>();
        foreach (VisualHit hit in EditTargets3D())
        {
            switch (hit.Kind)
            {
                case VisualHitKind.Floor:
                    if (hit.Sector != null) targets.Add(new VisualSlopeToggleTarget(Sector: hit.Sector, Floor: true));
                    break;
                case VisualHitKind.Ceiling:
                    if (hit.Sector != null) targets.Add(new VisualSlopeToggleTarget(Sector: hit.Sector, Ceiling: true));
                    break;
                case VisualHitKind.Wall:
                    if (hit.Line != null)
                    {
                        Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
                        if (side != null) targets.Add(new VisualSlopeToggleTarget(Sidedef: side, Part: hit.Part));
                    }
                    break;
            }
        }

        if (targets.Count == 0)
        {
            Target3DChanged?.Invoke(VisualSlopeToggleEmptySelectionMessage());
            return;
        }

        EditBegun?.Invoke("Toggle Slope");
        VisualSlopeToggleResult result = VisualSlopeToggle.Toggle(targets);
        if (result.Changed)
        {
            _geo3DDirty = true;
            MarkGeometryDirty();
            Changed?.Invoke();
            RequestNextFrameRendering();
            ClearSelection3D();
        }

        Target3DChanged?.Invoke(result.StatusMessage);
    }

    public static string VisualSlopeToggleEmptySelectionMessage()
        => "Toggle Slope action requires selected surfaces!";

    private void ResetSlope3D()
    {
        if (_map == null) return;

        var targets = new List<VisualSlopeResetTarget>();
        foreach (VisualHit hit in EditTargets3D())
        {
            if (hit.Kind == VisualHitKind.Floor && hit.Sector != null)
                targets.Add(new VisualSlopeResetTarget(hit.Sector, Ceiling: false));
            else if (hit.Kind == VisualHitKind.Ceiling && hit.Sector != null)
                targets.Add(new VisualSlopeResetTarget(hit.Sector, Ceiling: true));
        }

        if (targets.Count == 0)
        {
            Target3DChanged?.Invoke(VisualSlopeReset.EmptySelectionMessage);
            return;
        }

        EditBegun?.Invoke("Reset plane slope");
        VisualSlopeResetResult result = VisualSlopeReset.Reset(targets);
        if (result.Changed)
        {
            _geo3DDirty = true;
            MarkGeometryDirty();
            Changed?.Invoke();
            RequestNextFrameRendering();
        }

        Target3DChanged?.Invoke(result.StatusMessage);
    }

    private void ApplyVisualSlopeBetweenHandles3D()
    {
        IReadOnlyList<VisualSlopeLevel> levels = SelectedVisualSlopeLevels3D();
        IReadOnlyList<VisualSlopeHandle> selectedHandles = SelectedVisualSlopeLineHandles3D();
        VisualSlopeHandle? highlightedHandle = HighlightedVisualSlopeLineHandle3D();
        IReadOnlyList<VisualSlopeHandle> handles = VisualSlopeLineHandlesForActions3D(selectedHandles, highlightedHandle);
        if (levels.Count > 0 && CanResolveVisualSlopeLineHandlePair(selectedHandles, highlightedHandle, handles))
            EditBegun?.Invoke("Slope between handles");

        VisualSlopeBetweenHandlesApplyResult result = VisualSlopeHandles.ApplySlopeBetweenSelectedHandles(
            levels,
            handles,
            highlightedHandle,
            useOppositeSmartPivotHandle: _useOppositeSmartPivotHandle);
        ApplyVisualSlopeBetweenHandlesResult(result);
    }

    private void ApplyVisualArchBetweenHandles3D()
    {
        IReadOnlyList<VisualSlopeLevel> levels = SelectedVisualSlopeLevels3D();
        IReadOnlyList<VisualSlopeHandle> selectedHandles = SelectedVisualSlopeLineHandles3D();
        VisualSlopeHandle? highlightedHandle = HighlightedVisualSlopeLineHandle3D();
        IReadOnlyList<VisualSlopeHandle> handles = VisualSlopeLineHandlesForActions3D(selectedHandles, highlightedHandle);
        if (levels.Count >= 2 && CanResolveVisualSlopeLineHandlePair(selectedHandles, highlightedHandle, handles))
            EditBegun?.Invoke("Arch between slope handles");

        VisualSlopeBetweenHandlesApplyResult result = VisualSlopeHandles.ApplyArchBetweenSelectedHandles(
            levels,
            handles,
            highlightedHandle,
            useOppositeSmartPivotHandle: _useOppositeSmartPivotHandle);
        ApplyVisualSlopeBetweenHandlesResult(result);
    }

    private void ApplyVisualSlopeHandleNearestHeight3D(bool raise)
    {
        IReadOnlyList<VisualSlopeLevel> levels = SelectedVisualSlopeLevels3D();
        IReadOnlyList<VisualSlopeHandle> handles = SelectedVisualSlopeLineHandles3D();
        if (handles.Count == 1)
            EditBegun?.Invoke(raise ? "Raise slope handle to nearest" : "Lower slope handle to nearest");

        VisualSlopeNearestHandleApplyResult result = raise
            ? VisualSlopeHandles.RaiseSelectedSlopeHandleToNearest(handles, affectedLevels: levels)
            : VisualSlopeHandles.LowerSelectedSlopeHandleToNearest(handles, affectedLevels: levels);
        ApplyVisualSlopeNearestHeightResult(result);
    }

    private void ApplyVisualSlopeNearestHeightResult(VisualSlopeNearestHandleApplyResult result)
    {
        if (result.Result == VisualSlopeNearestHandleResult.Changed && result.ChangedLevels > 0)
        {
            _geo3DDirty = true;
            MarkGeometryDirty();
            Changed?.Invoke();
            RequestNextFrameRendering();
        }

        Target3DChanged?.Invoke(result.StatusMessage);
    }

    private static bool CanResolveVisualSlopeLineHandlePair(
        IReadOnlyList<VisualSlopeHandle> handles,
        VisualSlopeHandle? highlightedHandle,
        IReadOnlyList<VisualSlopeHandle> actionHandles)
        => handles.Count == 2
           || (handles.Count == 1 && highlightedHandle != null && !ReferenceEquals(handles[0].Sidedef, highlightedHandle.Sidedef))
           || (handles.Count == 0 && highlightedHandle != null && actionHandles.Count > 1);

    private void ApplyVisualSlopeBetweenHandlesResult(VisualSlopeBetweenHandlesApplyResult result)
    {
        if (result.Result == VisualSlopeBetweenHandlesResult.Changed && result.ChangedLevels > 0)
        {
            _geo3DDirty = true;
            MarkGeometryDirty();
            Changed?.Invoke();
            RequestNextFrameRendering();
        }

        Target3DChanged?.Invoke(result.StatusMessage);
    }

    private IReadOnlyList<VisualSlopeLevel> SelectedVisualSlopeLevels3D()
    {
        var levels = new List<VisualSlopeLevel>();
        var seen = new HashSet<(Sector Sector, VisualSlopeLevelType Type)>();
        foreach (VisualHit hit in _sel3D)
        {
            if (hit.Kind == VisualHitKind.Floor && hit.Sector is { } floor && seen.Add((floor, VisualSlopeLevelType.Floor)))
                levels.Add(VisualSlopeLevel.Floor(floor));
            else if (hit.Kind == VisualHitKind.Ceiling && hit.Sector is { } ceiling && seen.Add((ceiling, VisualSlopeLevelType.Ceiling)))
                levels.Add(VisualSlopeLevel.Ceiling(ceiling));
        }

        return levels;
    }

    private IReadOnlyList<VisualSlopeHandle> SelectedVisualSlopeLineHandles3D()
    {
        var handles = new List<VisualSlopeHandle>();
        var seen = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance);
        foreach (VisualHit hit in _sel3D)
        {
            if (VisualSlopeLineHandleFromHit(hit, selected: true) is { } handle && handle.Sidedef != null && seen.Add(handle.Sidedef))
                handles.Add(handle);
        }

        return handles;
    }

    private VisualSlopeHandle? HighlightedVisualSlopeLineHandle3D()
        => _target3D is { } target
            ? VisualSlopeLineHandleFromHit(target, selected: false)
            : null;

    private static IReadOnlyList<VisualSlopeHandle> VisualSlopeLineHandlesForActions3D(
        IReadOnlyList<VisualSlopeHandle> selectedHandles,
        VisualSlopeHandle? highlightedHandle)
    {
        var handles = new List<VisualSlopeHandle>();
        var seen = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance);

        foreach (VisualSlopeHandle handle in selectedHandles)
            AddVisualSlopeActionHandle(handle, handles, seen);

        if (highlightedHandle != null)
            AddVisualSlopeActionHandle(highlightedHandle, handles, seen);

        foreach (VisualSlopeHandle handle in handles.ToArray())
        {
            foreach (Sidedef side in handle.Level.Sector.Sidedefs)
                AddVisualSlopeActionHandle(
                    VisualSlopeHandles.CreateSidedef(side, handle.Level, up: true),
                    handles,
                    seen);
        }

        return handles;
    }

    private static void AddVisualSlopeActionHandle(
        VisualSlopeHandle handle,
        List<VisualSlopeHandle> handles,
        HashSet<Sidedef> seen)
    {
        if (handle.Kind != VisualSlopeHandleKind.Line || handle.Sidedef == null || !seen.Add(handle.Sidedef)) return;
        handles.Add(handle);
    }

    private static VisualSlopeHandle? VisualSlopeLineHandleFromHit(VisualHit hit, bool selected)
    {
        if (hit.Kind != VisualHitKind.Wall || hit.Line == null) return null;
        Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
        Sector? sector = hit.Sector ?? side?.Sector;
        if (side == null || sector == null) return null;

        VisualSlopeLevel level = VisualSlopeLevelForWallHit(hit, sector);
        bool up = hit.Point.z >= WallHitMidpointZ(hit, sector);
        return VisualSlopeHandles.CreateSidedef(side, level, up) with { Selected = selected };
    }

    private static VisualSlopeLevel VisualSlopeLevelForWallHit(VisualHit hit, Sector sector)
    {
        Vec2D position = new(hit.Point.x, hit.Point.y);
        double floorZ = sector.GetFloorZ(position);
        double ceilingZ = sector.GetCeilZ(position);
        return Math.Abs(hit.Point.z - ceilingZ) < Math.Abs(hit.Point.z - floorZ)
            ? VisualSlopeLevel.Ceiling(sector)
            : VisualSlopeLevel.Floor(sector);
    }

    private static double WallHitMidpointZ(VisualHit hit, Sector sector)
    {
        Vec2D position = new(hit.Point.x, hit.Point.y);
        return (sector.GetFloorZ(position) + sector.GetCeilZ(position)) * 0.5;
    }

    // Deletes selected or targeted visual things, or clears selected or targeted surface textures.
    private void DeleteVisualTargets3D()
    {
        if (_map == null) return;

        var targets = EditTargets3D();
        if (targets.Count == 0) { Target3DChanged?.Invoke("aim at a surface or thing to delete"); return; }

        int textures = 0;
        int things = 0;
        bool begun = false;
        string deleteStatus = string.Empty;
        var seenSectors = new HashSet<(Sector Sector, VisualHitKind Kind)>();
        var seenParts = new HashSet<(Sidedef Side, SidedefPart Part)>();
        var seenThings = new HashSet<Thing>();

        foreach (VisualHit hit in targets)
        {
            if (hit.Kind == VisualHitKind.Floor && hit.Sector is { } floor && seenSectors.Add((floor, hit.Kind)))
            {
                if (!begun) { EditBegun?.Invoke(VisualDelete3DEditName(hit.Kind)); begun = true; }
                floor.SetFloorTexture("-");
                textures++;
                deleteStatus = VisualDelete3DStatusText(VisualHitKind.Floor);
            }
            else if (hit.Kind == VisualHitKind.Ceiling && hit.Sector is { } ceiling && seenSectors.Add((ceiling, hit.Kind)))
            {
                if (!begun) { EditBegun?.Invoke(VisualDelete3DEditName(hit.Kind)); begun = true; }
                ceiling.SetCeilTexture("-");
                textures++;
                deleteStatus = VisualDelete3DStatusText(VisualHitKind.Ceiling);
            }
            else if (hit.Kind == VisualHitKind.Wall && hit.Line != null)
            {
                Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
                if (side == null || hit.Part == SidedefPart.None || !seenParts.Add((side, hit.Part))) continue;
                if (!begun) { EditBegun?.Invoke(VisualDelete3DEditName(hit.Kind)); begun = true; }
                side.SetTexture(hit.Part, "-");
                textures++;
                deleteStatus = VisualDelete3DStatusText(VisualHitKind.Wall);
            }
            else if (hit.Kind == VisualHitKind.Thing && hit.Thing is { } thing && seenThings.Add(thing))
            {
                if (!begun) { EditBegun?.Invoke(VisualDelete3DEditName(hit.Kind)); begun = true; }
                _map.RemoveThing(thing);
                things++;
                deleteStatus = VisualDelete3DStatusText(VisualHitKind.Thing);
            }
        }

        if (!begun)
        {
            Target3DChanged?.Invoke("aim at a surface or thing to delete");
            return;
        }

        if (things > 0) _map.BuildIndexes();
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        ClearSelection3D();
        Target3DChanged?.Invoke(deleteStatus);
    }

    public static string VisualDelete3DStatusText(VisualHitKind kind)
        => kind == VisualHitKind.Thing ? "Deleted a thing." : "Deleted a texture.";

    public static string VisualDelete3DEditName(VisualHitKind kind)
        => kind == VisualHitKind.Thing ? "Delete thing" : "Delete texture";

    // Auto-aligns textures along the targeted wall's run, undoable.
    private void AutoAlignSide3D(Sidedef side, bool alignX, bool alignY, string editName)
    {
        string tex = SidedefTextureAlignment.PrimaryTexture(side);
        var img = _resources?.GetWallTexture(tex);
        EditBegun?.Invoke(editName);
        if (alignX) SidedefTextureAlignment.AutoAlignX(side, img?.Width ?? 0);
        if (alignY) SidedefTextureAlignment.AutoAlignY(side, img?.Height ?? 0);
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(VisualAutoAlign3DStatusText(alignX, alignY, selected: false));
    }

    private void AutoAlignTarget3D(bool alignX, bool alignY)
    {
        if (TargetSidedef3D() is not { } sd)
        {
            if (AutoAlignFlatTargets3D(alignX, alignY)) return;
            Target3DChanged?.Invoke("aim at a wall or UDMF flat to align textures");
            return;
        }

        AutoAlignSide3D(sd, alignX, alignY, VisualAutoAlign3DEditName(alignX, alignY, selected: false));
    }

    private bool AutoAlignFlatTargets3D(bool alignX, bool alignY)
    {
        if (_mapFormat != MapFormat.Udmf) return false;
        if (_target3D is not { Kind: VisualHitKind.Floor or VisualHitKind.Ceiling, Sector: { } targetSector } target)
            return false;

        bool ceiling = target.Kind == VisualHitKind.Ceiling;
        string targetTexture = ceiling ? targetSector.CeilTexture : targetSector.FloorTexture;
        var candidates = targetSector.Sidedefs.Select(side => side.Line).Distinct().ToList();
        if (candidates.Count == 0) return false;

        int changed = 0;
        bool begun = false;
        foreach (VisualHit hit in EditTargets3D())
        {
            if (hit.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling) || hit.Sector is not { } sector) continue;
            bool hitCeiling = hit.Kind == VisualHitKind.Ceiling;
            string textureName = hitCeiling ? sector.CeilTexture : sector.FloorTexture;
            if (!string.Equals(textureName, targetTexture, StringComparison.OrdinalIgnoreCase)) continue;

            var image = _resources?.GetFlat(textureName);
            var texture = image == null ? (SectorFlatAlignmentTexture?)null : new SectorFlatAlignmentTexture(image.Width, image.Height);
            if (!begun) { EditBegun?.Invoke(VisualAutoAlign3DEditName(alignX, alignY, selected: false)); begun = true; }
            SectorFlatAlignmentResult result = SectorFlatAlignment.AlignToClosestLine(
                sector,
                candidates,
                new Vector2D(target.Point.x, target.Point.y),
                floors: !hitCeiling,
                alignX: alignX,
                alignY: alignY,
                texture: texture);
            if (result.Applied) changed++;
        }

        if (changed == 0) return false;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(VisualAutoAlign3DStatusText(alignX, alignY, selected: false));
        return true;
    }

    private void AutoAlignSelectedVisualTextures3D(bool alignX, bool alignY)
    {
        var targets = SelectedWallTextureParts3D();
        if (targets.Count == 0) { Target3DChanged?.Invoke("select wall surfaces to align textures"); return; }

        var seen = new System.Collections.Generic.HashSet<Sidedef>();
        EditBegun?.Invoke(VisualAutoAlign3DEditName(alignX, alignY, selected: true));
        foreach ((Sidedef side, _) in targets)
        {
            if (!seen.Add(side)) continue;
            string tex = SidedefTextureAlignment.PrimaryTexture(side);
            var img = _resources?.GetWallTexture(tex);
            if (alignX) SidedefTextureAlignment.AutoAlignX(side, img?.Width ?? 0);
            if (alignY) SidedefTextureAlignment.AutoAlignY(side, img?.Height ?? 0);
        }

        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(VisualAutoAlign3DStatusText(alignX, alignY, selected: true));
    }

    public static string VisualAutoAlign3DStatusText(bool alignX, bool alignY, bool selected)
    {
        string axis = alignX && alignY ? "X and Y" : alignX ? "X" : "Y";
        return selected
            ? "Auto-aligned textures to selected sidedefs (" + axis + ")."
            : "Auto-aligned textures (" + axis + ").";
    }

    public static string VisualAutoAlign3DEditName(bool alignX, bool alignY, bool selected)
    {
        string axis = alignX && alignY ? "X and Y" : alignX ? "X" : "Y";
        return selected
            ? "Auto-align textures to selected sidedefs (" + axis + ")"
            : "Auto-align textures (" + axis + ")";
    }

    // Adjusts the selected (or targeted) sectors' brightness ([ darker / ] brighter), undoable.
    private void AdjustTargetBrightness3D(bool raise)
    {
        if (_map == null) return;
        var done = new System.Collections.Generic.HashSet<Sector>();
        var doneCeilings = new System.Collections.Generic.HashSet<Sector>();
        var doneSides = new System.Collections.Generic.HashSet<(Sidedef Side, SidedefPart Part)>();
        IReadOnlyList<int> brightnessLevels = _gameConfig?.BrightnessLevels ?? [];
        bool begun = false;
        string brightnessStatus = string.Empty;
        foreach (var h in EditTargets3D())
        {
            Sidedef? wallSide = h.Kind == VisualHitKind.Wall && h.Line != null
                ? (h.Front ? h.Line.Front : h.Line.Back)
                : null;
            if (wallSide != null && !doneSides.Add((wallSide, h.Part))) continue;
            if (h.Kind == VisualHitKind.Wall && AdjustVisualWallBrightness3D(h, raise, brightnessLevels, _mapFormat, _gameConfig, out brightnessStatus, mapInfo: CurrentMapInfo()))
            {
                if (!begun) { EditBegun?.Invoke(VisualBrightness3DEditName(h.Kind)); begun = true; }
                continue;
            }

            if (h.Kind == VisualHitKind.Ceiling &&
                h.Sector != null &&
                doneCeilings.Add(h.Sector) &&
                AdjustVisualCeilingBrightness3D(h, raise, brightnessLevels, _mapFormat, _gameConfig, out brightnessStatus))
            {
                if (!begun) { EditBegun?.Invoke(VisualBrightness3DEditName(h.Kind)); begun = true; }
                continue;
            }

            if (h.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling or VisualHitKind.Wall)) continue;
            if (h.Sector is not { } s || !done.Add(s)) continue; // each sector once
            if (!begun) { EditBegun?.Invoke(VisualBrightness3DEditName(VisualHitKind.Floor)); begun = true; }
            s.Brightness = raise
                ? SectorBrightnessAdjustment.NextHigher(brightnessLevels, s.Brightness)
                : SectorBrightnessAdjustment.NextLower(brightnessLevels, s.Brightness);
            brightnessStatus = VisualBrightness3DStatusText(VisualHitKind.Floor, s.Brightness);
        }
        if (!begun) return;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(brightnessStatus);
    }

    public static string VisualBrightness3DStatusText(VisualHitKind kind, int brightness)
        => kind switch
        {
            VisualHitKind.Ceiling => "Changed ceiling brightness to " + brightness + ".",
            VisualHitKind.Wall => "Changed wall brightness to " + brightness + ".",
            _ => "Changed sector brightness to " + brightness + ".",
        };

    public static string VisualBrightness3DEditName(VisualHitKind kind)
        => kind switch
        {
            VisualHitKind.Ceiling => "Change ceiling brightness",
            VisualHitKind.Wall => "Change wall brightness",
            _ => "Change sector brightness",
        };

    public static bool AdjustVisualCeilingBrightness3D(
        VisualHit hit,
        bool raise,
        IReadOnlyList<int> brightnessLevels,
        MapFormat mapFormat,
        GameConfiguration? config,
        out string status)
    {
        status = string.Empty;
        if (hit.Kind != VisualHitKind.Ceiling || hit.Sector == null) return false;
        if (mapFormat != MapFormat.Udmf || config?.DistinctFloorAndCeilingBrightness != true) return false;

        bool absolute = hit.Sector.GetField("lightceilingabsolute", false);
        int current = hit.Sector.GetIntegerField("lightceiling");
        int next = raise
            ? SectorBrightnessAdjustment.NextHigher(brightnessLevels, current, absolute)
            : SectorBrightnessAdjustment.NextLower(brightnessLevels, current, absolute);
        if (next == current) return false;

        hit.Sector.SetIntegerField("lightceiling", next, absolute ? int.MinValue : 0);
        status = VisualBrightness3DStatusText(VisualHitKind.Ceiling, next);
        return true;
    }

    public static bool AdjustVisualWallBrightness3D(
        VisualHit hit,
        bool raise,
        IReadOnlyList<int> brightnessLevels,
        MapFormat mapFormat,
        GameConfiguration? config,
        out string status,
        MapInfoEntry? mapInfo = null)
    {
        status = string.Empty;
        if (hit.Kind != VisualHitKind.Wall || hit.Line == null || hit.Sector == null) return false;

        Sidedef? side = hit.Front ? hit.Line.Front : hit.Line.Back;
        if (side?.Sector == null) return false;

        bool distinctWallBrightness = mapFormat == MapFormat.Udmf &&
            (config?.DistinctWallBrightness == true || config?.DistinctSidedefPartBrightness == true);
        if (!distinctWallBrightness) return false;

        string field = config?.DistinctSidedefPartBrightness == true
            ? "light_" + SidedefPartBrightnessName(hit.Part)
            : "light";
        string absoluteField = config?.DistinctSidedefPartBrightness == true
            ? "lightabsolute_" + SidedefPartBrightnessName(hit.Part)
            : "lightabsolute";
        bool absolute = side.GetField(absoluteField, false);
        int current = side.GetIntegerField(field);
        int next = raise
            ? SectorBrightnessAdjustment.NextHigher(brightnessLevels, current, absolute)
            : SectorBrightnessAdjustment.NextLower(brightnessLevels, current, absolute);
        if (next == current) return false;

        side.SetIntegerField(field, next, absolute ? int.MinValue : 0);
        SidedefFogTools.UpdateLightFogFlag(side, mapInfo, config);
        status = VisualBrightness3DStatusText(VisualHitKind.Wall, next);
        return true;
    }

    private static string SidedefPartBrightnessName(SidedefPart part) => part switch
    {
        SidedefPart.Upper => "top",
        SidedefPart.Middle => "mid",
        SidedefPart.Lower => "bottom",
        _ => "mid",
    };

    private void MatchBrightness3D()
    {
        if (_map == null) return;
        if (_mapFormat != MapFormat.Udmf)
        {
            Target3DChanged?.Invoke("'Match Brightness' action works only in UDMF map format!");
            return;
        }

        if (_sel3D.Count == 0)
        {
            Target3DChanged?.Invoke("'Match Brightness' action requires a selection!");
            return;
        }

        int targetBrightness = 0;
        string message = VisualBrightnessMatch.InvalidTargetMessage;
        if (_target3D is not { } target ||
            !VisualBrightnessMatch.TryReadTargetBrightness(target, out targetBrightness, out message))
        {
            Target3DChanged?.Invoke(string.IsNullOrWhiteSpace(message) ? VisualBrightnessMatch.InvalidTargetMessage : message);
            return;
        }

        EditBegun?.Invoke("Match Brightness");
        VisualBrightnessMatchResult result = VisualBrightnessMatch.Apply(targetBrightness, _sel3D, target, _gameConfig, CurrentMapInfo());
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(result.Message);
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
        Target3DChanged?.Invoke(SurfaceSelection3DStatusText(_sel3D.Count));
        RequestNextFrameRendering();
    }

    public static string SurfaceSelection3DStatusText(int surfaceCount)
        => $"{CountLabel(surfaceCount, "surface")} selected";

    private void BeginVisualPaintSelection()
    {
        _visualPaintSelectHighlight = null;
        ActionStateChanged?.Invoke();
    }

    private void EndVisualPaintSelection()
    {
        _visualPaintSelectHighlight = null;
        ActionStateChanged?.Invoke();
    }

    private bool IsVisualPaintSelectionActive()
        => _heldMapCommands.Contains("map3d.visual-paint-select") || _heldMapCommands.Contains("map3d.visualpaintselect");

    private void ApplyVisualPaintSelection()
    {
        if (_target3D is not { } target) return;
        if (_visualPaintSelectHighlight != null && SameSurface3D(_visualPaintSelectHighlight, target)) return;

        bool add = _visualPaintSelectModifiers.HasFlag(KeyModifiers.Shift);
        bool remove = _visualPaintSelectModifiers.HasFlag(KeyModifiers.Control) || _visualPaintSelectModifiers.HasFlag(KeyModifiers.Meta);
        int index = _sel3D.FindIndex(hit => SameSurface3D(hit, target));
        if (add || (!remove && index < 0))
        {
            if (index < 0) _sel3D.Add(target);
        }
        else if (index >= 0)
        {
            _sel3D.RemoveAt(index);
        }

        _visualPaintSelectHighlight = target;
        Target3DChanged?.Invoke(SurfaceSelection3DStatusText(_sel3D.Count));
        RequestNextFrameRendering();
    }

    public static string VisualThingVisibilityStatusText(int state)
    {
        string label = state switch
        {
            0 => "OFF",
            1 => "SPRITE ONLY",
            _ => "ON",
        };
        return $"Thing visibility is now {label}.";
    }

    public static string VisualThingSelectionStatusText(string verb, int count)
        => $"{verb} {CountLabel(count, "thing")}.";

    public static string VisualThingSelectionEditName(string verb, int count)
        => VisualThingSelectionStatusText(verb, count);

    public static string VisualThingInsertedStatusText()
        => "Inserted a new thing.";

    public static string VisualMiddleTextureCreatedStatusText()
        => "Created middle texture.";

    public static bool TryCreateVisualMiddleTexture3D(VisualHit target, string defaultWallTexture, bool useLongTextureNames)
    {
        if (!CanCreateVisualMiddleTexture3D(target, out Sidedef? side)) return false;

        side!.SetTextureMid(defaultWallTexture);
        side.LongMiddleTexture = Lump.MakeLongName(side.MidTexture, useLongTextureNames);
        if (side.Other != null && IsBlankTexture(side.Other.MidTexture))
        {
            side.Other.SetTextureMid(defaultWallTexture);
            side.Other.LongMiddleTexture = Lump.MakeLongName(side.Other.MidTexture, useLongTextureNames);
        }
        return true;
    }

    private static bool CanCreateVisualMiddleTexture3D(VisualHit target, out Sidedef? side)
    {
        side = target.Front ? target.Line?.Front : target.Line?.Back;
        return target.Kind == VisualHitKind.Wall
            && side != null
            && !side.MiddleRequired()
            && IsBlankTexture(side.MidTexture);
    }

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

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

    public IReadOnlyList<Thing> SelectedVisualThingsForActions()
    {
        var result = new System.Collections.Generic.List<Thing>();
        var seen = new HashSet<Thing>();
        foreach (VisualHit hit in _sel3D)
            if (hit.Thing is { } selected && seen.Add(selected))
                result.Add(selected);

        return result;
    }

    public IReadOnlyList<VisualHit> SelectedVisualSurfacesForActions()
        => _sel3D
            .Where(hit => hit.Kind is VisualHitKind.Floor or VisualHitKind.Ceiling or VisualHitKind.Wall)
            .ToList();

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
        string heightStatus = string.Empty;
        foreach (var h in EditTargets3D())
        {
            string? editLabel = VisualHeight3DEditName(h.Kind);
            if (editLabel == null) continue;
            if (!any) { EditBegun?.Invoke(editLabel); any = true; }
            ApplyHeightDelta(h, step);
            heightStatus = VisualHeight3DStatusText(h);
        }
        if (!any) return;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(heightStatus);
    }

    private void AdjustTargetToNearest3D(bool raise, bool withinSelection)
    {
        if (_map == null) return;
        var targets = EditTargets3D();
        if (targets.Count == 0)
        {
            Target3DChanged?.Invoke(VisualNearestHeight.NoSuitableObjectsMessage);
            return;
        }

        EditBegun?.Invoke(raise ? "Raise to nearest" : "Lower to nearest");
        VisualNearestHeightResult result = VisualNearestHeight.Apply(
            targets,
            raise,
            withinSelection,
            _gameConfig?.HasThingHeight == true);
        if (result.ChangedSurfaces == 0)
        {
            Target3DChanged?.Invoke(result.Message);
            return;
        }

        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(result.Message);
    }

    private static string? HeightEditLabel(VisualHit h) => VisualHeight3DEditName(h.Kind);

    public static string? VisualHeight3DEditName(VisualHitKind kind) => kind switch
    {
        VisualHitKind.Floor => "Change floor height",
        VisualHitKind.Ceiling => "Change ceiling height",
        VisualHitKind.Thing => "Change thing height",
        _ => null,
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

    public static string VisualHeight3DStatusText(VisualHit hit)
        => hit.Kind switch
        {
            VisualHitKind.Floor when hit.Sector != null => "Changed floor height to " + hit.Sector.FloorHeight + ".",
            VisualHitKind.Ceiling when hit.Sector != null => "Changed ceiling height to " + hit.Sector.CeilHeight + ".",
            VisualHitKind.Thing when hit.Thing != null => "Changed thing height to " + hit.Thing.Height.ToString(CultureInfo.InvariantCulture) + ".",
            _ => string.Empty,
        };

    // The surfaces a right-drag affects: the selection if any, otherwise the captured target.
    private System.Collections.Generic.List<VisualHit> DragTargets3D()
        => _sel3D.Count > 0 ? _sel3D : (_drag3DTarget != null ? new() { _drag3DTarget } : new());

    private System.Collections.Generic.List<Thing> ThingTargets3D()
    {
        var result = new System.Collections.Generic.List<Thing>();
        var seen = new HashSet<Thing>();
        foreach (VisualHit hit in _sel3D)
            if (hit.Thing is { } selected && seen.Add(selected))
                result.Add(selected);

        if (result.Count == 0 && _target3D?.Thing is { } target)
            result.Add(target);

        return result;
    }

    private System.Collections.Generic.List<Thing> SelectedThings3D()
    {
        var result = new System.Collections.Generic.List<Thing>();
        var seen = new HashSet<Thing>();
        foreach (VisualHit hit in _sel3D)
            if (hit.Thing is { } selected && seen.Add(selected))
                result.Add(selected);

        if (result.Count == 0 && _target3D?.Thing is { } target)
            result.Add(target);

        return result;
    }

    private bool MoveThingTargets3D(Vector2D direction)
    {
        var things = ThingTargets3D();
        if (things.Count == 0) return false;

        DBuilder.Geometry.Vector3D[] coordinates = things
            .Select(thing => new DBuilder.Geometry.Vector3D(thing.Position, thing.Height))
            .ToArray();
        IReadOnlyList<DBuilder.Geometry.Vector3D> translated = VisualThingMovement.TranslateRelative(
            coordinates,
            direction,
            _yaw + Angle2D.PIHALF);

        EditBegun?.Invoke(things.Count == 1 ? "Move thing" : $"Move {things.Count} things");
        for (int i = 0; i < things.Count; i++)
            things[i].Move(translated[i]);

        _blockmapCache = null;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(VisualThingPosition3DStatusText(things[^1]));
        return true;
    }

    private bool PlaceThingTargetsAtCursor3D()
    {
        var things = ThingTargets3D();
        if (_target3D is not { } target)
        {
            Target3DChanged?.Invoke("Cannot place Thing here");
            return false;
        }

        if (things.Count == 0) return false;

        DBuilder.Geometry.Vector3D[] coordinates = things
            .Select(thing => new DBuilder.Geometry.Vector3D(thing.Position, thing.Height))
            .ToArray();
        var cursor = new Vector2D(Math.Round(target.Point.x), Math.Round(target.Point.y));
        IReadOnlyList<DBuilder.Geometry.Vector3D> translated = VisualThingMovement.TranslateToCursor(coordinates, cursor);

        EditBegun?.Invoke(things.Count == 1 ? "Move thing" : $"Move {things.Count} things");
        for (int i = 0; i < things.Count; i++)
            things[i].Move(translated[i]);

        _blockmapCache = null;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(VisualThingPosition3DStatusText(things[^1]));
        return true;
    }

    public static string VisualThingPosition3DStatusText(Thing thing)
        => "Changed thing position to " + new DBuilder.Geometry.Vector3D(thing.Position, thing.Height) + ".";

    private bool InsertThingAtTarget3D()
    {
        if (_target3D is not { } target)
        {
            Target3DChanged?.Invoke("Cannot insert thing here!");
            return false;
        }

        if (CanCreateVisualMiddleTexture3D(target, out _))
        {
            EditBegun?.Invoke("Create middle texture");
            TryCreateVisualMiddleTexture3D(target, DefaultWallTexture3D(), _gameConfig?.UseLongTextureNames ?? false);
            _geo3DDirty = true;
            MarkGeometryDirty();
            Changed?.Invoke();
            Target3DChanged?.Invoke(VisualMiddleTextureCreatedStatusText());
            RequestNextFrameRendering();
            return true;
        }

        InsertThingAt(new Vec2D(target.Point.x, target.Point.y), snap: false, height: target.Point.z);
        _blockmapCache = null;
        _geo3DDirty = true;
        Target3DChanged?.Invoke(VisualThingInsertedStatusText());
        RequestNextFrameRendering();
        return true;
    }

    private string DefaultWallTexture3D()
        => FirstNonBlankOr("-", _mapOptions?.DefaultWallTexture, _gameConfig?.DefaultWallTexture);

    private bool CopyVisualThingSelection3D()
    {
        var things = SelectedThings3D();
        if (things.Count == 0)
        {
            Target3DChanged?.Invoke("nothing selected to copy");
            return false;
        }

        byte[]? data = WithTemporaryThingSelection(things, () => _map == null ? null : SelectionClipboard.CopySelection(_map));
        if (data == null)
        {
            Target3DChanged?.Invoke("nothing selected to copy");
            return false;
        }

        _visualThingClipboard = data;
        _clipboard = data;
        Target3DChanged?.Invoke(VisualThingSelectionStatusText("Copied", things.Count));
        return true;
    }

    private bool CutVisualThingSelection3D()
    {
        if (_map == null) return false;
        var things = SelectedThings3D();
        if (things.Count == 0)
        {
            Target3DChanged?.Invoke("nothing selected to cut");
            return false;
        }

        if (!CopyVisualThingSelection3D()) return false;

        var selected = new HashSet<Thing>(things, ReferenceEqualityComparer.Instance);
        EditBegun?.Invoke(VisualThingSelectionEditName("Cut", things.Count));
        _map.Things.RemoveAll(thing => selected.Contains(thing));
        _sel3D.RemoveAll(hit => hit.Thing != null && selected.Contains(hit.Thing));
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        Target3DChanged?.Invoke(VisualThingSelectionStatusText("Cut", things.Count));
        return true;
    }

    private bool PasteVisualThingSelection3D()
    {
        if (_map == null) return false;
        if (_target3D is not { } target)
        {
            Target3DChanged?.Invoke("Cannot paste here!");
            return false;
        }

        if (_visualThingClipboard == null)
        {
            Target3DChanged?.Invoke("visual clipboard empty");
            return false;
        }

        EditBegun?.Invoke(VisualThingSelectionEditName("Paste", ClipboardThingCount(_visualThingClipboard)));
        PasteResult result = SelectionClipboard.Paste(_map, _visualThingClipboard, new Vec2D(0, 0), PasteOptions, _gameConfig);
        var pasted = new List<Thing>();
        for (int i = result.FirstThing; i < result.FirstThing + result.ThingCount; i++)
            pasted.Add(_map.Things[i]);

        if (pasted.Count > 0)
        {
            DBuilder.Geometry.Vector3D[] coordinates = pasted
                .Select(thing => new DBuilder.Geometry.Vector3D(thing.Position, thing.Height))
                .ToArray();
            var cursor = new Vector2D(Math.Round(target.Point.x), Math.Round(target.Point.y));
            IReadOnlyList<DBuilder.Geometry.Vector3D> translated = VisualThingMovement.TranslateToCursor(coordinates, cursor);

            for (int i = 0; i < pasted.Count; i++)
                pasted[i].Move(translated[i]);
        }

        _sel3D.Clear();
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        Target3DChanged?.Invoke(VisualThingSelectionStatusText("Pasted", pasted.Count));
        return true;
    }

    private static int ClipboardThingCount(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadInt32();
        return reader.ReadInt32();
    }

    private T WithTemporaryThingSelection<T>(IReadOnlyList<Thing> things, Func<T> action)
    {
        if (_map == null) return action();

        var vertices = _map.GetSelectedVertices();
        var linedefs = _map.GetSelectedLinedefs();
        var sidedefs = _map.GetSelectedSidedefs();
        var sectors = _map.GetSelectedSectors();
        var priorThings = _map.GetSelectedThings();
        _map.ClearAllSelected();
        try
        {
            foreach (Thing thing in things)
                thing.Selected = true;

            return action();
        }
        finally
        {
            _map.ClearAllSelected();
            foreach (Vertex vertex in vertices) vertex.Selected = true;
            foreach (Linedef linedef in linedefs) linedef.Selected = true;
            foreach (Sidedef sidedef in sidedefs) sidedef.Selected = true;
            foreach (Sector sector in sectors) sector.Selected = true;
            foreach (Thing thing in priorThings) thing.Selected = true;
        }
    }

    private bool RotateThingTargets3D(int angleIncrement)
    {
        var things = ThingTargets3D();
        if (!BeginThingOrientationChange3D(things, VisualThingOrientationEditName("angle"))) return false;

        VisualThingRotation.Rotate(things, angleIncrement, _gameConfig?.DoomThingRotationAngles ?? false);
        FinishThingOrientationChange3D(things, "angle");
        return true;
    }

    private bool RotateVisualTargets3D(int thingAngleIncrement, int textureAngleIncrement)
    {
        var targets = EditTargets3D();
        if (targets.Count == 0) return false;

        var things = new List<Thing>();
        var seenThings = new HashSet<Thing>();
        foreach (VisualHit hit in targets)
        {
            if (hit.Kind == VisualHitKind.Thing && hit.Thing is { } thing && seenThings.Add(thing))
                things.Add(thing);
        }

        int thingCount = things.Count;
        int flatCount = CountFlatRotationTargets3D(targets);
        if (thingCount == 0 && flatCount == 0) return false;

        if (thingCount > 0 && flatCount == 0)
            BeginThingOrientationChange3D(things, VisualThingOrientationEditName("angle"));
        else
            EditBegun?.Invoke(thingCount > 0 ? "Rotate things and textures" : flatCount == 1 ? "Rotate texture" : "Rotate textures");

        thingCount = VisualThingRotation.Rotate(things, thingAngleIncrement, _gameConfig?.DoomThingRotationAngles ?? false);
        flatCount = VisualFlatRotation.Rotate(targets, textureAngleIncrement, _mapFormat == MapFormat.Udmf);

        if (thingCount > 0 && flatCount == 0)
        {
            FinishThingOrientationChange3D(things, "angle");
            return true;
        }

        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(VisualRotation3DStatusFromTargets(targets));
        return true;
    }

    private int CountFlatRotationTargets3D(IEnumerable<VisualHit> targets)
    {
        if (_mapFormat != MapFormat.Udmf) return 0;

        var seen = new HashSet<(Sector Sector, bool Ceiling)>();
        foreach (VisualHit hit in targets)
        {
            if (hit.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling) || hit.Sector == null) continue;
            seen.Add((hit.Sector, hit.Kind == VisualHitKind.Ceiling));
        }

        return seen.Count;
    }

    public static string VisualRotation3DStatusText(VisualHitKind kind, double angle)
        => (kind == VisualHitKind.Ceiling ? "Ceiling" : "Floor") + " rotation changed to " + angle.ToString(CultureInfo.InvariantCulture);

    private static string VisualRotation3DStatusFromTargets(IEnumerable<VisualHit> targets)
    {
        string status = string.Empty;
        var seen = new HashSet<(Sector Sector, bool Ceiling)>();
        foreach (VisualHit hit in targets)
        {
            if (hit.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling) || hit.Sector == null) continue;

            bool ceiling = hit.Kind == VisualHitKind.Ceiling;
            if (!seen.Add((hit.Sector, ceiling))) continue;

            double angle = hit.Sector.GetFloatField(ceiling ? "rotationceiling" : "rotationfloor", 0.0);
            status = VisualRotation3DStatusText(hit.Kind, angle);
        }

        return status;
    }

    private bool ChangeThingPitchTargets3D(int increment)
    {
        var things = ThingTargets3D();
        if (!BeginThingOrientationChange3D(things, VisualThingOrientationEditName("pitch"))) return false;

        VisualThingRotation.ChangePitch(things, increment);
        FinishThingOrientationChange3D(things, "pitch");
        return true;
    }

    private bool ChangeThingRollTargets3D(int increment)
    {
        var things = ThingTargets3D();
        if (!BeginThingOrientationChange3D(things, VisualThingOrientationEditName("roll"))) return false;

        VisualThingRotation.ChangeRoll(things, increment);
        FinishThingOrientationChange3D(things, "roll");
        return true;
    }

    private bool ApplyCameraRotationToSelectedThings3D()
    {
        var things = SelectedThings3D();
        if (things.Count == 0)
        {
            Target3DChanged?.Invoke("Can't apply camera rotation to things: no things selected.");
            return false;
        }

        EditBegun?.Invoke(VisualCameraRotationEditName());
        VisualThingRotation.ApplyCameraRotation(things, _yaw, _pitch, _mapFormat == MapFormat.Udmf);
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke($"Applied camera rotation and pitch to {things.Count} thing{(things.Count == 1 ? "" : "s")}.");
        return true;
    }

    public static string VisualCameraRotationEditName()
        => "Apply camera rotation to things";

    private bool LookThroughSelectedThing3D()
    {
        var things = SelectedThings3D();
        if (_map == null || things.Count != 1)
        {
            Target3DChanged?.Invoke("Look Through Selection action requires 1 selected Thing!");
            return false;
        }

        VisualCameraPose pose = VisualCameraMovement.LookThroughThing(
            things[0],
            _map.Things,
            ThingCenter3D,
            _mapFormat == MapFormat.Udmf);
        _cam3DPos = new Vector3((float)pose.Position.x, (float)pose.Position.y, (float)pose.Position.z);
        _yaw = pose.Yaw;
        _pitch = pose.Pitch;
        RequestNextFrameRendering();
        if (pose.StatusMessage != null) Target3DChanged?.Invoke(pose.StatusMessage);
        return true;
    }

    private bool AlignSelectedVisualThingsToWall3D()
    {
        var things = SelectedThings3D();
        if (_map == null) return false;
        if (things.Count == 0)
        {
            Target3DChanged?.Invoke("This action requires selected Things!");
            return false;
        }

        if (_gameConfig == null)
        {
            Target3DChanged?.Invoke("no game configuration");
            return false;
        }

        ThingWallAlignmentResult result = ThingWallAlignment.AlignThingsToNearestWalls(_map, _gameConfig, things);
        if (result.EligibleCount == 0)
        {
            Target3DChanged?.Invoke(result.Message);
            return false;
        }

        EditBegun?.Invoke(things.Count == 1 ? "Align thing" : $"Align {things.Count} things");
        _blockmapCache = null;
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(result.Message);
        return result.AlignedCount > 0;
    }

    private DBuilder.Geometry.Vector3D ThingCenter3D(Thing thing)
    {
        var sector = _blockmapCache?.GetSectorAt(thing.Position) ?? _map?.GetSectorAt(thing.Position);
        double floorZ = sector?.GetFloorZ(thing.Position) ?? 0;
        double height = ThingSize3D(thing).height;
        return new DBuilder.Geometry.Vector3D(thing.Position, floorZ + thing.Height + height * 0.5);
    }

    private bool BeginThingOrientationChange3D(
        IReadOnlyList<Thing> things,
        string editLabel)
    {
        if (things.Count == 0) return false;
        EditBegun?.Invoke(editLabel);
        return true;
    }

    private void FinishThingOrientationChange3D(IReadOnlyList<Thing> things, string orientation)
    {
        _geo3DDirty = true;
        MarkGeometryDirty();
        Changed?.Invoke();
        RequestNextFrameRendering();
        Target3DChanged?.Invoke(VisualThingOrientation3DStatusText(things[^1], orientation));
    }

    public static string VisualThingOrientation3DStatusText(Thing thing, string orientation)
        => orientation switch
        {
            "pitch" => "Changed thing pitch to " + thing.Pitch + ".",
            "roll" => "Changed thing roll to " + thing.Roll + ".",
            _ => "Changed thing angle to " + thing.Angle + ".",
        };

    public static string VisualThingOrientationEditName(string orientation)
        => orientation switch
        {
            "pitch" => "Change thing pitch",
            "roll" => "Change thing roll",
            _ => "Change thing angle",
        };

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

    private bool TryOrbit3D(double dx, double dy)
    {
        if (_orbit3DPoint == null)
        {
            UpdateTarget3D();
            if (_target3D is not { } target) return false;
            _orbit3DPoint = target.Point;
        }

        var current = new DBuilder.Geometry.Vector3D(_cam3DPos.X, _cam3DPos.Y, _cam3DPos.Z);
        if (!VisualCameraMovement.TryOrbit(current, _orbit3DPoint.Value, dx, dy, out VisualCameraPose pose)) return false;

        _cam3DPos = new Vector3((float)pose.Position.x, (float)pose.Position.y, (float)pose.Position.z);
        _yaw = pose.Yaw;
        _pitch = pose.Pitch;
        return true;
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
        Gldefs? gldefs = _resources?.GetGldefs();

        static System.Collections.Generic.List<FlatVertex> Bucket(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>> d, string k)
        { if (!d.TryGetValue(k, out var l)) { l = new(); d[k] = l; } return l; }

        int Gray(int brightness, double scale)
        {
            double b = _fullBrightness ? 1.0 : Math.Clamp(brightness / 255.0, 0.15, 1.0);
            b *= scale;
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
            int fc = GlowingFlatDisplay.SurfaceRenderTint(
                s.Brightness,
                GlowingFlatDisplay.SurfaceLighting(s, GlowingFlatSurface.Floor, gldefs),
                _fullBrightness,
                1.0,
                _classicRendering);
            int cc = GlowingFlatDisplay.SurfaceRenderTint(
                s.Brightness,
                GlowingFlatDisplay.SurfaceLighting(s, GlowingFlatSurface.Ceiling, gldefs),
                _fullBrightness,
                0.85,
                _classicRendering);
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
                PushWall(wallB, a, b, fs.GetFloorZ(a), fs.GetFloorZ(b), fs.GetCeilZ(a), fs.GetCeilZ(b), front.MidTexture, front.OffsetX, front.OffsetY, peg, 0, 0, scale => VisualSurfaceLighting.WallRenderTint(front, VisualWallPart.Middle, _fullBrightness, scale, _classicRendering));
            }
            else if (fs != null && bs != null && front != null)
            {
                double fFa = fs.GetFloorZ(a), fFb = fs.GetFloorZ(b), bFa = bs.GetFloorZ(a), bFb = bs.GetFloorZ(b);
                if (fFa != bFa || fFb != bFb)
                {
                    // Lower step: top-pegged at the higher floor by default; pegged to the ceiling when lower-unpegged.
                    var peg = unpegBottom ? WallPeg.Custom : WallPeg.Top;
                    PushWall(wallB, a, b, Math.Min(fFa, bFa), Math.Min(fFb, bFb), Math.Max(fFa, bFa), Math.Max(fFb, bFb), front.LowTexture, front.OffsetX, front.OffsetY, peg, fs.GetCeilZ(a), fs.GetCeilZ(b), scale => VisualSurfaceLighting.WallRenderTint(front, VisualWallPart.Bottom, _fullBrightness, scale, _classicRendering));
                }
                double fCa = fs.GetCeilZ(a), fCb = fs.GetCeilZ(b), bCa = bs.GetCeilZ(a), bCb = bs.GetCeilZ(b);
                if (fCa != bCa || fCb != bCb)
                {
                    // Upper step: bottom-pegged at the lower ceiling by default; top-pegged when upper-unpegged.
                    var peg = unpegTop ? WallPeg.Top : WallPeg.BottomUp;
                    PushWall(wallB, a, b, Math.Min(fCa, bCa), Math.Min(fCb, bCb), Math.Max(fCa, bCa), Math.Max(fCb, bCb), front.HighTexture, front.OffsetX, front.OffsetY, peg, 0, 0, scale => VisualSurfaceLighting.WallRenderTint(front, VisualWallPart.Top, _fullBrightness, scale, _classicRendering));
                }
            }
        }

        if (_show3DFloors)
        {
            // GZDoom 3D floors: render each control sector's slab into target sectors.
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
                    int tc = GlowingFlatDisplay.SurfaceRenderTint(
                        f.Brightness,
                        GlowingFlatDisplay.SurfaceLighting(f.Control, GlowingFlatSurface.Ceiling, gldefs),
                        _fullBrightness,
                        1.0,
                        _classicRendering);
                    int bc = GlowingFlatDisplay.SurfaceRenderTint(
                        f.Brightness,
                        GlowingFlatDisplay.SurfaceLighting(f.Control, GlowingFlatSurface.Floor, gldefs),
                        _fullBrightness,
                        0.85,
                        _classicRendering);
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
                            f.Bottom, f.Bottom, f.Top, f.Top, f.SideTexture, 0, 0, WallPeg.Top, 0, 0, scale => Gray(f.Brightness, scale));
                    }
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
        Vec2D a, Vec2D b, double botZa, double botZb, double topZa, double topZb, string texName,
        int offsetX, int offsetY, WallPeg peg, double customPegA, double customPegB, Func<double, int> tint)
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
        int c = textured ? tint(1.0) : tint(0.6);

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

        if (ImageExampleMode)
        {
            DrawImageExample();
            return;
        }

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

            AutomapRenderPlan? automapPlan = null;
            if (AutomapMode)
            {
                var settings = _automapSettings;
                var options = AutomapModeModel.ToOptions(settings, _automapInvertLineVisibility, isUdmf: _mapFormat == MapFormat.Udmf, isDoom: _mapFormat == MapFormat.Doom);
                automapPlan = AutomapModeModel.BuildRenderPlan(
                    _map,
                    options,
                    settings,
                    AutomapModeModel.Palette(settings.ColorPreset),
                    _useHighlight ? _automapHighlight : null,
                    _automapEditSectors);
            }

            // Draw order: sector fills (textured) -> lines -> things/sprites -> selection markers.
            bool drawFills = AutomapMode ? automapPlan?.RenderTexturedSurfaces == true : _showFills && _classicViewMode != ClassicViewMode.Wireframe;
            if (drawFills)
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
            if (!AutomapMode && _showThings && _thingTris > 0 && _thingsVb != null)
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
            if (!AutomapMode && _showThings && _spriteBuckets.Count > 0)
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
            if (!AutomapMode && _showThings && _thingDirCount > 0 && _thingDirVb != null)
            {
                _device.SetVertexBuffer(_thingDirVb);
                _device.Draw(DBPrimitiveType.LineList, 0, _thingDirCount);
            }

            if (!AutomapMode) DrawRejectOverlay();

            if (!AutomapMode && _commentIconTris > 0 && _commentIconsVb != null)
            {
                _device.SetVertexBuffer(_commentIconsVb);
                _device.Draw(DBPrimitiveType.TriangleList, 0, _commentIconTris);
            }

            if (!AutomapMode && _selVertTris > 0 && _selVertsVb != null)
            {
                _device.SetVertexBuffer(_selVertsVb);
                _device.Draw(DBPrimitiveType.TriangleList, 0, _selVertTris);
            }

            if (!AutomapMode)
            {
                DrawBlockmap(); // debug overlay on top of geometry
                DrawVisplaneExplorerOverlay();
                DrawNodes();    // BSP partition lines overlay
                DrawSoundLeakPath();
                if (_useHighlight) DrawWadAuthorHighlight();
            }

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

        // Sector fills, bucketed by the active view mode texture when resolvable.
        RebuildFills();

        // Lines (each linedef segment plus a short front-side tick at its midpoint showing orientation).
        if (_linesVb != null)
        {
            const double frontTick = 6;
            var lv = new System.Collections.Generic.List<FlatVertex>(_map.Linedefs.Count * 4);
            if (AutomapMode)
            {
                AutomapModeSettings settings = _automapSettings;
                AutomapRenderPlan plan = AutomapModeModel.BuildRenderPlan(
                    _map,
                    AutomapModeModel.ToOptions(settings, _automapInvertLineVisibility, isUdmf: _mapFormat == MapFormat.Udmf, isDoom: _mapFormat == MapFormat.Doom),
                    settings,
                    AutomapModeModel.Palette(settings.ColorPreset),
                    _useHighlight ? _automapHighlight : null,
                    _automapEditSectors);
                foreach (var renderLine in plan.Lines)
                {
                    int c = ToArgb(renderLine.Color);
                    lv.Add(FV(renderLine.Line.Start.Position, c));
                    lv.Add(FV(renderLine.Line.End.Position, c));
                }
            }
            else
            {
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
            }
            _device.SetBufferData(_linesVb, lv.ToArray());
            _lineCount = lv.Count / 2;
        }

        // Thing direction ticks (own buffer, drawn above sprites). Skipped in arrow mode, which draws its own arrow.
        if (_thingDirVb != null)
        {
            var dv = new System.Collections.Generic.List<FlatVertex>();
            if (ThingIconRenderPolicy.ShouldDrawDirectionTicks(_zoom, _thingArrows))
            {
                bool compactThingMarkers = ThingIconRenderPolicy.UseCompactMarkers(_zoom, _fixedThingsScale, _thingArrows);
                double tickLen = ThingMarkerSize(ThingIconRenderPolicy.DirectionTickBaseSize(compactThingMarkers));
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
            bool compactThingMarkers = ThingIconRenderPolicy.UseCompactMarkers(_zoom, _fixedThingsScale, _thingArrows);
            bool overviewThingMarkers = ThingIconRenderPolicy.UseOverviewMarkers(_zoom, _thingArrows);
            bool farOverviewThingMarkers = ThingIconRenderPolicy.UseFarOverviewMarkers(_zoom, _thingArrows);
            double s = ThingMarkerSize(
                ThingIconRenderPolicy.MarkerBaseSize(
                    compactThingMarkers,
                    overviewThingMarkers,
                    farOverviewThingMarkers),
                compactThingMarkers);
            Gldefs? gldefs = _resources?.GetGldefs();
            System.Collections.Generic.HashSet<Thing>? overviewRepresentatives = null;
            if (ThingIconRenderPolicy.ShouldCullOverlappingOverviewThings(_zoom, _thingArrows))
            {
                var candidates = new System.Collections.Generic.List<ThingOverviewScreenCandidate<Thing>>();
                foreach (var candidate in _map.Things)
                {
                    if (ThingHidden2D(candidate)) continue;
                    ThingTypeInfo? candidateInfo = _gameConfig?.GetThing(candidate.Type);
                    double candidateRadius = ThingVisualRadius(candidate, candidateInfo);
                    bool candidateFixedSize = candidate.FixedSize || candidateInfo?.FixedSize == true;
                    if (!candidate.Selected && !ThingIconRenderPolicy.ShouldRenderThing(
                        candidateRadius,
                        _zoom,
                        _fixedThingsScale,
                        candidateFixedSize)) continue;
                    var candidateScreen = ThingScreenPosition(candidate.Position);
                    double candidateScreenRadius = ThingIconRenderPolicy.ProjectedThingScreenRadius(
                        candidateRadius,
                        _zoom,
                        _fixedThingsScale,
                        candidateFixedSize);
                    if (!ThingIconRenderPolicy.IsThingOnScreen(
                        candidateScreen.X,
                        candidateScreen.Y,
                        candidateScreenRadius,
                        Bounds.Width,
                        Bounds.Height)) continue;

                    candidates.Add(new ThingOverviewScreenCandidate<Thing>(
                        candidate,
                        ThingOverviewCell(candidateScreen),
                        candidate.Selected,
                        candidateRadius,
                        candidateScreen.X,
                        candidateScreen.Y));
                }

                overviewRepresentatives = new System.Collections.Generic.HashSet<Thing>(
                    ThingIconRenderPolicy.SelectOverviewScreenRepresentatives(candidates, _zoom, _thingArrows));
            }

            foreach (var t in _map.Things)
            {
                if (ThingHidden2D(t)) continue;
                ThingTypeInfo? thingInfo = _gameConfig?.GetThing(t.Type);
                double thingRadius = ThingVisualRadius(t, thingInfo);
                bool fixedSize = t.FixedSize || thingInfo?.FixedSize == true;
                if (!t.Selected && !ThingIconRenderPolicy.ShouldRenderThing(
                    thingRadius,
                    _zoom,
                    _fixedThingsScale,
                    fixedSize)) continue;
                var screen = ThingScreenPosition(t.Position);
                double screenRadius = ThingIconRenderPolicy.ProjectedThingScreenRadius(
                    thingRadius,
                    _zoom,
                    _fixedThingsScale,
                    fixedSize);
                if (!ThingIconRenderPolicy.IsThingOnScreen(
                    screen.X,
                    screen.Y,
                    screenRadius,
                    Bounds.Width,
                    Bounds.Height)) continue;
                if (overviewRepresentatives != null)
                {
                    if (!overviewRepresentatives.Contains(t)) continue;
                }
                // Arrow mode: Doom-Builder-style colored disc + direction arrow (no sprites).
                if (_thingArrows)
                {
                    BuildThingDisc(tv, t, gldefs, s, ThingIconRenderPolicy.ShouldDrawDiscArrow(_zoom, _thingArrows));
                    continue;
                }

                ThingBillboardDisplay? display = ThingBillboardDisplayPlanner.Plan(thingInfo, _resources);
                if (display != null
                    && ThingIconRenderPolicy.ShouldRenderSpriteIcon(
                        thingRadius,
                        _zoom,
                        _fixedThingsScale,
                        fixedSize)
                    && GetSpriteTexture(display.SpriteName) is { })
                {
                    ImageData img = display.Image;
                    int sc = ThingBillboardTint(t, gldefs);
                    var (hw, hh) = ThingIconRenderPolicy.SpriteHalfSize(
                        img.Width,
                        img.Height,
                        thingRadius,
                        _zoom,
                        _fixedThingsScale,
                        fixedSize);
                    var p = t.Position;
                    if (!spriteVerts.TryGetValue(display.SpriteName, out var list)) { list = new(); spriteVerts[display.SpriteName] = list; }
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

                int c = t.Selected ? unchecked((int)0xffffee00) : ThingLightColor(t, gldefs) ?? ThingColor(t.Type);
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
                if (!_alwaysShowVertices && _editMode != EditMode.Vertices && !v.Selected) continue;
                var p = v.Position;
                int c = v.Selected ? mc : unchecked((int)0xffd8d8d8);
                var n = new Vec2D(p.x, p.y + s);
                var e = new Vec2D(p.x + s, p.y);
                var so = new Vec2D(p.x, p.y - s);
                var w = new Vec2D(p.x - s, p.y);
                vv.Add(FV(p, c)); vv.Add(FV(n, c)); vv.Add(FV(e, c));
                vv.Add(FV(p, c)); vv.Add(FV(e, c)); vv.Add(FV(so, c));
                vv.Add(FV(p, c)); vv.Add(FV(so, c)); vv.Add(FV(w, c));
                vv.Add(FV(p, c)); vv.Add(FV(w, c)); vv.Add(FV(n, c));
            }
            var arr = vv.ToArray();
            if (arr.Length > 0) _device.SetBufferData(_selVertsVb, arr);
            _selVertTris = arr.Length / 3;
        }

        RebuildCommentIcons();
    }

    private void RebuildCommentIcons()
    {
        _commentIconTris = 0;
        if (_map == null || _commentIconsVb == null) return;

        var labels = new System.Collections.Generic.Dictionary<Sector, System.Collections.Generic.IReadOnlyList<LabelPositionInfo>>();
        if (_editMode == EditMode.Sectors)
        {
            foreach (Sector sector in _map.Sectors)
            {
                if (sector.Selected) continue;
                try { labels[sector] = Tools.FindLabelPositions(sector); }
                catch { }
            }
        }

        var icons = CommentsPanelModel.BuildRenderIcons(
            _map,
            new CommentRenderOptions(
                CommentModeFor(_editMode),
                IsUdmf: _mapFormat == MapFormat.Udmf,
                RenderComments: _renderComments,
                Scale: 1.0 / Math.Max(_zoom, 0.001),
                FixedThingsScale: _fixedThingsScale,
                Highlighted: HighlightedCommentElement(),
                SectorLabels: labels));
        if (icons.Count == 0) return;

        var verts = new System.Collections.Generic.List<FlatVertex>(icons.Count * 6);
        foreach (CommentRenderIcon icon in icons)
            AddCommentIcon(verts, icon);

        if (verts.Count == 0) return;
        _device?.SetBufferData(_commentIconsVb, verts.ToArray());
        _commentIconTris = verts.Count / 3;
    }

    private IFielded? HighlightedCommentElement()
        => CurrentPropertyHighlight() as IFielded;

    private static CommentsPanelMode CommentModeFor(EditMode mode)
        => mode switch
        {
            EditMode.Vertices => CommentsPanelMode.Vertices,
            EditMode.Sectors => CommentsPanelMode.Sectors,
            EditMode.Things => CommentsPanelMode.Things,
            _ => CommentsPanelMode.Linedefs,
        };

    private static void AddCommentIcon(System.Collections.Generic.List<FlatVertex> verts, CommentRenderIcon icon)
    {
        int fill = CommentIconColor(icon.Color);
        int mark = unchecked((int)0xff101820);
        var rect = icon.Rectangle;
        double left = rect.X;
        double right = rect.X + rect.Width;
        double top = rect.Y;
        double bottom = rect.Y + rect.Height;
        var center = new Vec2D((left + right) * 0.5, (top + bottom) * 0.5);
        double radius = Math.Max(Math.Abs(rect.Width), Math.Abs(rect.Height)) * 0.5;

        if (icon.Icon == CommentIconKind.Regular)
            AddFilledRect(verts, center, radius, fill);
        else
            AddFilledDiamond(verts, center, radius, fill);

        double markRadius = radius * 0.45;
        if (icon.Icon == CommentIconKind.Problem || icon.Icon == CommentIconKind.Question)
        {
            AddFilledRect(verts, center, markRadius, mark);
        }
        else if (icon.Icon == CommentIconKind.Info)
        {
            AddFilledRect(verts, new Vec2D(center.x, center.y + radius * 0.35), markRadius * 0.55, mark);
            AddFilledRect(verts, new Vec2D(center.x, center.y - radius * 0.25), markRadius * 0.55, mark);
        }
        else if (icon.Icon == CommentIconKind.Smile)
        {
            AddFilledDiamond(verts, center, markRadius, mark);
        }
    }

    private static int CommentIconColor(CommentIconColorRole role)
        => role switch
        {
            CommentIconColorRole.Selection => unchecked((int)0xffffee00),
            CommentIconColorRole.Highlight => unchecked((int)0xffff8040),
            _ => unchecked((int)0xffffffff),
        };

    // Builds sector fills bucketed by the active view mode texture when resolvable.
    private void RebuildFills()
    {
        foreach (var b in _fillBuckets) b.Vb.Dispose();
        _fillBuckets.Clear();
        if (_map == null || _device is null || _gl is null) return;

        var buckets = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);
        Gldefs? gldefs = _resources?.GetGldefs();
        foreach (var sector in _map.Sectors)
        {
            if (AutomapMode && !AutomapModeModel.IsSectorVisible(sector)) continue;
            if (sector.Sidedefs.Count == 0) continue;
            Triangulation tri;
            try { tri = Triangulation.Create(sector); }
            catch { continue; }
            if (tri.Vertices.Count == 0) continue;

            var fill = SectorFillForViewMode(sector);
            bool textured = fill.TextureName.Length > 0 && GetFlatTexture(fill.TextureName) != null;
            string key = textured ? fill.TextureName : "";
            int c = textured ? TexturedFillColor(sector, gldefs) : fill.Color;

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

    private (string TextureName, int Color) SectorFillForViewMode(Sector sector)
        => _classicViewMode switch
        {
            ClassicViewMode.FloorTextures => (sector.FloorTexture ?? "-", SectorFillColor(sector)),
            ClassicViewMode.CeilingTextures => (sector.CeilTexture ?? "-", SectorFillColor(sector)),
            ClassicViewMode.Brightness => ("", BrightnessFillColor(sector)),
            _ => ("", SectorFillColor(sector)),
        };

    // Brightness-shaded color used to modulate a textured flat (selected sectors tint cyan).
    private int TexturedFillColor(Sector s, Gldefs? gldefs)
    {
        GlowingFlatSurface? surface = _classicViewMode switch
        {
            ClassicViewMode.FloorTextures => GlowingFlatSurface.Floor,
            ClassicViewMode.CeilingTextures => GlowingFlatSurface.Ceiling,
            _ => null,
        };
        if (surface is { } flatSurface)
        {
            GlowingFlatSurfaceLighting lighting = GlowingFlatDisplay.SurfaceLighting(s, flatSurface, gldefs);
            if (lighting.Absolute && lighting.Light == GlowingFlatDisplay.DefaultGlowBrightness && lighting.Color == GlowingFlatDisplay.NoColorOverride)
                return s.Selected ? unchecked((int)0xff7fffff) : unchecked((int)0xffffffff);
        }

        double b = _fullBrightness && _classicViewMode != ClassicViewMode.Brightness
            ? 1.0
            : Math.Clamp(s.Brightness / 255.0, 0.2, 1.0);
        byte g = (byte)(b * 255);
        if (s.Selected)
            return unchecked((int)(0xff000000u | ((uint)(g / 2) << 16) | ((uint)g << 8) | g));
        return unchecked((int)(0xff000000u | ((uint)g << 16) | ((uint)g << 8) | g));
    }

    // Untextured fallback fill: dim gray so the line/thing overlays stay legible (selected -> cyan).
    private int SectorFillColor(Sector s)
    {
        double br = _fullBrightness && _classicViewMode != ClassicViewMode.Brightness
            ? 0.45
            : Math.Clamp(s.Brightness / 255.0, 0.12, 1.0) * 0.45;
        byte g = (byte)Math.Clamp(br * 255, 0, 255);
        if (s.Selected)
            return unchecked((int)(0xff000000u | ((uint)(g / 2) << 16) | ((uint)Math.Min(255, g + 60) << 8) | (uint)Math.Min(255, g + 80)));
        return unchecked((int)(0xff000000u | ((uint)g << 16) | ((uint)g << 8) | g));
    }

    private static int BrightnessFillColor(Sector s)
    {
        byte g = (byte)Math.Clamp(s.Brightness, 0, 255);
        if (s.Selected)
            return unchecked((int)(0xff000000u | ((uint)(g / 2) << 16) | ((uint)g << 8) | (uint)Math.Min(255, g + 32)));
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

    private double ThingMarkerSize(double baseSize, bool compactMarkers = false)
        => ThingIconRenderPolicy.MarkerWorldSize(baseSize, _zoom, _fixedThingsScale, compactMarkers);

    private static double ThingVisualRadius(Thing thing, ThingTypeInfo? thingInfo)
        => thing.Size > 0 ? thing.Size : thingInfo?.RenderRadius ?? thingInfo?.Width ?? 10.0;

    private (double X, double Y) ThingScreenPosition(Vec2D position)
    {
        double scale = Math.Max(_zoom, 0.001);
        double screenX = (position.x - _camX) / scale + Bounds.Width * 0.5;
        double screenY = Bounds.Height * 0.5 - (position.y - _camY) / scale;
        return (screenX, screenY);
    }

    private (int X, int Y) ThingOverviewCell((double X, double Y) screen)
    {
        return (
            ThingIconRenderPolicy.OverviewCullCell(screen.X, _zoom, _thingArrows),
            ThingIconRenderPolicy.OverviewCullCell(screen.Y, _zoom, _thingArrows));
    }

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
    private void BuildThingDisc(System.Collections.Generic.List<FlatVertex> list, Thing t, Gldefs? gldefs, double radius, bool drawArrow)
    {
        const int segments = 14;
        var p = t.Position;
        int catColor = DynamicLightDisplay.ThingColor(t, _gameConfig, gldefs)
            ?? (_gameConfig?.GetThing(t.Type) is { } info ? Color16(info.Color) : ThingColor(t.Type));
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

        if (!drawArrow) return;

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

    private int LineColor(Linedef l)
    {
        if (l.Selected) return unchecked((int)0xffffee00);                       // yellow
        if ((l.Front?.Sector?.Selected ?? false) || (l.Back?.Sector?.Selected ?? false))
            return unchecked((int)0xff00ccff);                                   // cyan
        bool twoSided = l.Front != null && l.Back != null;
        if (_markExtraFloors && l.ExtraFloorFlag) return ThreeDFloorLineColor;
        if (LinedefColorPresetModel.TryGetColor(l, _linedefColorPresets, _mapFormat == MapFormat.Udmf, out int presetColor))
            return twoSided ? LinedefColorPresetModel.WithAlpha(presetColor, _doubleSidedAlphaByte) : presetColor;
        return twoSided ? unchecked((int)0xff8090a0) : unchecked((int)0xffe0e0e0);
    }

    private static int ToArgb(AutomapColor color)
        => unchecked((int)(((uint)color.Alpha << 24) | ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue));

    private static FlatVertex FV(Vec2D p, int color)
        => new FlatVertex { x = (float)p.x, y = (float)p.y, z = 0, w = 1, c = color, u = 0, v = 0 };

    // ---- Input ----

    private Vec2D ToWorld(Point p)
        => new Vec2D(_camX + (p.X - Bounds.Width * 0.5) * _zoom,
                     _camY - (p.Y - Bounds.Height * 0.5) * _zoom);

    // Rounds a world point to the nearest grid intersection (identity when snapping is off).
    private Vec2D SnapToGrid(Vec2D w)
    {
        if (!_snapToGrid || _grid.GridSizeF <= 0) return w;
        return _grid.SnappedToGrid(w);
    }

    // Builds and draws the visible grid as a line list. Skips when cells would be denser than a few pixels.
    private void DrawGrid()
    {
        if (!_renderGrid) { _gridLineCount = 0; return; }
        if (_device is null || _gridVb is null || _grid.GridSizeF <= 0) { _gridLineCount = 0; return; }
        if (_grid.GridSizeF / _zoom < 4) { _gridLineCount = 0; return; }

        double halfW = Bounds.Width * 0.5 * _zoom;
        double halfH = Bounds.Height * 0.5 * _zoom;
        double left = _camX - halfW, right = _camX + halfW;
        double bottom = _camY - halfH, top = _camY + halfH;

        const int col = unchecked((int)0xff20242c);   // dim grid
        const int axis = unchecked((int)0xff3a4654);   // brighter x=0 / y=0 axes
        var verts = new System.Collections.Generic.List<FlatVertex>();
        var corners = new[]
        {
            GridLocal(new Vec2D(left, bottom)),
            GridLocal(new Vec2D(left, top)),
            GridLocal(new Vec2D(right, bottom)),
            GridLocal(new Vec2D(right, top)),
        };
        double localLeft = corners.Min(c => c.x);
        double localRight = corners.Max(c => c.x);
        double localBottom = corners.Min(c => c.y);
        double localTop = corners.Max(c => c.y);
        int x0 = (int)Math.Floor(localLeft / _grid.GridSizeF), x1 = (int)Math.Ceiling(localRight / _grid.GridSizeF);
        int y0 = (int)Math.Floor(localBottom / _grid.GridSizeF), y1 = (int)Math.Ceiling(localTop / _grid.GridSizeF);
        for (int gx = x0; gx <= x1; gx++)
        {
            double x = gx * _grid.GridSizeF; int c = gx == 0 ? axis : col;
            verts.Add(FV(GridWorld(new Vec2D(x, localBottom)), c));
            verts.Add(FV(GridWorld(new Vec2D(x, localTop)), c));
        }
        for (int gy = y0; gy <= y1; gy++)
        {
            double y = gy * _grid.GridSizeF; int c = gy == 0 ? axis : col;
            verts.Add(FV(GridWorld(new Vec2D(localLeft, y)), c));
            verts.Add(FV(GridWorld(new Vec2D(localRight, y)), c));
        }

        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetBufferData(_gridVb, verts.ToArray());
        _gridLineCount = verts.Count / 2;
        _device.SetVertexBuffer(_gridVb);
        _device.Draw(DBPrimitiveType.LineList, 0, _gridLineCount);
    }

    private Vec2D GridLocal(Vec2D world)
    {
        var origin = new Vec2D(_grid.GridOriginX, _grid.GridOriginY);
        return (world - origin).GetRotated(-_grid.GridRotate);
    }

    private Vec2D GridWorld(Vec2D local)
    {
        var origin = new Vec2D(_grid.GridOriginX, _grid.GridOriginY);
        return local.GetRotated(_grid.GridRotate) + origin;
    }

    private void SetEditorGridSize(double size)
    {
        _grid.SetGridSize(size);
        Picked?.Invoke($"grid {GridSizeLabel()}");
        MarkGeometryDirty();
    }

    public string ToggleSnapToGrid()
    {
        _snapToGrid = !_snapToGrid;
        string status = $"snap {(_snapToGrid ? "on" : "off")} (grid {GridSizeLabel()})";
        ActionStateChanged?.Invoke();
        Picked?.Invoke(status);
        return status;
    }

    public string ToggleGridRendering()
    {
        RenderGridEnabled = !RenderGridEnabled;
        string status = "Grid rendering is " + (_renderGrid ? "ENABLED" : "DISABLED");
        Picked?.Invoke(status);
        return status;
    }

    public string SnapSelectedMapElementsToGrid()
    {
        if (_map is null) return "No map loaded.";

        var vertices = new HashSet<Vertex>(_map.GetSelectedVertices());
        foreach (var line in _map.GetSelectedLinedefs())
        {
            vertices.Add(line.Start);
            vertices.Add(line.End);
        }

        var things = _map.GetSelectedThings();
        if (vertices.Count == 0 && things.Count == 0)
            return "Select any map element first.";

        bool willMove = vertices.Any(vertex => _grid.SnappedToGrid(vertex.Position) != vertex.Position)
            || things.Any(thing => _grid.SnappedToGrid(thing.Position) != thing.Position);

        if (!willMove)
            return "Selected map elements were already on the grid.";

        EditBegun?.Invoke("Snap map elements to grid");
        var result = _map.SnapSelectedMapElementsToGrid(_grid.SnappedToGrid);
        MarkGeometryDirty();

        var parts = new List<string>();
        if (result.SnappedVertices > 0) parts.Add($"{result.SnappedVertices} vertices");
        if (result.SnappedThings > 0) parts.Add($"{result.SnappedThings} things");
        return "Snapped " + string.Join(" and ", parts);
    }

    public string ChangeGridSize(bool larger)
    {
        _dynamicGridSize = false;
        bool changed = _grid.TryStepGridSize(larger);
        string status = changed
            ? $"grid {GridSizeLabel()}"
            : $"grid {(larger ? "max" : "min")} {GridSizeLabel()}";

        Picked?.Invoke(status);
        if (changed) MarkGeometryDirty();
        return status;
    }

    public string ToggleDynamicGridSize()
    {
        _dynamicGridSize = !_dynamicGridSize;
        if (_dynamicGridSize) MatchGridSizeToDisplayScale();
        string status = "Dynamic grid size is " + (_dynamicGridSize ? "ENABLED" : "DISABLED");
        Picked?.Invoke(status);
        MarkGeometryDirty();
        return status;
    }

    private bool MatchGridSizeToDisplayScale()
    {
        if (!_dynamicGridSize || Bounds.Width <= 0 || Bounds.Height <= 0) return false;
        bool changed = _grid.MatchSizeToDisplayScale(Bounds.Width, Bounds.Height, _zoom);
        if (changed)
        {
            Picked?.Invoke($"grid {GridSizeLabel()}");
            MarkGeometryDirty();
        }

        return changed;
    }

    public string AlignGridToSelectedLinedef()
    {
        if (_map is null) return "No map loaded.";
        return ApplyGridTransform(DBuilder.IO.SmartGridTransform.AlignToSelectedLinedef(_grid, _map.GetSelectedLinedefs()));
    }

    public string SetGridOriginToSelectedVertex()
    {
        if (_map is null) return "No map loaded.";
        return ApplyGridTransform(DBuilder.IO.SmartGridTransform.SetOriginToSelectedVertex(_grid, _map.GetSelectedVertices()));
    }

    public string ResetGridTransform()
        => ApplyGridTransform(DBuilder.IO.SmartGridTransform.Reset(_grid));

    public string SmartGridTransform()
    {
        if (_map is null) return "No map loaded.";

        SmartGridTransformResult result = _editMode switch
        {
            EditMode.Vertices => DBuilder.IO.SmartGridTransform.SmartFromVertices(
                _grid,
                _map.GetSelectedVertices(),
                _map.NearestVertex(_cursorWorld, HighlightRangeWorld())),
            EditMode.Linedefs => DBuilder.IO.SmartGridTransform.SmartFromLinedefs(
                _grid,
                _map.GetSelectedLinedefs(),
                _map.NearestLinedef(_cursorWorld, HighlightRangeWorld()),
                _cursorWorld),
            EditMode.Things => DBuilder.IO.SmartGridTransform.SmartFromThings(
                _grid,
                _map.GetSelectedThings(),
                NearestVisibleThing(_cursorWorld, ThingHighlightRangeWorld())),
            EditMode.Sectors => DBuilder.IO.SmartGridTransform.SmartFromSectors(_grid),
            _ => DBuilder.IO.SmartGridTransform.Reset(_grid),
        };

        return ApplyGridTransform(result);
    }

    private string ApplyGridTransform(SmartGridTransformResult result)
    {
        if (result.Applied) MarkGeometryDirty();
        Picked?.Invoke(result.Message);
        return result.Message;
    }

    public GridSetup GridSetupSnapshot()
    {
        var grid = new GridSetup();
        grid.SetGridSize(_grid.GridSizeF);
        grid.SetGridOrigin(_grid.GridOriginX, _grid.GridOriginY);
        grid.SetGridRotation(_grid.GridRotate);
        grid.SetBackground(_grid.BackgroundName, _grid.BackgroundSource);
        grid.SetBackgroundView(_grid.BackgroundX, _grid.BackgroundY, _grid.BackgroundScaleX, _grid.BackgroundScaleY);
        return grid;
    }

    public void ApplyGridSetup(double size, double originX, double originY, double rotation)
    {
        _grid.SetGridSize(size);
        _grid.SetGridOrigin(originX, originY);
        _grid.SetGridRotation(rotation);
        Picked?.Invoke($"grid {GridSizeLabel()}");
        MarkGeometryDirty();
    }

    public void ApplyGridSetup(GridSetup grid)
    {
        _grid.SetGridSize(grid.GridSizeF);
        _grid.SetGridOrigin(grid.GridOriginX, grid.GridOriginY);
        _grid.SetGridRotation(grid.GridRotate);
        _grid.SetBackground(grid.BackgroundName, grid.BackgroundSource);
        _grid.SetBackgroundView(grid.BackgroundX, grid.BackgroundY, grid.BackgroundScaleX, grid.BackgroundScaleY);
        Picked?.Invoke($"grid {GridSizeLabel()}");
        MarkGeometryDirty();
    }

    private string GridSizeLabel()
        => _grid.GridSizeF % 1.0 == 0.0
            ? _grid.GridSize.ToString(CultureInfo.InvariantCulture)
            : _grid.GridSizeF.ToString("0.###", CultureInfo.InvariantCulture);

    // Draws the 128-unit blockmap grid over the map (occupied blocks brighter), as a debug overlay.
    private void DrawBlockmap()
    {
        _blockmapLineCount = 0;
        _blockmapFillTriCount = 0;
        if (!_showBlockmap || _device is null || _blockmapVb is null || _blockmapFillVb is null || _map is null || _map.Vertices.Count == 0) return;

        double originX;
        double originY;
        int columns;
        int rows;
        double bs = BlockmapLump.BlockSize;
        bool parsedBlockmap = _blockmapExplorerData is { IsUsable: true };
        DBuilder.Map.BlockMap? generatedBlockmap = null;

        if (_blockmapExplorerData is { IsUsable: true } lump)
        {
            originX = lump.OriginX;
            originY = lump.OriginY;
            columns = lump.Columns;
            rows = lump.Rows;
        }
        else
        {
            _blockmapCache ??= new DBuilder.Map.BlockMap(_map);
            generatedBlockmap = _blockmapCache;
            originX = generatedBlockmap.OriginX;
            originY = generatedBlockmap.OriginY;
            columns = generatedBlockmap.Columns;
            rows = generatedBlockmap.Rows;
            bs = generatedBlockmap.BlockSize;
        }

        if (bs / _zoom < 3) return; // too dense to be useful

        // Visible block range (clamped to the blockmap extents).
        double halfW = Bounds.Width * 0.5 * _zoom, halfH = Bounds.Height * 0.5 * _zoom;
        int c0 = Math.Max(0, (int)Math.Floor((_camX - halfW - originX) / bs));
        int c1 = Math.Min(columns, (int)Math.Ceiling((_camX + halfW - originX) / bs));
        int r0 = Math.Max(0, (int)Math.Floor((_camY - halfH - originY) / bs));
        int r1 = Math.Min(rows, (int)Math.Ceiling((_camY + halfH - originY) / bs));
        if (c1 <= c0 || r1 <= r0) return;

        // Note: the renderer swaps R<->B, so these read blue-ish on screen (consistent with the app palette).
        const int grid = unchecked((int)0xff403028);      // dim grid for empty blocks (kept subtle)
        const int occupied = unchecked((int)0xffffc040);  // bright highlight for blocks containing linedefs
        const int highlight = unchecked((int)0x806060a0);
        const int questionable = unchecked((int)0x80502090);
        var fill = new System.Collections.Generic.List<FlatVertex>();
        var verts = new System.Collections.Generic.List<FlatVertex>();

        if (parsedBlockmap && _blockmapExplorerData != null)
        {
            if (_blockmapExplorerQuestionable)
            {
                foreach ((int column, int row) in _blockmapExplorerData.GetQuestionableBlocks())
                    AddBlockFill(fill, originX, originY, bs, column, row, questionable);
            }

            if (_blockmapExplorerColumn is int hc && _blockmapExplorerRow is int hr)
            {
                foreach ((int column, int row) in _blockmapExplorerData.GetHighlightedBlocks(hc, hr, _blockmapExplorerShared))
                    AddBlockFill(fill, originX, originY, bs, column, row, highlight);
            }
        }

        double x0 = originX + c0 * bs, x1 = originX + c1 * bs;
        double y0 = originY + r0 * bs, y1 = originY + r1 * bs;
        for (int c = c0; c <= c1; c++) { double x = originX + c * bs; verts.Add(FV(new Vec2D(x, y0), grid)); verts.Add(FV(new Vec2D(x, y1), grid)); }
        for (int r = r0; r <= r1; r++) { double y = originY + r * bs; verts.Add(FV(new Vec2D(x0, y), grid)); verts.Add(FV(new Vec2D(x1, y), grid)); }

        // Outline blocks that contain linedefs.
        for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int lineCount = parsedBlockmap && _blockmapExplorerData != null
                    ? _blockmapExplorerData.GetLinesInBlock(c, r).Count
                    : generatedBlockmap?.LinedefCountAt(c, r) ?? 0;
                if (lineCount == 0) continue;
                double bx0 = originX + c * bs, by0 = originY + r * bs, bx1 = bx0 + bs, by1 = by0 + bs;
                var p00 = new Vec2D(bx0, by0); var p10 = new Vec2D(bx1, by0);
                var p11 = new Vec2D(bx1, by1); var p01 = new Vec2D(bx0, by1);
                verts.Add(FV(p00, occupied)); verts.Add(FV(p10, occupied));
                verts.Add(FV(p10, occupied)); verts.Add(FV(p11, occupied));
                verts.Add(FV(p11, occupied)); verts.Add(FV(p01, occupied));
                verts.Add(FV(p01, occupied)); verts.Add(FV(p00, occupied));
            }

        if (fill.Count > 0)
        {
            _device.SetUniform("useTexture", 0f);
            _device.SetTexture(0, _placeholderTex);
            _device.SetAlphaBlendEnable(true);
            _device.SetSourceBlend(Blend.SourceAlpha);
            _device.SetDestinationBlend(Blend.InverseSourceAlpha);
            _device.SetBufferData(_blockmapFillVb, fill.ToArray());
            _blockmapFillTriCount = fill.Count / 3;
            _device.SetVertexBuffer(_blockmapFillVb);
            _device.Draw(DBPrimitiveType.TriangleList, 0, _blockmapFillTriCount);
            _device.SetAlphaBlendEnable(false);
        }

        if (verts.Count == 0) return;
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetBufferData(_blockmapVb, verts.ToArray());
        _blockmapLineCount = verts.Count / 2;
        _device.SetVertexBuffer(_blockmapVb);
        _device.Draw(DBPrimitiveType.LineList, 0, _blockmapLineCount);
    }

    private static void AddBlockFill(System.Collections.Generic.List<FlatVertex> verts, double originX, double originY, double blockSize, int column, int row, int color)
    {
        double bx0 = originX + column * blockSize;
        double by0 = originY + row * blockSize;
        double bx1 = bx0 + blockSize;
        double by1 = by0 + blockSize;
        var p00 = new Vec2D(bx0, by0);
        var p10 = new Vec2D(bx1, by0);
        var p11 = new Vec2D(bx1, by1);
        var p01 = new Vec2D(bx0, by1);
        verts.Add(FV(p00, color));
        verts.Add(FV(p10, color));
        verts.Add(FV(p11, color));
        verts.Add(FV(p00, color));
        verts.Add(FV(p11, color));
        verts.Add(FV(p01, color));
    }

    private void DrawVisplaneExplorerOverlay()
    {
        if (_device is null || _visplaneOverlayVb is null) return;
        if (_visplaneOverlayDirty)
        {
            var verts = new System.Collections.Generic.List<FlatVertex>(_visplaneOverlay.Length * 6);
            foreach (VisplaneOverlayRectangle rectangle in _visplaneOverlay)
                AddVisplaneOverlayFill(verts, rectangle);

            _device.SetBufferData(_visplaneOverlayVb, verts.ToArray());
            _visplaneOverlayTriCount = verts.Count / 3;
            _visplaneOverlayDirty = false;
        }

        if (_visplaneOverlayTriCount == 0) return;
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetAlphaBlendEnable(true);
        _device.SetSourceBlend(Blend.SourceAlpha);
        _device.SetDestinationBlend(Blend.InverseSourceAlpha);
        _device.SetVertexBuffer(_visplaneOverlayVb);
        _device.Draw(DBPrimitiveType.TriangleList, 0, _visplaneOverlayTriCount);
        _device.SetAlphaBlendEnable(false);
    }

    private static void AddVisplaneOverlayFill(System.Collections.Generic.List<FlatVertex> verts, VisplaneOverlayRectangle rectangle)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0 || (rectangle.Color >> 24) == 0) return;

        int color = unchecked((int)rectangle.Color);
        double x0 = rectangle.X;
        double y0 = rectangle.Y;
        double x1 = rectangle.X + rectangle.Width;
        double y1 = rectangle.Y + rectangle.Height;
        var p00 = new Vec2D(x0, y0);
        var p10 = new Vec2D(x1, y0);
        var p11 = new Vec2D(x1, y1);
        var p01 = new Vec2D(x0, y1);
        verts.Add(FV(p00, color));
        verts.Add(FV(p10, color));
        verts.Add(FV(p11, color));
        verts.Add(FV(p00, color));
        verts.Add(FV(p11, color));
        verts.Add(FV(p01, color));
    }

    // Draws classic NodesViewer subsector fills and BSP partition lines as a green overlay.
    private void DrawNodes()
    {
        if (!_showNodes || _device is null || _nodesVb is null || _nodePolygonsVb is null) return;

        if (_nodesDirty)
        {
            var verts = new System.Collections.Generic.List<FlatVertex>(_nodeLines.Length * 2);
            const int col = unchecked((int)0xff40ff80); // green (R<->B swap -> greenish on screen)
            foreach (var (a, b) in _nodeLines) { verts.Add(FV(a, col)); verts.Add(FV(b, col)); }
            _device.SetBufferData(_nodesVb, verts.ToArray());
            _nodesLineCount = verts.Count / 2;

            var polys = new System.Collections.Generic.List<FlatVertex>();
            for (int i = 0; i < _nodePolygons.Length; i++)
            {
                Vec2D[] poly = _nodePolygons[i];
                if (poly.Length < 3) continue;

                int c = NodePolygonColor(i);
                for (int p = 1; p < poly.Length - 1; p++)
                {
                    polys.Add(FV(poly[0], c));
                    polys.Add(FV(poly[p], c));
                    polys.Add(FV(poly[p + 1], c));
                }
            }
            _device.SetBufferData(_nodePolygonsVb, polys.ToArray());
            _nodePolygonTriCount = polys.Count / 3;
            _nodesDirty = false;
        }

        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        if (_nodePolygonTriCount > 0)
        {
            _device.SetAlphaBlendEnable(true);
            _device.SetSourceBlend(Blend.SourceAlpha);
            _device.SetDestinationBlend(Blend.InverseSourceAlpha);
            _device.SetVertexBuffer(_nodePolygonsVb);
            _device.Draw(DBPrimitiveType.TriangleList, 0, _nodePolygonTriCount);
            _device.SetAlphaBlendEnable(false);
        }
        if (_nodesLineCount == 0) return;

        _device.SetVertexBuffer(_nodesVb);
        _device.Draw(DBPrimitiveType.LineList, 0, _nodesLineCount);
    }

    private static int NodePolygonColor(int index)
    {
        ReadOnlySpan<uint> colors =
        [
            0x403060ffu,
            0x4020dc20u,
            0x40d09030u,
            0x4080ffffu,
            0x40ff6060u,
            0x40c060ffu,
        ];
        return unchecked((int)colors[index % colors.Length]);
    }

    private void DrawRejectOverlay()
    {
        if (_map == null || _device is null || _rejectOverlayVb is null) return;
        if (_rejectOverlayDirty)
        {
            var verts = new System.Collections.Generic.List<FlatVertex>();
            int count = Math.Min(_map.Sectors.Count, _rejectOverlayColors.Length);
            for (int i = 0; i < count; i++)
            {
                Sector sector = _map.Sectors[i];
                if (sector.Sidedefs.Count == 0) continue;
                Triangulation tri;
                try { tri = Triangulation.Create(sector); }
                catch { continue; }

                int color = WithAlpha(_rejectOverlayColors[i], _rejectOverlayAlpha);
                foreach (Vec2D point in tri.Vertices)
                    verts.Add(FV(point, color));
            }

            _device.SetBufferData(_rejectOverlayVb, verts.ToArray());
            _rejectOverlayTriCount = verts.Count / 3;
            _rejectOverlayDirty = false;
        }

        if (_rejectOverlayTriCount == 0) return;
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        _device.SetAlphaBlendEnable(true);
        _device.SetSourceBlend(Blend.SourceAlpha);
        _device.SetDestinationBlend(Blend.InverseSourceAlpha);
        _device.SetVertexBuffer(_rejectOverlayVb);
        _device.Draw(DBPrimitiveType.TriangleList, 0, _rejectOverlayTriCount);
        _device.SetAlphaBlendEnable(false);
    }

    private void DrawSoundLeakPath()
    {
        if (_device is null || _soundLeakPathVb is null || _soundLeakMarkerVb is null) return;
        if (_soundLeakDirty)
        {
            const int red = unchecked((int)0xffff0000);
            var lineVerts = new System.Collections.Generic.List<FlatVertex>();
            var markerVerts = new System.Collections.Generic.List<FlatVertex>();

            for (int i = 1; i < _soundLeakPath.Length; i++)
            {
                lineVerts.Add(FV(_soundLeakPath[i - 1], red));
                lineVerts.Add(FV(_soundLeakPath[i], red));
            }

            double halfSize = 4.0 / Math.Max(_zoom, 0.001);
            for (int i = 1; i < _soundLeakPath.Length - 1; i++)
                AddFilledRect(markerVerts, _soundLeakPath[i], halfSize, red);

            _device.SetBufferData(_soundLeakPathVb, lineVerts.ToArray());
            _device.SetBufferData(_soundLeakMarkerVb, markerVerts.ToArray());
            _soundLeakLineCount = lineVerts.Count / 2;
            _soundLeakMarkerTriCount = markerVerts.Count / 3;
            _soundLeakDirty = false;
        }

        if (_soundLeakLineCount == 0 && _soundLeakMarkerTriCount == 0) return;
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        if (_soundLeakLineCount > 0)
        {
            _device.SetVertexBuffer(_soundLeakPathVb);
            _device.Draw(DBPrimitiveType.LineList, 0, _soundLeakLineCount);
        }
        if (_soundLeakMarkerTriCount > 0)
        {
            _device.SetVertexBuffer(_soundLeakMarkerVb);
            _device.Draw(DBPrimitiveType.TriangleList, 0, _soundLeakMarkerTriCount);
        }
    }

    private void DrawWadAuthorHighlight()
    {
        if (!WadAuthorMode || _device is null || _wadAuthorVb is null || _wadAuthorFillVb is null) return;
        if (_wadAuthorDirty)
        {
            const int highlight = unchecked((int)0xffffee00);
            const int fill = unchecked((int)0x70ffee00);
            var verts = new System.Collections.Generic.List<FlatVertex>();
            var tris = new System.Collections.Generic.List<FlatVertex>();

            switch (_wadAuthorHighlight.Target)
            {
                case Vertex vertex when !_wadAuthorHighlight.Kind.Equals(WadAuthorHighlightKind.None):
                    AddFilledDiamond(tris, vertex.Position, 7.0 * _zoom, highlight);
                    break;
                case Linedef line:
                    verts.Add(FV(line.Start.Position, highlight));
                    verts.Add(FV(line.End.Position, highlight));
                    AddFilledDiamond(tris, line.Start.Position, 5.0 * _zoom, highlight);
                    AddFilledDiamond(tris, line.End.Position, 5.0 * _zoom, highlight);
                    break;
                case Sector sector:
                    AddSectorFill(tris, sector, fill);
                    break;
                case Thing thing:
                    AddFilledDiamond(tris, thing.Position, 12.0 * _zoom, highlight);
                    break;
            }

            _device.SetBufferData(_wadAuthorVb, verts.ToArray());
            _device.SetBufferData(_wadAuthorFillVb, tris.ToArray());
            _wadAuthorLineCount = verts.Count / 2;
            _wadAuthorTriCount = tris.Count / 3;
            _wadAuthorDirty = false;
        }

        if (_wadAuthorLineCount == 0 && _wadAuthorTriCount == 0) return;
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
        if (_wadAuthorLineCount > 0)
        {
            _device.SetVertexBuffer(_wadAuthorVb);
            _device.Draw(DBPrimitiveType.LineList, 0, _wadAuthorLineCount);
        }
        if (_wadAuthorTriCount > 0)
        {
            _device.SetAlphaBlendEnable(true);
            _device.SetSourceBlend(Blend.SourceAlpha);
            _device.SetDestinationBlend(Blend.InverseSourceAlpha);
            _device.SetVertexBuffer(_wadAuthorFillVb);
            _device.Draw(DBPrimitiveType.TriangleList, 0, _wadAuthorTriCount);
            _device.SetAlphaBlendEnable(false);
        }
    }

    private static void AddSectorFill(System.Collections.Generic.List<FlatVertex> verts, Sector sector, int color)
    {
        if (sector.Sidedefs.Count == 0) return;
        Triangulation tri;
        try { tri = Triangulation.Create(sector); }
        catch { return; }

        foreach (Vec2D point in tri.Vertices)
            verts.Add(FV(point, color));
    }

    private static void AddFilledRect(System.Collections.Generic.List<FlatVertex> verts, Vec2D center, double halfSize, int color)
    {
        var a = new Vec2D(center.x - halfSize, center.y - halfSize);
        var b = new Vec2D(center.x + halfSize, center.y - halfSize);
        var c = new Vec2D(center.x + halfSize, center.y + halfSize);
        var d = new Vec2D(center.x - halfSize, center.y + halfSize);
        verts.Add(FV(a, color));
        verts.Add(FV(b, color));
        verts.Add(FV(c, color));
        verts.Add(FV(a, color));
        verts.Add(FV(c, color));
        verts.Add(FV(d, color));
    }

    private static void AddFilledDiamond(System.Collections.Generic.List<FlatVertex> verts, Vec2D center, double radius, int color)
    {
        var n = new Vec2D(center.x, center.y + radius);
        var e = new Vec2D(center.x + radius, center.y);
        var s = new Vec2D(center.x, center.y - radius);
        var w = new Vec2D(center.x - radius, center.y);
        verts.Add(FV(center, color));
        verts.Add(FV(n, color));
        verts.Add(FV(e, color));
        verts.Add(FV(center, color));
        verts.Add(FV(e, color));
        verts.Add(FV(s, color));
        verts.Add(FV(center, color));
        verts.Add(FV(s, color));
        verts.Add(FV(w, color));
        verts.Add(FV(center, color));
        verts.Add(FV(w, color));
        verts.Add(FV(n, color));
    }

    private static int WithAlpha(int argb, byte alpha)
        => unchecked((int)(((uint)alpha << 24) | ((uint)argb & 0x00ffffffu)));

    private void DrawImageExample()
    {
        if (_device is null || _imageExampleVb is null || _imageExampleTex is null) return;

        var proj = Matrix4x4.CreateOrthographicOffCenter(0, (float)Bounds.Width, (float)Bounds.Height, 0, -1, 1);
        _device.SetUniform("projection", proj);
        _device.SetUniform("tex0", 0);
        _device.SetUniform("useTexture", 1f);
        _device.SetAlphaBlendEnable(false);
        _device.SetTexture(0, _imageExampleTex);

        const float left = 20f;
        const float top = 20f;
        const float right = 448f;
        const float bottom = 352f;
        const int white = unchecked((int)0xffffffff);
        var verts = new[]
        {
            ImageVertex(left, top, 0, 0, white),
            ImageVertex(right, top, 1, 0, white),
            ImageVertex(right, bottom, 1, 1, white),
            ImageVertex(left, top, 0, 0, white),
            ImageVertex(right, bottom, 1, 1, white),
            ImageVertex(left, bottom, 0, 1, white),
        };
        _device.SetBufferData(_imageExampleVb, verts);
        _device.SetVertexBuffer(_imageExampleVb);
        _device.Draw(DBPrimitiveType.TriangleList, 0, 2);
        _device.SetUniform("useTexture", 0f);
        _device.SetTexture(0, _placeholderTex);
    }

    private static FlatVertex ImageVertex(float x, float y, float u, float v, int color)
        => new() { x = x, y = y, z = 0, w = 1, c = color, u = u, v = v };

    internal static byte[] BuildImageExampleRgba(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int p = (y * width + x) * 4;
                byte grid = (byte)(((x / 16) + (y / 16)) % 2 == 0 ? 48 : 68);
                rgba[p] = (byte)Math.Clamp(36 + x * 120 / Math.Max(1, width - 1), 0, 255);
                rgba[p + 1] = (byte)Math.Clamp(grid + y * 80 / Math.Max(1, height - 1), 0, 255);
                rgba[p + 2] = (byte)Math.Clamp(130 + (width - x) * 70 / Math.Max(1, width), 0, 255);
                rgba[p + 3] = 255;
            }
        }

        DrawImageExampleFrame(rgba, width, height);
        return rgba;
    }

    private static void DrawImageExampleFrame(byte[] rgba, int width, int height)
    {
        int margin = 22;
        int right = width - margin - 1;
        int bottom = height - margin - 1;
        for (int y = margin; y <= bottom; y++)
        {
            for (int x = margin; x <= right; x++)
            {
                bool border = x == margin || x == right || y == margin || y == bottom;
                bool slash = Math.Abs((x - margin) - (y - margin)) < 3 || Math.Abs((right - x) - (y - margin)) < 3;
                if (!border && !slash) continue;
                int p = (y * width + x) * 4;
                rgba[p] = 240;
                rgba[p + 1] = 240;
                rgba[p + 2] = 240;
            }
        }
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
    private readonly HashSet<string> _pressedMapShortcuts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _heldMapCommands = new(StringComparer.Ordinal);

    private static bool IsFlyKey(Key k) => k is Key.W or Key.A or Key.S or Key.D or Key.Q or Key.E
        or Key.Up or Key.Down or Key.Left or Key.Right;

    private static bool IsFlyMovementCommand(string commandId) => commandId is
        "map3d.move-forward" or
        "map3d.moveforward" or
        "map3d.move-backward" or
        "map3d.movebackward" or
        "map3d.move-left" or
        "map3d.moveleft" or
        "map3d.move-right" or
        "map3d.moveright" or
        "map3d.move-up" or
        "map3d.moveup" or
        "map3d.move-down" or
        "map3d.movedown";

    private static bool IsHeldMapCommand(string commandId)
        => IsFlyMovementCommand(commandId) || commandId is
            "map3d.orbit" or
            "map2d.classicpaintselect" or
            "map2d.pan" or
            "map2d.pan_view" or
            "map3d.visual-paint-select" or
            "map3d.visualpaintselect";

    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool accel = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (AutomapMode) UpdateAutomapHighlight(_cursorWorld, e.KeyModifiers);
        string key = e.Key.ToString();
        var scope = _mode3D ? EditorCommandScope.Map3D : EditorCommandScope.Map2D;
        string pressKey = EditorCommandCatalog.ShortcutPressKey(scope, key, accel, shift, alt);
        if (EditorCommandCatalog.ResolveShortcut(ShortcutBindings, scope, key, accel, shift, alt) is { } commandId)
        {
            if (!ShouldRunMapShortcut(commandId, pressKey))
            {
                e.Handled = true;
                return;
            }

            if (RunMapCommand(commandId, e.KeyModifiers))
            {
                e.Handled = true;
                return;
            }
        }

        if (_mode3D && IsFlyKey(e.Key)) { _heldKeys.Add(e.Key); e.Handled = true; return; }

        base.OnKeyDown(e);
    }

    private bool RunMapCommand(string commandId, KeyModifiers modifiers = KeyModifiers.None)
    {
        switch (commandId)
        {
            case "map2d.toggle-3d":
            case "map2d.gzdbvisualmode":
            case "map3d.toggle-2d":
                Toggle3DMode();
                return true;
            case "map3d.move-forward":
            case "map3d.moveforward":
            case "map3d.move-backward":
            case "map3d.movebackward":
            case "map3d.move-left":
            case "map3d.moveleft":
            case "map3d.move-right":
            case "map3d.moveright":
            case "map3d.move-up":
            case "map3d.moveup":
            case "map3d.move-down":
            case "map3d.movedown":
            case "map3d.orbit":
                _heldMapCommands.Add(commandId);
                return true;
            case "map2d.classicpaintselect":
                BeginClassicPaintSelection();
                _heldMapCommands.Add(commandId);
                return true;
            case "map3d.visual-paint-select":
            case "map3d.visualpaintselect":
                _visualPaintSelectModifiers = modifiers;
                BeginVisualPaintSelection();
                _heldMapCommands.Add(commandId);
                return true;
            case "map2d.pan":
            case "map2d.pan_view":
                BeginHeldPanView();
                _heldMapCommands.Add(commandId);
                return true;
            case "map2d.toggle-sector-fills":
                ToggleSectorFills();
                return true;
            case "map2d.toggle-things":
                ToggleThings();
                return true;
            case "map2d.toggle-thing-arrows":
                ThingArrows = !ThingArrows;
                return true;
            case "map2d.toggle-event-lines":
                ToggleEventLines();
                Target3DChanged?.Invoke($"Event lines are {(_showEventLines ? "ENABLED" : "DISABLED")}");
                return true;
            case "map2d.toggle-comments":
            case "map2d.togglecomments":
                Target3DChanged?.Invoke($"Comments are {(ToggleComments() ? "ENABLED" : "DISABLED")}");
                return true;
            case "map2d.toggle-fixed-things-scale":
            case "map2d.togglefixedthingsscale":
                ToggleFixedThingsScale();
                return true;
            case "map2d.toggle-always-show-vertices":
            case "map2d.togglealwaysshowvertices":
                ToggleAlwaysShowVertices();
                return true;
            case "map2d.toggle-full-brightness":
            case "map3d.toggle-full-brightness":
                ToggleFullBrightness();
                return true;
            case "map2d.toggle-highlight":
            case "map3d.toggle-highlight":
            case "map2d.togglehighlight":
            case "map3d.togglehighlight":
                ToggleHighlight();
                return true;
            case "map2d.view-mode-wireframe":
            case "map2d.viewmodenormal":
                SetViewMode2D(ClassicViewMode.Wireframe);
                return true;
            case "map2d.view-mode-brightness":
            case "map2d.viewmodebrightness":
                SetViewMode2D(ClassicViewMode.Brightness);
                return true;
            case "map2d.view-mode-floors":
            case "map2d.viewmodefloors":
            case "map2d.flooralignmode":
                SetViewMode2D(ClassicViewMode.FloorTextures);
                return true;
            case "map2d.view-mode-ceilings":
            case "map2d.viewmodeceilings":
            case "map2d.ceilingalignmode":
                SetViewMode2D(ClassicViewMode.CeilingTextures);
                return true;
            case "map2d.next-view-mode":
            case "map2d.nextviewmode":
                NextViewMode2D();
                return true;
            case "map2d.previous-view-mode":
            case "map2d.previousviewmode":
                PreviousViewMode2D();
                return true;
            case "map2d.editselectionmode":
                BeginEditSelectionMode();
                return true;
            case "map2d.edit-properties":
            case "map2d.classicedit":
                EditRequested?.Invoke();
                return true;
            case "map2d.draw-sector":
                ToggleDrawMode(linesOnly: false);
                return true;
            case "map2d.draw-lines":
            case "map2d.drawlinesmode":
                ToggleDrawMode(linesOnly: true);
                return true;
            case "map2d.draw-rectangle":
            case "map2d.drawrectanglemode":
                SetShapeMode(ShapeKind.Rectangle);
                return true;
            case "map2d.draw-ellipse":
            case "map2d.drawellipsemode":
                SetShapeMode(ShapeKind.Ellipse);
                return true;
            case "map2d.draw-curve":
            case "map2d.drawcurvemode":
                ToggleDrawMode(linesOnly: true, curve: true);
                return true;
            case "map2d.draw-grid":
            case "map2d.drawgridmode":
                SetShapeMode(ShapeKind.Grid);
                return true;
            case "map2d.increase-subdivision-level":
            case "map2d.increasesubdivlevel":
                return AdjustDrawSubdivision(increase: true);
            case "map2d.decrease-subdivision-level":
            case "map2d.decreasesubdivlevel":
                return AdjustDrawSubdivision(increase: false);
            case "map2d.increase-bevel":
            case "map2d.increasebevel":
                return AdjustDrawBevel(increase: true);
            case "map2d.decrease-bevel":
            case "map2d.decreasebevel":
                return AdjustDrawBevel(increase: false);
            case "map2d.draw-point":
            case "map2d.drawpoint":
                if (!_drawMode) return false;
                PlaceDrawPoint(_drawCursor);
                return true;
            case "map2d.remove-draw-point":
            case "map2d.removepoint":
                return RemoveDrawPoint(_drawPoints.Count - 1);
            case "map2d.remove-first-draw-point":
            case "map2d.removefirstpoint":
                return RemoveDrawPoint(0);
            case "map2d.make-sector":
            case "map2d.makesectormode":
                MakeSectorAtCursor();
                return true;
            case "map2d.split-line":
                SplitLinedefs();
                return true;
            case "map2d.curvelinesmode":
                CurveSelectedLinedefs();
                return true;
            case "map2d.insert":
            case "map2d.insertitem":
                InsertAtCursor();
                return true;
            case "map2d.placevisualstart":
                PlaceVisualStart();
                return true;
            case "map2d.place-things":
            case "map2d.placethings":
                PlaceThingsFromSelection();
                return true;
            case "map2d.thingaligntowall":
                AlignSelectedThingsToWall();
                return true;
            case "map2d.point-thing-to-cursor":
            case "map2d.thinglookatcursor":
                PointThingsToCursor(awayFromCursor: modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta));
                return true;
            case "map2d.syncedthingedit":
                Picked?.Invoke(ToggleSynchronizedThingEditing()
                    ? "Things editing is SYNCHRONIZED"
                    : "Things editing is not synchronized");
                return true;
            case "map2d.bridge-mode":
            case "map2d.bridgemode":
                RunBridgeCommand();
                return true;
            case "map2d.mode-3d-floor":
            case "map2d.threedfloorhelpermode":
                SetThreeDFloorEditMode(ThreeDFloorEditMode.Floor);
                return true;
            case "map2d.mode-3d-slope":
            case "map2d.threedslopemode":
                SetThreeDFloorEditMode(ThreeDFloorEditMode.Slope);
                return true;
            case "map2d.mode-draw-slopes":
            case "map2d.drawslopesmode":
                SetThreeDFloorEditMode(ThreeDFloorEditMode.DrawSlopes);
                return true;
            case "map2d.mode-stair-sector-builder":
            case "map2d.stairsectorbuildermode":
                SetThreeDFloorEditMode(ThreeDFloorEditMode.StairSectorBuilder);
                return true;
            case "map2d.3dfloor.select-control-sector":
            case "map2d.select3dfloorcontrolsector":
                SelectThreeDFloorControlSectors();
                return true;
            case "map2d.3dfloor.relocate-control-sectors":
            case "map2d.relocate3dfloorcontrolsectors":
                RelocateThreeDFloorControlSectors();
                return true;
            case "map2d.3dfloor.duplicate-geometry":
            case "map2d.duplicate3dfloorgeometry":
                DuplicateThreeDFloorGeometry();
                return true;
            case "map2d.3dfloor.draw-slope-point":
            case "map2d.drawslopepoint":
                PlaceThreeDFloorSlopeDrawPoint(_drawCursor);
                return true;
            case "map2d.3dfloor.draw-floor-slope":
            case "map2d.drawfloorslope":
                FinishThreeDFloorSlopeDraw(ThreeDFloorSlopeDrawingMode.Floor);
                return true;
            case "map2d.3dfloor.draw-ceiling-slope":
            case "map2d.drawceilingslope":
                FinishThreeDFloorSlopeDraw(ThreeDFloorSlopeDrawingMode.Ceiling);
                return true;
            case "map2d.3dfloor.draw-floor-and-ceiling-slope":
            case "map2d.drawfloorandceilingslope":
                FinishThreeDFloorSlopeDraw(ThreeDFloorSlopeDrawingMode.FloorAndCeiling);
                return true;
            case "map2d.3dfloor.finish-slope-draw":
            case "map2d.finishslopedraw":
                FinishThreeDFloorSlopeDraw(_threeDFloorSlopeDrawingMode);
                return true;
            case "map2d.3dfloor.flip-slope":
            case "map2d.threedflipslope":
                FlipThreeDFloorSlopeDraw();
                return true;
            case "map2d.3dfloor.cycle-highlight-up":
            case "map2d.cyclehighlighted3dfloorup":
                CycleHighlightedThreeDFloor(up: true);
                return true;
            case "map2d.3dfloor.cycle-highlight-down":
            case "map2d.cyclehighlighted3dfloordown":
                CycleHighlightedThreeDFloor(up: false);
                return true;
            case "map2d.select":
            case "map2d.classicselect":
                SelectAtCursor(modifiers);
                return true;
            case "map2d.mode-vertices":
            case "map2d.verticesmode":
                SetEditMode(EditMode.Vertices);
                return true;
            case "map2d.mode-linedefs":
            case "map2d.linedefsmode":
                SetEditMode(EditMode.Linedefs);
                return true;
            case "map2d.mode-sectors":
            case "map2d.sectorsmode":
                SetEditMode(EditMode.Sectors);
                return true;
            case "map2d.select-sectors-outline":
            case "map2d.selectsectorsoutline":
                SelectSectorsOutline();
                return true;
            case "map2d.mode-things":
            case "map2d.thingsmode":
                SetEditMode(EditMode.Things);
                return true;
            case "map2d.mode-image-example":
            case "map2d.imageexamplemode":
                ToggleImageExampleMode();
                return true;
            case "map2d.mode-automap":
            case "map2d.automapmode":
                ToggleAutomapMode();
                return true;
            case "map2d.mode-wadauthor":
            case "map2d.wadauthormode":
                ToggleWadAuthorMode();
                return true;
            case "map2d.mode-visplane-explorer":
            case "map2d.visplaneexplorermode":
                VisplaneExplorerRequested?.Invoke();
                return true;
            case "map2d.flip":
            case "map2d.fliplinedefs":
                FlipLinedefs();
                return true;
            case "map2d.flip-sidedefs":
            case "map2d.flipsidedefs":
                FlipSidedefs();
                return true;
            case "map2d.select-single-sided":
            case "map2d.selectsinglesided":
                KeepSelectedLinedefsBySidedness(doubleSided: false);
                return true;
            case "map2d.select-double-sided":
            case "map2d.selectdoublesided":
                KeepSelectedLinedefsBySidedness(doubleSided: true);
                return true;
            case "map2d.align-linedefs":
            case "map2d.alignlinedefs":
                AlignLinedefs();
                return true;
            case "map2d.split-linedefs":
            case "map2d.splitlinedefs":
                SplitLinedefs();
                return true;
            case "map2d.dissolveitem":
                DissolveItem();
                return true;
            case "map2d.join-sectors":
            case "map2d.joinsectors":
                JoinOrMergeSelectedSectors(merge: false);
                return true;
            case "map2d.merge-sectors":
            case "map2d.mergesectors":
                JoinOrMergeSelectedSectors(merge: true);
                return true;
            case "map2d.lower-floor-8":
            case "map2d.lowerfloor8":
                AdjustSectorHeights(SectorHeightPart.Floor, -8);
                return true;
            case "map2d.raise-floor-8":
            case "map2d.raisefloor8":
                AdjustSectorHeights(SectorHeightPart.Floor, 8);
                return true;
            case "map2d.lower-ceiling-8":
            case "map2d.lowerceiling8":
                AdjustSectorHeights(SectorHeightPart.Ceiling, -8);
                return true;
            case "map2d.raise-ceiling-8":
            case "map2d.raiseceiling8":
                AdjustSectorHeights(SectorHeightPart.Ceiling, 8);
                return true;
            case "map2d.raise-brightness-8":
            case "map2d.raisebrightness8":
                AdjustSectorBrightness(raise: true);
                return true;
            case "map2d.lower-brightness-8":
            case "map2d.lowerbrightness8":
                AdjustSectorBrightness(raise: false);
                return true;
            case "map2d.align-textures-x":
                AutoAlignSelectedTextures(vertical: false);
                return true;
            case "map2d.align-textures-y":
                AutoAlignSelectedTextures(vertical: true);
                return true;
            case "map2d.fit-selected-textures":
                FitSelectedTextures();
                return true;
            case "map2d.apply-lightfog-flag":
            case "map2d.applylightfogflag":
                ApplyLightFogFlag();
                return true;
            case "map2d.toggle-grid-snap":
            case "map2d.togglesnap":
                ToggleSnapToGrid();
                return true;
            case "map2d.toggle-grid-rendering":
            case "map2d.togglegrid":
                ToggleGridRendering();
                return true;
            case "map2d.toggle-dynamic-grid-size":
            case "map2d.toggledynamicgrid":
                ToggleDynamicGridSize();
                return true;
            case "map2d.align-grid-to-linedef":
            case "map2d.aligngridtolinedef":
                AlignGridToSelectedLinedef();
                return true;
            case "map2d.set-grid-origin-to-vertex":
            case "map2d.setgridorigintovertex":
                SetGridOriginToSelectedVertex();
                return true;
            case "map2d.reset-grid-transform":
            case "map2d.resetgrid":
                ResetGridTransform();
                return true;
            case "map2d.smart-grid-transform":
            case "map2d.smartgridtransform":
                SmartGridTransform();
                return true;
            case "map2d.grid-down":
                ChangeGridSize(larger: false);
                return true;
            case "map2d.griddec":
                ChangeGridSize(larger: true);
                return true;
            case "map2d.grid-up":
                ChangeGridSize(larger: true);
                return true;
            case "map2d.gridinc":
                ChangeGridSize(larger: false);
                return true;
            case "map2d.finish-draw":
            case "map2d.finishdraw":
            case "map2d.acceptmode":
                if (!_drawMode) return false;
                FinishDraw();
                return true;
            case "map2d.cancel-draw":
            case "map2d.cancelmode":
                if (!InDrawMode) return false;
                ExitDrawModes();
                return true;
            case "map2d.fit":
            case "map2d.centerinscreen":
                FitToMap();
                MarkGeometryDirty();
                return true;
            case "map2d.scrollwest":
                ScrollView(-100, 0);
                return true;
            case "map2d.scrolleast":
                ScrollView(100, 0);
                return true;
            case "map2d.scrollnorth":
                ScrollView(0, 100);
                return true;
            case "map2d.scrollsouth":
                ScrollView(0, -100);
                return true;
            case "map2d.zoom-in":
            case "map2d.zoomin":
                ZoomBy(0.8);
                return true;
            case "map2d.zoom-out":
            case "map2d.zoomout":
                ZoomBy(1.25);
                return true;
            case "map3d.toggle-gravity":
            case "map3d.togglegravity":
            case "map3d.walk-mode":
                _walkMode = !_walkMode;
                RequestNextFrameRendering();
                return true;
            case "map3d.move-camera-to-cursor":
            case "map3d.movecameratocursor":
                MoveCameraToCursor();
                return true;
            case "map3d.move-thing-left":
            case "map3d.movethingleft":
                MoveThingTargets3D(new Vec2D(0, -_grid.GridSizeF));
                return true;
            case "map3d.move-thing-right":
            case "map3d.movethingright":
                MoveThingTargets3D(new Vec2D(0, _grid.GridSizeF));
                return true;
            case "map3d.move-thing-forward":
            case "map3d.movethingfwd":
                MoveThingTargets3D(new Vec2D(-_grid.GridSizeF, 0));
                return true;
            case "map3d.move-thing-backward":
            case "map3d.movethingback":
                MoveThingTargets3D(new Vec2D(_grid.GridSizeF, 0));
                return true;
            case "map3d.insert-item":
            case "map3d.insertitem":
                InsertThingAtTarget3D();
                return true;
            case "map3d.copy-selection":
            case "map3d.copyselection":
                CopyVisualThingSelection3D();
                return true;
            case "map3d.cut-selection":
            case "map3d.cutselection":
                CutVisualThingSelection3D();
                return true;
            case "map3d.paste-selection":
            case "map3d.pasteselection":
                PasteVisualThingSelection3D();
                return true;
            case "map3d.place-thing-at-cursor":
            case "map3d.placethingatcursor":
                PlaceThingTargetsAtCursor3D();
                return true;
            case "map3d.rotate-clockwise":
            case "map3d.rotateclockwise":
            case "map3d.rotate-thing-clockwise":
            case "map3d.rotatethingclockwise":
                RotateVisualTargets3D(_gameConfig?.DoomThingRotationAngles == true ? 45 : 5, 5);
                return true;
            case "map3d.rotate-counterclockwise":
            case "map3d.rotatecounterclockwise":
            case "map3d.rotate-thing-counterclockwise":
            case "map3d.rotatethingcounterclockwise":
                RotateVisualTargets3D(_gameConfig?.DoomThingRotationAngles == true ? -45 : -5, -5);
                return true;
            case "map3d.pitch-clockwise":
            case "map3d.pitchclockwise":
            case "map3d.pitch-thing-clockwise":
            case "map3d.pitchthingclockwise":
                ChangeThingPitchTargets3D(-5);
                return true;
            case "map3d.pitch-counterclockwise":
            case "map3d.pitchcounterclockwise":
            case "map3d.pitch-thing-counterclockwise":
            case "map3d.pitchthingcounterclockwise":
                ChangeThingPitchTargets3D(5);
                return true;
            case "map3d.roll-clockwise":
            case "map3d.rollclockwise":
            case "map3d.roll-thing-clockwise":
            case "map3d.rollthingclockwise":
                ChangeThingRollTargets3D(-5);
                return true;
            case "map3d.roll-counterclockwise":
            case "map3d.rollcounterclockwise":
            case "map3d.roll-thing-counterclockwise":
            case "map3d.rollthingcounterclockwise":
                ChangeThingRollTargets3D(5);
                return true;
            case "map3d.apply-camera-rotation":
            case "map3d.apply-camera-rotation-to-things":
            case "map3d.applycamerarotationtothings":
                ApplyCameraRotationToSelectedThings3D();
                return true;
            case "map3d.look-through-selection":
            case "map3d.look-through-thing":
            case "map3d.lookthroughthing":
                LookThroughSelectedThing3D();
                return true;
            case "map3d.thing-align-to-wall":
            case "map3d.align-things-to-wall":
            case "map3d.thingaligntowall":
                AlignSelectedVisualThingsToWall3D();
                return true;
            case "map3d.show-visual-things":
            case "map3d.showvisualthings":
                CycleVisualThings3D();
                return true;
            case "map3d.scale-up":
            case "map3d.scaleup":
                ChangeVisualScale3D(1, 1);
                return true;
            case "map3d.scale-down":
            case "map3d.scaledown":
                ChangeVisualScale3D(-1, -1);
                return true;
            case "map3d.scale-up-x":
            case "map3d.scaleupx":
                ChangeVisualScale3D(1, 0);
                return true;
            case "map3d.scale-down-x":
            case "map3d.scaledownx":
                ChangeVisualScale3D(-1, 0);
                return true;
            case "map3d.scale-up-y":
            case "map3d.scaleupy":
                ChangeVisualScale3D(0, 1);
                return true;
            case "map3d.scale-down-y":
            case "map3d.scaledowny":
                ChangeVisualScale3D(0, -1);
                return true;
            case "map3d.lower-sector-1":
            case "map3d.lowersector1":
                AdjustTarget3D(-1);
                return true;
            case "map3d.raise-sector-1":
            case "map3d.raisesector1":
                AdjustTarget3D(1);
                return true;
            case "map3d.lower-sector-8":
            case "map3d.lowersector8":
                AdjustTarget3D(-8);
                return true;
            case "map3d.raise-sector-8":
            case "map3d.raisesector8":
                AdjustTarget3D(8);
                return true;
            case "map3d.lower-sector-128":
            case "map3d.lowersector128":
                AdjustTarget3D(-128);
                return true;
            case "map3d.raise-sector-128":
            case "map3d.raisesector128":
                AdjustTarget3D(128);
                return true;
            case "map3d.lower-map-element-by-grid-size":
            case "map3d.lowermapelementbygridsize":
                AdjustTarget3D(-_grid.GridSize);
                return true;
            case "map3d.raise-map-element-by-grid-size":
            case "map3d.raisemapelementbygridsize":
                AdjustTarget3D(_grid.GridSize);
                return true;
            case "map3d.lower-sector-to-nearest":
            case "map3d.lowersectortonearest":
                AdjustTargetToNearest3D(raise: false, withinSelection: modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta));
                return true;
            case "map3d.raise-sector-to-nearest":
            case "map3d.raisesectortonearest":
                AdjustTargetToNearest3D(raise: true, withinSelection: modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta));
                return true;
            case "map3d.brightness-down":
            case "map3d.lower-brightness-8":
            case "map3d.lowerbrightness8":
                AdjustTargetBrightness3D(raise: false);
                return true;
            case "map3d.brightness-up":
            case "map3d.raise-brightness-8":
            case "map3d.raisebrightness8":
                AdjustTargetBrightness3D(raise: true);
                return true;
            case "map3d.match-brightness":
            case "map3d.matchbrightness":
                MatchBrightness3D();
                return true;
            case "map3d.texture-copy":
            case "map3d.texturecopy":
            case "map3d.copy-texture":
                CopyTexture3D();
                return true;
            case "map3d.texture-paste":
            case "map3d.texturepaste":
            case "map3d.apply-texture":
                ApplyTexture3D();
                return true;
            case "map3d.flood-fill-texture":
            case "map3d.floodfilltextures":
                FloodFillTexture3D();
                return true;
            case "map3d.align-texture-x":
            case "map3d.visual-auto-align-x":
            case "map3d.visualautoalignx":
                AutoAlignTarget3D(alignX: true, alignY: false);
                return true;
            case "map3d.align-texture-y":
            case "map3d.visual-auto-align-y":
            case "map3d.visualautoaligny":
                AutoAlignTarget3D(alignX: false, alignY: true);
                return true;
            case "map3d.visual-auto-align":
            case "map3d.visualautoalign":
                AutoAlignTarget3D(alignX: true, alignY: true);
                return true;
            case "map3d.visual-auto-align-to-selection-x":
            case "map3d.visualautoaligntoselectionx":
                AutoAlignSelectedVisualTextures3D(alignX: true, alignY: false);
                return true;
            case "map3d.visual-auto-align-to-selection-y":
            case "map3d.visualautoaligntoselectiony":
                AutoAlignSelectedVisualTextures3D(alignX: false, alignY: true);
                return true;
            case "map3d.visual-auto-align-to-selection":
            case "map3d.visualautoaligntoselection":
                AutoAlignSelectedVisualTextures3D(alignX: true, alignY: true);
                return true;
            case "map3d.visual-select":
            case "map3d.select-target":
            case "map3d.visualselect":
                ToggleSelection3D();
                return true;
            case "map3d.visual-edit":
            case "map3d.edit-properties":
            case "map3d.visualedit":
                OpenTargetDialog3D();
                return true;
            case "map3d.clear-selection":
            case "map3d.clear-target":
            case "map3d.clearselection":
                ClearSelection3D();
                return true;
            case "map3d.reset-offsets":
            case "map3d.resettexture":
                ResetVisualTexture3D(local: false);
                return true;
            case "map3d.reset-local-offsets":
            case "map3d.resettextureudmf":
                ResetVisualTexture3D(local: true);
                return true;
            case "map3d.texture-copy-offsets":
            case "map3d.texturecopyoffsets":
            case "map3d.copy-offsets":
                CopyTextureOffsets3D();
                return true;
            case "map3d.texture-paste-offsets":
            case "map3d.texturepasteoffsets":
            case "map3d.paste-offsets":
                PasteTextureOffsets3D();
                return true;
            case "map3d.copy-properties":
            case "map3d.copyproperties":
                CopyVisualPropertiesTarget();
                return true;
            case "map3d.paste-properties":
            case "map3d.pasteproperties":
                PasteVisualPropertiesTargets();
                return true;
            case "map3d.paste-properties-options":
            case "map3d.pastepropertieswithoptions":
                PastePropertiesOptionsRequested?.Invoke();
                return true;
            case "map3d.fit-textures":
            case "map3d.visualfittextures":
                FitSelectedVisualTextures3D();
                return true;
            case "map3d.toggle-upper-unpegged":
            case "map3d.toggleupperunpegged":
                ToggleUnpegged3D(upper: true);
                return true;
            case "map3d.toggle-lower-unpegged":
            case "map3d.togglelowerunpegged":
                ToggleUnpegged3D(upper: false);
                return true;
            case "map3d.toggle-slope":
            case "map3d.toggleslope":
                ToggleSlope3D();
                return true;
            case "map3d.reset-slope":
            case "map3d.resetslope":
                ResetSlope3D();
                return true;
            case "map3d.raise-slope-handle-to-nearest":
            case "map3d.raiseslopehandletonearest":
                ApplyVisualSlopeHandleNearestHeight3D(raise: true);
                return true;
            case "map3d.lower-slope-handle-to-nearest":
            case "map3d.lowerslopehandletonearest":
                ApplyVisualSlopeHandleNearestHeight3D(raise: false);
                return true;
            case "map3d.slope-between-handles":
            case "map3d.slopebetweenhandles":
                ApplyVisualSlopeBetweenHandles3D();
                return true;
            case "map3d.arch-between-handles":
            case "map3d.archbetweenhandles":
                ApplyVisualArchBetweenHandles3D();
                return true;
            case "map3d.toggle-alpha-based-texture-highlighting":
            case "map3d.alphabasedtexturehighlighting":
                ToggleAlphaBasedTextureHighlighting();
                return true;
            case "map3d.toggle-models-rendering":
            case "map3d.gztogglemodels":
            case "map3d.toggle-model-rendering":
                CycleModelRenderMode();
                Target3DChanged?.Invoke($"Models rendering mode: {ThingModelRenderPlanner.StatusLabel(_modelRenderMode)}");
                return true;
            case "map3d.toggle-dynamic-lights-rendering":
            case "map3d.toggledynamiclightsrendering":
            case "map3d.gztogglelights":
                CycleLightRenderMode();
                Target3DChanged?.Invoke($"Dynamic lights rendering mode: {ThingLightRenderPlanner.StatusLabel(_lightRenderMode)}");
                return true;
            case "map3d.toggle-enhanced-rendering-effects":
            case "map3d.gztoggleenhancedrendering":
                ToggleEnhancedRenderingEffects();
                Target3DChanged?.Invoke($"Enhanced rendering effects are {(_enhancedRenderingEffects ? "ENABLED" : "DISABLED")}");
                return true;
            case "map3d.toggle-classic-rendering":
            case "map3d.toggleclassicrendering":
                ToggleClassicRendering();
                Target3DChanged?.Invoke($"Classic rendering is {(_classicRendering ? "ENABLED" : "DISABLED")}");
                return true;
            case "map3d.toggle-fog-rendering":
            case "map3d.togglefogrendering":
            case "map3d.gztogglefog":
                ToggleDrawFog();
                Target3DChanged?.Invoke($"Fog rendering is {(_drawFog ? "ENABLED" : "DISABLED")}");
                return true;
            case "map3d.toggle-sky-rendering":
            case "map3d.toggleskyrendering":
            case "map3d.gztogglesky":
                ToggleDrawSky();
                Target3DChanged?.Invoke($"Sky rendering is {(_drawSky ? "ENABLED" : "DISABLED")}");
                return true;
            case "map3d.toggle-event-lines":
            case "map3d.toggleeventlines":
            case "map3d.gztoggleeventlines":
                ToggleEventLines();
                Target3DChanged?.Invoke($"Event lines are {(_showEventLines ? "ENABLED" : "DISABLED")}");
                return true;
            case "map3d.toggle-visual-vertices":
            case "map3d.togglevisualvertices":
            case "map3d.gztogglevisualvertices":
                ToggleVisualVertices();
                Target3DChanged?.Invoke($"Visual vertices are {(_showVisualVertices ? "ENABLED" : "DISABLED")}");
                return true;
            case "map3d.toggle-visual-sidedef-slope-picking":
            case "map3d.togglevisualslopepicking":
                ToggleVisualSidedefSlopePicking();
                return true;
            case "map3d.toggle-visual-vertex-slope-picking":
            case "map3d.togglevisualvertexslopepicking":
                ToggleVisualVertexSlopePicking();
                return true;
            case "map3d.toggle-visual-vertex-slope-adjacent-selection":
            case "map3d.togglevisualvertexslopeadjacentselection":
                ToggleVisualVertexSlopeAdjacentSelection();
                return true;
            case "map3d.delete-target":
            case "map3d.deleteitem":
                DeleteVisualTargets3D();
                return true;
            case "map3d.select-texture":
            case "map3d.textureselect":
            case "map3d.browse-texture":
                if (_target3D is null) return false;
                BrowseTexturesRequested?.Invoke(_target3D.Kind != VisualHitKind.Wall);
                return true;
            case "map3d.nudge-offset-left":
                NudgeTargetOffset3D(-8, 0);
                return true;
            case "map3d.nudge-offset-right":
                NudgeTargetOffset3D(8, 0);
                return true;
            case "map3d.nudge-offset-up":
                NudgeTargetOffset3D(0, -8);
                return true;
            case "map3d.nudge-offset-down":
                NudgeTargetOffset3D(0, 8);
                return true;
            case "map3d.move-texture-left-1":
            case "map3d.movetextureleft":
                NudgeTargetOffset3D(-1, 0);
                return true;
            case "map3d.move-texture-right-1":
            case "map3d.movetextureright":
                NudgeTargetOffset3D(1, 0);
                return true;
            case "map3d.move-texture-up-1":
            case "map3d.movetextureup":
                NudgeTargetOffset3D(0, -1);
                return true;
            case "map3d.move-texture-down-1":
            case "map3d.movetexturedown":
                NudgeTargetOffset3D(0, 1);
                return true;
            case "map3d.move-texture-left-8":
            case "map3d.movetextureleft8":
                NudgeTargetOffset3D(-8, 0);
                return true;
            case "map3d.move-texture-right-8":
            case "map3d.movetextureright8":
                NudgeTargetOffset3D(8, 0);
                return true;
            case "map3d.move-texture-up-8":
            case "map3d.movetextureup8":
                NudgeTargetOffset3D(0, -8);
                return true;
            case "map3d.move-texture-down-8":
            case "map3d.movetexturedown8":
                NudgeTargetOffset3D(0, 8);
                return true;
            case "map3d.move-texture-left-grid":
            case "map3d.movetextureleftgs":
                NudgeTargetOffset3D(-_grid.GridSize, 0);
                return true;
            case "map3d.move-texture-right-grid":
            case "map3d.movetexturerightgs":
                NudgeTargetOffset3D(_grid.GridSize, 0);
                return true;
            case "map3d.move-texture-up-grid":
            case "map3d.movetextureupgs":
                NudgeTargetOffset3D(0, -_grid.GridSize);
                return true;
            case "map3d.move-texture-down-grid":
            case "map3d.movetexturedowngs":
                NudgeTargetOffset3D(0, _grid.GridSize);
                return true;
            default:
                return false;
        }
    }

    private bool AdjustDrawSubdivision(bool increase)
    {
        if (_drawMode && _drawCurve)
        {
            _drawCurveSettings = increase
                ? _drawCurveSettings.IncreaseSegmentLength()
                : _drawCurveSettings.DecreaseSegmentLength();
            InvalidateDrawPreview();
            return true;
        }

        switch (_shapeKind)
        {
            case ShapeKind.Rectangle:
                _drawRectangleSettings = increase
                    ? _drawRectangleSettings.IncreaseSubdivisions()
                    : _drawRectangleSettings.DecreaseSubdivisions();
                break;
            case ShapeKind.Ellipse:
                _drawEllipseSettings = increase
                    ? _drawEllipseSettings.IncreaseSubdivisions()
                    : _drawEllipseSettings.DecreaseSubdivisions();
                break;
            case ShapeKind.Grid:
                _drawGridSettings = increase
                    ? _drawGridSettings.IncreaseVerticalSlices()
                    : _drawGridSettings.DecreaseVerticalSlices();
                break;
            default:
                return false;
        }

        InvalidateDrawPreview();
        return true;
    }

    private bool AdjustDrawBevel(bool increase)
    {
        switch (_shapeKind)
        {
            case ShapeKind.Rectangle:
                _drawRectangleSettings = increase
                    ? _drawRectangleSettings.IncreaseBevel(_grid.GridSizeF)
                    : _drawRectangleSettings.DecreaseBevel(_grid.GridSizeF);
                break;
            case ShapeKind.Ellipse:
                _drawEllipseSettings = increase
                    ? _drawEllipseSettings.IncreaseBevel(_grid.GridSizeF)
                    : _drawEllipseSettings.DecreaseBevel(_grid.GridSizeF);
                break;
            case ShapeKind.Grid:
                _drawGridSettings = increase
                    ? _drawGridSettings.IncreaseHorizontalSlices()
                    : _drawGridSettings.DecreaseHorizontalSlices();
                break;
            default:
                return false;
        }

        InvalidateDrawPreview();
        return true;
    }

    private void InvalidateDrawPreview()
    {
        _drawDirty = true;
        RequestNextFrameRendering();
    }

    private bool ShouldRunMapShortcut(string commandId, string pressKey)
    {
        bool repeated = !_pressedMapShortcuts.Add(pressKey);
        return !repeated || EditorCommandCatalog.IsRepeatable(commandId);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (AutomapMode) UpdateAutomapHighlight(_cursorWorld, e.KeyModifiers);
        bool accel = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var scope = _mode3D ? EditorCommandScope.Map3D : EditorCommandScope.Map2D;
        if (EditorCommandCatalog.ResolveShortcut(ShortcutBindings, scope, e.Key.ToString(), accel, shift, alt) is { } commandId
            && IsHeldMapCommand(commandId))
        {
            _heldMapCommands.Remove(commandId);
            if (commandId == "map3d.orbit") _orbit3DPoint = null;
            if (commandId == "map2d.classicpaintselect") EndClassicPaintSelection();
            if (commandId is "map3d.visual-paint-select" or "map3d.visualpaintselect") EndVisualPaintSelection();
            if (commandId is "map2d.pan" or "map2d.pan_view") EndHeldPanView();
            e.Handled = true;
        }

        RemovePressedMapShortcut(e.Key.ToString());
        if (_heldKeys.Remove(e.Key)) e.Handled = true;
        if (!e.Handled) base.OnKeyUp(e);
    }

    private void RemovePressedMapShortcut(string key)
    {
        string map2D = EditorCommandCatalog.ShortcutReleasePrefix(EditorCommandScope.Map2D, key);
        string map3D = EditorCommandCatalog.ShortcutReleasePrefix(EditorCommandScope.Map3D, key);
        _pressedMapShortcuts.RemoveWhere(pressKey =>
            pressKey.StartsWith(map2D, StringComparison.Ordinal) ||
            pressKey.StartsWith(map3D, StringComparison.Ordinal));
    }

    private void Reset3DCamera()
    {
        if (_map != null)
        {
            if (_gameConfig?.Start3DModeThingType > 0)
            {
                foreach (Thing thing in _map.Things)
                {
                    if (thing.Type == _gameConfig.Start3DModeThingType && thing.Sector == null)
                        thing.DetermineSector(_map);
                }

                var current = new DBuilder.Geometry.Vector3D(_cam3DPos.X, _cam3DPos.Y, _cam3DPos.Z);
                if (VisualCameraMovement.TryPlanStartThingPose(
                    _map.Things,
                    _gameConfig.Start3DModeThingType,
                    current,
                    out VisualCameraStartThingPlan startThingPlan))
                {
                    _cam3DPos = new Vector3(
                        (float)startThingPlan.Pose.Position.x,
                        (float)startThingPlan.Pose.Position.y,
                        (float)startThingPlan.Pose.Position.z);
                    _yaw = (float)startThingPlan.Pose.Yaw;
                    _pitch = (float)startThingPlan.Pose.Pitch;
                    return;
                }
            }

            var (minX, minY, maxX, maxY) = _map.Bounds();
            var center = new Vec2D((minX + maxX) * 0.5, (minY + maxY) * 0.5);
            double currentZ = _cam3DPos.Z == 0 ? 200.0 : _cam3DPos.Z;
            DBuilder.Geometry.Vector3D position = VisualCameraMovement.PlanEngagePosition(center, currentZ, _map.GetSectorAt(center));
            _cam3DPos = new Vector3((float)position.x, (float)position.y, (float)position.z);
        }
        else _cam3DPos = new Vector3(0, 0, 200f);
        _yaw = 0; _pitch = -0.3;
    }

    private Vector3 Cam3DForward()
    {
        float cp = (float)Math.Cos(_pitch);
        return new Vector3(cp * (float)Math.Cos(_yaw), cp * (float)Math.Sin(_yaw), (float)Math.Sin(_pitch));
    }

    private void UpdateAutomapHighlight(Vec2D world, KeyModifiers modifiers)
    {
        if (_map == null || !AutomapMode) return;

        bool editSectors = modifiers.HasFlag(KeyModifiers.Shift) && _mapFormat == MapFormat.Udmf;
        bool invert = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        AutomapModeSettings settings = _automapSettings;
        AutomapModeOptions options = AutomapModeModel.ToOptions(
            settings,
            invert,
            isUdmf: _mapFormat == MapFormat.Udmf,
            isDoom: _mapFormat == MapFormat.Doom);
        List<Linedef> validLines = AutomapModeModel.GetValidLinedefs(_map, options);
        AutomapHighlightResult next = AutomapModeModel.PlanHighlight(_map, validLines, world, highlightRange: 20, viewScale: _zoom, editSectors);

        if (_automapHighlight == next && _automapEditSectors == editSectors && _automapInvertLineVisibility == invert) return;
        _automapHighlight = next;
        _automapEditSectors = editSectors;
        _automapInvertLineVisibility = invert;
        _geometryDirty = true;
        Picked?.Invoke(next.Kind switch
        {
            AutomapHighlightKind.Linedef when next.Line != null => $"automap linedef {_map.Linedefs.IndexOf(next.Line)}",
            AutomapHighlightKind.Sector when next.Sector != null => $"automap sector {_map.Sectors.IndexOf(next.Sector)}",
            _ => "automap"
        });
        RequestNextFrameRendering();
    }

    private void UpdateWadAuthorHighlight(Vec2D world)
    {
        if (_map == null || !WadAuthorMode) return;

        WadAuthorHighlight next = WadAuthorModeModel.PickHighlight(_map, world, _zoom);
        if (Equals(_wadAuthorHighlight, next)) return;

        _wadAuthorHighlight = next;
        _wadAuthorDirty = true;
        Picked?.Invoke(WadAuthorModeModel.FormatHighlightStatus(_map, next));
        RequestNextFrameRendering();
    }

    private void ShowWadAuthorLinedefPopup()
    {
        if (_map == null || _wadAuthorHighlight.Target is not Linedef line) return;

        WadAuthorModeModel.SelectOnlyLinedef(_map, line);
        MarkGeometryDirty();
        Changed?.Invoke();

        var controls = new List<AvaloniaControl>();
        foreach (WadAuthorLinedefPopupItem popupItem in WadAuthorModeModel.LinedefPopupItems)
        {
            if (popupItem.Action == null)
            {
                controls.Add(new AvaloniaSeparator());
                continue;
            }

            WadAuthorLinedefPopupAction action = popupItem.Action.Value;
            var menuItem = new AvaloniaMenuItem
            {
                Header = popupItem.Title,
                IsEnabled = WadAuthorModeModel.CanExecuteLinedefPopupAction(action),
            };
            menuItem.Click += (_, _) => ExecuteWadAuthorLinedefPopupAction(line, action);
            controls.Add(menuItem);
        }

        var menu = new AvaloniaContextMenu { ItemsSource = controls };
        menu.Open(this);
    }

    private void ExecuteWadAuthorLinedefPopupAction(Linedef line, WadAuthorLinedefPopupAction action)
    {
        if (_map == null) return;

        Vec2D splitPosition = NearestPointOnLine(line, _cursorWorld);
        if (action == WadAuthorLinedefPopupAction.Properties)
        {
            WadAuthorLinedefPopupResult propertiesResult = WadAuthorModeModel.ExecuteLinedefPopupAction(_map, line, action, splitPosition);
            if (propertiesResult.Status == WadAuthorModeModel.EditPropertiesStatus)
            {
                Changed?.Invoke();
                EditRequested?.Invoke();
            }

            Picked?.Invoke(propertiesResult.Status);
            return;
        }

        EditBegun?.Invoke(WadAuthorModeModel.EditDescription(action));
        WadAuthorLinedefPopupResult result = WadAuthorModeModel.ExecuteLinedefPopupAction(_map, line, action, splitPosition);
        if (result.Changed)
        {
            _map.BuildIndexes();
            _wadAuthorHighlight = WadAuthorHighlight.None;
            _wadAuthorDirty = true;
            MarkGeometryDirty();
            Changed?.Invoke();
        }
        Picked?.Invoke(result.Status);
        RequestNextFrameRendering();
    }

    private void ToggleAutomapSecretOrSector()
    {
        if (_map == null || _automapHighlight == null) return;

        if (_automapHighlight.Kind == AutomapHighlightKind.Linedef && _automapHighlight.Line != null)
        {
            EditBegun?.Invoke("Toggle automap secret");
            AutomapModeModel.ToggleSecretFlag(_automapHighlight.Line, _mapFormat == MapFormat.Udmf);
            Picked?.Invoke("toggled automap secret");
        }
        else if (_automapHighlight.Kind == AutomapHighlightKind.Sector && _automapHighlight.Sector != null)
        {
            EditBegun?.Invoke("Toggle textured automap hidden");
            AutomapModeModel.ToggleTexturedAutomapHiddenFlag(_automapHighlight.Sector);
            Picked?.Invoke("toggled textured automap hidden");
        }
        else
        {
            return;
        }

        _map.BuildIndexes();
        UpdateAutomapHighlight(_cursorWorld, CurrentAutomapModifiers());
        MarkGeometryDirty();
        Changed?.Invoke();
    }

    private void ToggleAutomapHiddenLine()
    {
        if (_map == null || _automapHighlight?.Kind != AutomapHighlightKind.Linedef || _automapHighlight.Line == null) return;

        EditBegun?.Invoke("Toggle automap hidden");
        AutomapModeModel.ToggleHiddenFlag(_automapHighlight.Line, _mapFormat == MapFormat.Udmf);
        Picked?.Invoke("toggled automap hidden");
        _map.BuildIndexes();
        UpdateAutomapHighlight(_cursorWorld, CurrentAutomapModifiers());
        MarkGeometryDirty();
        Changed?.Invoke();
    }

    private KeyModifiers CurrentAutomapModifiers()
    {
        KeyModifiers modifiers = KeyModifiers.None;
        if (_automapEditSectors) modifiers |= KeyModifiers.Shift;
        if (_automapInvertLineVisibility) modifiers |= KeyModifiers.Control;
        return modifiers;
    }

    private void UpdateFlyCamera()
    {
        double now = _clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(0.05, now - _lastTime); // clamp to avoid jumps after a stall
        _lastTime = now;

        float move = dt * 3.2f * _moveSpeed;
        float look = dt * 1.6f;
        if (_heldKeys.Contains(Key.Left)) _yaw += look;
        if (_heldKeys.Contains(Key.Right)) _yaw -= look;
        if (_heldKeys.Contains(Key.Up)) _pitch += look;
        if (_heldKeys.Contains(Key.Down)) _pitch -= look;
        _pitch = Math.Clamp(_pitch, -1.5, 1.5);

        var flatFwd = new Vector3((float)Math.Cos(_yaw), (float)Math.Sin(_yaw), 0);
        var right = new Vector3((float)Math.Sin(_yaw), -(float)Math.Cos(_yaw), 0);
        if (_heldKeys.Contains(Key.W) || _heldMapCommands.Contains("map3d.move-forward") || _heldMapCommands.Contains("map3d.moveforward")) _cam3DPos += flatFwd * move;
        if (_heldKeys.Contains(Key.S) || _heldMapCommands.Contains("map3d.move-backward") || _heldMapCommands.Contains("map3d.movebackward")) _cam3DPos -= flatFwd * move;
        if (_heldKeys.Contains(Key.D) || _heldMapCommands.Contains("map3d.move-right") || _heldMapCommands.Contains("map3d.moveright")) _cam3DPos += right * move;
        if (_heldKeys.Contains(Key.A) || _heldMapCommands.Contains("map3d.move-left") || _heldMapCommands.Contains("map3d.moveleft")) _cam3DPos -= right * move;

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
            if (_heldKeys.Contains(Key.E) || _heldMapCommands.Contains("map3d.move-up") || _heldMapCommands.Contains("map3d.moveup")) _cam3DPos.Z += move;
            if (_heldKeys.Contains(Key.Q) || _heldMapCommands.Contains("map3d.move-down") || _heldMapCommands.Contains("map3d.movedown")) _cam3DPos.Z -= move;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(); // take keyboard focus so the 3D fly camera receives WASD/arrows
        var pt = e.GetCurrentPoint(this);
        if (AutomapMode)
        {
            _cursorWorld = ToWorld(pt.Position);
            UpdateAutomapHighlight(_cursorWorld, e.KeyModifiers);
            return;
        }
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
        if (_drawMode || _threeDFloorEditMode == ThreeDFloorEditMode.DrawSlopes)
        {
            if (pt.Properties.IsRightButtonPressed)
            {
                if (_threeDFloorEditMode == ThreeDFloorEditMode.DrawSlopes) FinishThreeDFloorSlopeDraw(_threeDFloorSlopeDrawingMode);
                else CancelDraw();
                return;
            }
            if (pt.Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount >= 2)
                {
                    if (_threeDFloorEditMode == ThreeDFloorEditMode.DrawSlopes) FinishThreeDFloorSlopeDraw(_threeDFloorSlopeDrawingMode);
                    else FinishDraw();
                    return;
                }
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
            _moveCandidate = _selectionDoneOnPress || (_editSelectionMode && HasTransformableSelection());
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

        if (AutomapMode)
        {
            if (e.InitialPressMouseButton == MouseButton.Left) ToggleAutomapSecretOrSector();
            else if (e.InitialPressMouseButton == MouseButton.Right) ToggleAutomapHiddenLine();
            return;
        }

        if (WadAuthorMode && e.InitialPressMouseButton == MouseButton.Right)
        {
            bool wasDrag = _rightDragging;
            _rightPressed = false;
            _rightDragging = false;
            if (!wasDrag) ShowWadAuthorLinedefPopup();
            return;
        }

        // Right button up: split selected lines or the nearest line if it was a click.
        if (e.InitialPressMouseButton == MouseButton.Right && _rightPressed)
        {
            bool wasDrag = _rightDragging;
            _rightPressed = false; _rightDragging = false;
            if (!wasDrag && !_drawMode && _threeDFloorEditMode != ThreeDFloorEditMode.DrawSlopes && _map != null)
                SplitLinedefs(ToWorld(pos));
            return;
        }

        if (!_pressed) return;
        _pressed = false;

        if (_drawMode || _threeDFloorEditMode == ThreeDFloorEditMode.DrawSlopes)
        {
            if (_drag == DragKind.None)
            {
                if (_threeDFloorEditMode == ThreeDFloorEditMode.DrawSlopes) PlaceThreeDFloorSlopeDrawPoint(ToWorld(pos));
                else PlaceDrawPoint(ToWorld(pos)); // a click adds/closes a loop point
            }
            _drag = DragKind.None;
            return;
        }

        if (_drag == DragKind.None)
        {
            // A click. Point elements were already handled on press; otherwise pick a line/sector (or clear).
            if (!_selectionDoneOnPress)
            {
                var world = ToWorld(pos);
                if (_editMode == EditMode.Things && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    InsertThingAt(world);
                else
                    Pick(world, additive: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            }
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

    // Builds generated geometry inscribed in the dragged box (corners snapped to grid), undoable.
    private void CreateShapeFromBox(Vec2D a, Vec2D b)
    {
        if (_map == null) return;
        var p0 = SnapToGrid(a);
        var p1 = SnapToGrid(b);

        if (_shapeKind == ShapeKind.Grid)
        {
            CreateDrawGridFromBox(p0, p1);
            return;
        }

        DrawShapePlan plan = _shapeKind == ShapeKind.Rectangle
            ? ShapeGenerator.UdbRectangle(p0, p1, _drawRectangleSettings)
            : ShapeGenerator.UdbEllipse(p0, p1, _drawEllipseSettings);
        if (plan.Points.Count < 3) return;

        bool placeThings = _shapeKind == ShapeKind.Rectangle
            ? _drawRectangleSettings.PlaceThingsAtVertices
            : _drawEllipseSettings.PlaceThingsAtVertices;
        if (placeThings)
        {
            EditBegun?.Invoke(_shapeKind == ShapeKind.Rectangle ? "Place things at rectangle vertices" : "Place things at ellipse vertices");
            int count = DrawThingPlacement.PlaceAtPositions(_map, plan.Points, InsertThingType);
            if (count == 0) return;
            _map.BuildIndexes();
            MarkGeometryDirty();
            Changed?.Invoke();
            Picked?.Invoke($"placed {count} thing{(count == 1 ? "" : "s")} at draw vertices");
            if (!string.IsNullOrEmpty(plan.HintText)) Picked?.Invoke(plan.HintText);
            CompleteShapeDraw();
            return;
        }

        EditBegun?.Invoke(_shapeKind == ShapeKind.Rectangle ? "Draw rectangle" : "Draw ellipse");
        var verts = new System.Collections.Generic.List<Vertex>(plan.Points.Count);
        for (int i = 0; i < plan.Points.Count; i++)
        {
            var p = plan.Points[i];
            if (i == plan.Points.Count - 1 && SamePoint(p, plan.Points[0])) continue;
            var existing = _map.NearestVertex(p, 0.01);
            verts.Add(existing ?? _map.AddVertex(p));
        }
        if (verts.Count < 3) return;
        var nearbyLines = _map.Linedefs.Where(line => !LineTouchesOnlyDrawnVertices(line, verts)).ToList();
        Tools.MakeSectorFromLoop(_map, verts, nearbyLines, useOverrides: false, options: CreateSectorCreationOptions());
        _map.MergeOverlappingVertices(0.01);
        _map.SplitLinedefsAtVertices(0.5);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        if (!string.IsNullOrEmpty(plan.HintText)) Picked?.Invoke(plan.HintText);
        CompleteShapeDraw();
    }

    private void CreateDrawGridFromBox(Vec2D p0, Vec2D p1)
    {
        if (_map == null) return;

        DrawGridPlanOptions stored = _drawGridSettings.ToPlanOptions();
        var options = new DrawGridPlanOptions
        {
            HorizontalSlices = stored.HorizontalSlices,
            VerticalSlices = stored.VerticalSlices,
            Triangulate = stored.Triangulate,
            RelativeInterpolation = stored.RelativeInterpolation,
            GridLockMode = stored.GridLockMode,
            HorizontalInterpolation = stored.HorizontalInterpolation,
            VerticalInterpolation = stored.VerticalInterpolation,
            GridSize = _grid.GridSizeF,
            GridSizeF = _grid.GridSizeF,
        };
        var plan = DrawGridPlanner.Create(p0, p1, options);
        if (plan.Shapes.Count == 0) return;

        EditBegun?.Invoke("Draw grid");
        int sectorCount = 0;
        int lineCount = 0;

        foreach (var shape in plan.Shapes)
        {
            if (shape.Count < 2) continue;

            if (shape.Count == 2)
            {
                var a = MaterializeVertex(shape[0]);
                var b = MaterializeVertex(shape[1]);
                if (!ReferenceEquals(a, b))
                {
                    _map.AddLinedef(a, b);
                    lineCount++;
                }
                continue;
            }

            var verts = new System.Collections.Generic.List<Vertex>(shape.Count);
            for (int i = 0; i < shape.Count; i++)
            {
                if (i == shape.Count - 1 && SamePoint(shape[i], shape[0])) continue;
                verts.Add(MaterializeVertex(shape[i]));
            }

            if (verts.Count < 3) continue;
            var nearbyLines = _map.Linedefs.Where(line => !LineTouchesOnlyDrawnVertices(line, verts)).ToList();
            if (Tools.MakeSectorFromLoop(_map, verts, nearbyLines, useOverrides: false, options: CreateSectorCreationOptions()) != null)
                sectorCount++;
        }

        _map.MergeOverlappingVertices(0.01);
        _map.SplitLinedefsAtVertices(0.5);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke(DrawGridPlanner.CreatedStatus(plan, sectorCount, lineCount));
        CompleteShapeDraw();
    }

    private void CompleteShapeDraw()
    {
        if (_shapeKind == ShapeKind.None) return;

        bool continuous = _shapeKind switch
        {
            ShapeKind.Rectangle => _drawRectangleSettings.ContinuousDrawing,
            ShapeKind.Ellipse => _drawEllipseSettings.ContinuousDrawing,
            ShapeKind.Grid => _drawGridSettings.ContinuousDrawing,
            _ => false,
        };
        DrawModeTool tool = _shapeKind switch
        {
            ShapeKind.Rectangle => DrawModeTool.Rectangle,
            ShapeKind.Ellipse => DrawModeTool.Ellipse,
            ShapeKind.Grid => DrawModeTool.Grid,
            _ => DrawModeTool.Sector,
        };

        ApplyDrawLifecycle(DrawModeLifecycle.AfterAccept(tool, continuous));
    }

    private Vertex MaterializeVertex(Vec2D point)
        => _map!.NearestVertex(point, 0.01) ?? _map.AddVertex(point);

    private void RunBridgeCommand()
    {
        string status = BuildBridgeFromSelectedLinedefs();
        if (!status.StartsWith("Created a Bridge", StringComparison.Ordinal)) Picked?.Invoke(status);
    }

    public string BuildBridgeFromSelectedLinedefs()
    {
        if (_map == null) return "No map loaded.";
        var selected = _map.GetSelectedLinedefs();
        if (selected.Count < 2) return "Select two matching linedef chains to build a bridge.";

        var options = new BridgePlanOptions();
        BridgePlan? plan = BridgePlanner.TryCreate(selected, options);
        if (plan == null) return "Select exactly two non-intersecting linedef chains with matching vertex counts.";
        if (plan.Shapes.Count == 0) return "Selected linedefs did not produce bridge sectors.";

        EditBegun?.Invoke("Build bridge");
        var newSectors = new System.Collections.Generic.List<(Sector Sector, BridgeSectorProperties Properties)>();

        foreach (BridgeShape shape in plan.Shapes)
        {
            var verts = new System.Collections.Generic.List<Vertex>(shape.Loop.Count);
            for (int i = 0; i < shape.Loop.Count; i++)
            {
                if (i == shape.Loop.Count - 1 && SamePoint(shape.Loop[i], shape.Loop[0])) continue;
                verts.Add(MaterializeVertex(shape.Loop[i]));
            }

            if (verts.Count < 3) continue;
            Sector? sector = SectorBuilder.CreateSector(_map, verts);
            if (sector == null) continue;

            sector.FloorHeight = shape.Properties.FloorHeight;
            sector.CeilHeight = Math.Max(shape.Properties.CeilingHeight, sector.FloorHeight + 8);
            sector.Brightness = shape.Properties.Brightness;
            ApplyNewSectorDefaults(sector);
            newSectors.Add((sector, shape.Properties));
        }

        _map.MergeOverlappingVertices(0.01);
        _map.SplitLinedefsAtVertices(0.5);
        _map.BuildIndexes();

        foreach ((Sector sector, BridgeSectorProperties properties) in newSectors)
            ApplyBridgeTextures(sector, properties, _gameConfig?.UseLongTextureNames ?? false);

        _map.ClearAllSelected();
        foreach ((Sector sector, _) in newSectors) sector.Selected = true;
        MarkGeometryDirty();
        Changed?.Invoke();

        string status = BridgePlanner.CreatedStatus(options.Subdivisions);
        Picked?.Invoke(status);
        return status;
    }

    public static void ApplyBridgeTextures(Sector sector, BridgeSectorProperties properties, bool useLongTextureNames)
    {
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.LowRequired() && IsBlankTexture(side.LowTexture))
            {
                side.SetTextureLow(properties.LowTexture);
                side.LongLowTexture = Lump.MakeLongName(side.LowTexture, useLongTextureNames);
            }
            if (side.HighRequired() && IsBlankTexture(side.HighTexture))
            {
                side.SetTextureHigh(properties.HighTexture);
                side.LongHighTexture = Lump.MakeLongName(side.HighTexture, useLongTextureNames);
            }

            Sidedef? other = side.Other;
            if (other == null) continue;
            if (other.LowRequired() && IsBlankTexture(other.LowTexture))
            {
                other.SetTextureLow(properties.LowTexture);
                other.LongLowTexture = Lump.MakeLongName(other.LowTexture, useLongTextureNames);
            }
            if (other.HighRequired() && IsBlankTexture(other.HighTexture))
            {
                other.SetTextureHigh(properties.HighTexture);
                other.LongHighTexture = Lump.MakeLongName(other.HighTexture, useLongTextureNames);
            }
        }
    }

    private void ApplyNewSectorDefaults(Sector? sector)
    {
        if (_map == null || sector == null) return;
        SectorDrawingDefaults.Apply(_map, sector, _mapOptions, _gameConfig);
    }

    private static bool IsBlankTexture(string? name)
        => string.IsNullOrWhiteSpace(name) || name == "-";

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
    private static byte[]? _visualThingClipboard;

    /// <summary>Copies the current selection (with its dependency closure) to the clipboard.</summary>
    public string CopySelection()
    {
        if (_map == null) return "No map loaded.";
        var buf = SelectionClipboard.CopySelection(_map);
        if (buf == null)
        {
            const string empty = "nothing selected to copy";
            Picked?.Invoke(empty);
            return empty;
        }
        _clipboard = buf;
        const string status = "copied selection";
        Picked?.Invoke(status);
        return status;
    }

    /// <summary>Pastes the clipboard one grid cell from the originals and selects the result (undoable).</summary>
    public string PasteClipboard()
        => PasteClipboard(PasteOptions);

    public string PasteClipboard(PasteOptions options)
    {
        if (_map == null) return "No map loaded.";
        if (_clipboard == null)
        {
            const string empty = "clipboard empty";
            Picked?.Invoke(empty);
            return empty;
        }
        EditBegun?.Invoke("Paste");
        var res = SelectionClipboard.Paste(_map, _clipboard, new Vec2D(_grid.GridSize, _grid.GridSize), options, _gameConfig);
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = $"pasted {res.LinedefCount} lines, {res.SectorCount} sectors, {res.ThingCount} things";
        Picked?.Invoke(status);
        return status;
    }

    /// <summary>Duplicates the current selection one grid cell away without replacing the clipboard buffer.</summary>
    public string DuplicateSelection()
    {
        if (_map == null) return "No map loaded.";
        var res = SelectionClipboard.DuplicateSelection(
            _map,
            new Vec2D(_grid.GridSize, _grid.GridSize),
            PasteOptions,
            _gameConfig,
            () => EditBegun?.Invoke("Duplicate selection"));
        if (res is null)
        {
            const string empty = "nothing selected to duplicate";
            Picked?.Invoke(empty);
            return empty;
        }
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = $"duplicated {res.Value.LinedefCount} lines, {res.Value.SectorCount} sectors, {res.Value.ThingCount} things";
        Picked?.Invoke(status);
        return status;
    }

    public string DuplicateThreeDFloorGeometry()
    {
        if (_map == null) return "No map loaded.";
        var res = ThreeDFloorSelectionClipboard.DuplicateSelectionWithThreeDFloors(
            _map,
            new Vec2D(_grid.GridSize, _grid.GridSize),
            PasteOptions,
            _gameConfig,
            () => EditBegun?.Invoke("Duplicate 3D floor geometry"));
        if (res is null)
        {
            const string empty = "nothing selected to duplicate";
            Picked?.Invoke(empty);
            return empty;
        }

        MarkGeometryDirty();
        Changed?.Invoke();
        string status = $"duplicated {res.Value.LinedefCount} lines, {res.Value.SectorCount} sectors, {res.Value.ThingCount} things with 3D floors";
        Picked?.Invoke(status);
        return status;
    }

    public int ThreeDFloorSlopeDrawPointCount => _drawPoints.Count;

    private void ClearThreeDFloorSlopeDraw()
    {
        _drawMode = false;
        _drawCurve = false;
        _shapeKind = ShapeKind.None;
        _drawPoints.Clear();
        _drawClosed = false;
        _threeDFloorSlopeFlipped = false;
        _drawDirty = true;
        _drawLineCount = 0;
        DrawModeChanged?.Invoke();
        ActionStateChanged?.Invoke();
    }

    private void PlaceThreeDFloorSlopeDrawPoint(Vec2D world)
    {
        if (_threeDFloorEditMode != ThreeDFloorEditMode.DrawSlopes)
            SetThreeDFloorEditMode(ThreeDFloorEditMode.DrawSlopes);

        _drawPoints.Add(SnapWorld(world));
        _drawDirty = true;
        Picked?.Invoke("Added 3D slope draw point.");
        RequestNextFrameRendering();
    }

    private string FlipThreeDFloorSlopeDraw()
    {
        if (_threeDFloorEditMode != ThreeDFloorEditMode.DrawSlopes)
            SetThreeDFloorEditMode(ThreeDFloorEditMode.DrawSlopes);

        _threeDFloorSlopeFlipped = !_threeDFloorSlopeFlipped;
        string status = _threeDFloorSlopeFlipped ? "3D slope draw is flipped." : "3D slope draw is not flipped.";
        Picked?.Invoke(status);
        return status;
    }

    private string FinishThreeDFloorSlopeDraw(ThreeDFloorSlopeDrawingMode mode)
    {
        _threeDFloorSlopeDrawingMode = mode;
        if (_map == null)
        {
            ClearThreeDFloorSlopeDraw();
            return "No map loaded.";
        }

        if (_threeDFloorEditMode != ThreeDFloorEditMode.DrawSlopes)
            SetThreeDFloorEditMode(ThreeDFloorEditMode.DrawSlopes);

        if (_drawPoints.Count <= 1)
        {
            ClearThreeDFloorSlopeDraw();
            const string message = "Draw at least two slope points.";
            Picked?.Invoke(message);
            return message;
        }

        IReadOnlyList<Sector> sectors = SelectedSectorsOrHighlighted();
        if (sectors.Count == 0)
        {
            const string message = "Select sectors to slope.";
            Picked?.Invoke(message);
            return message;
        }

        Sector? slopeDataSector = ThreeDFloorSlopes.GetSlopeDataSector(_map);
        var groups = ThreeDFloorSlopes.LoadGroupsFromSector(slopeDataSector).ToList();
        EditBegun?.Invoke("Draw 3D floor slope");
        ThreeDFloorSlopeDrawResult result = ThreeDFloorSlopes.FinishDraw(
            _map,
            groups,
            MaterializedDrawPoints(includeCursor: false),
            sectors,
            mode,
            slopeDataSector,
            _threeDFloorSlopeFlipped);

        ClearThreeDFloorSlopeDraw();
        if (result.CreatedGroups.Count == 0)
        {
            const string message = "No 3D slope groups created.";
            Picked?.Invoke(message);
            return message;
        }

        MarkGeometryDirty();
        Changed?.Invoke();
        string status = ThreeDFloorSlopeDrawStatusText(result.CreatedGroups.Count, slopeDataSector != null);
        Picked?.Invoke(status);
        return status;
    }

    public static string ThreeDFloorSlopeDrawStatusText(int groupCount, bool stored)
    {
        string suffix = stored ? "." : " without a slope data sector.";
        return "Created " + groupCount + " 3D slope group" + (groupCount == 1 ? "" : "s") + suffix;
    }

    /// <summary>Serializes the current selection for saving as a prefab, or null when nothing is selected.</summary>
    public byte[]? GetSelectionPrefab() => _map == null ? null : SelectionClipboard.CopySelection(_map);

    /// <summary>Inserts a prefab buffer with its lower-left corner anchored at the grid-snapped cursor (undoable).</summary>
    public void InsertPrefab(byte[] data)
    {
        if (_map == null) return;
        EditBegun?.Invoke("Insert prefab");
        var res = SelectionClipboard.PasteAtAnchor(_map, data, SnapToGrid(_cursorWorld), PasteOptions, _gameConfig);
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke($"inserted prefab: {res.LinedefCount} lines, {res.SectorCount} sectors, {res.ThingCount} things");
    }

    // Auto-aligns textures along the wall run from the first selected linedef's front side (A = X, Shift+A = Y), undoable.
    public string AutoAlignSelectedTextures(bool vertical)
    {
        if (_map == null) return "No map loaded.";
        Linedef? start = null;
        foreach (var l in _map.Linedefs) if (l.Selected && l.Front != null) { start = l; break; }
        if (start?.Front == null)
        {
            const string message = "select a linedef with a front sidedef to align";
            Picked?.Invoke(message);
            return message;
        }

        string tex = SidedefTextureAlignment.PrimaryTexture(start.Front);
        var img = _resources?.GetWallTexture(tex);

        EditBegun?.Invoke(vertical ? "Auto-align textures (Y)" : "Auto-align textures (X)");
        int n = vertical
            ? SidedefTextureAlignment.AutoAlignY(start.Front, img?.Height ?? 0)
            : SidedefTextureAlignment.AutoAlignX(start.Front, img?.Width ?? 0);
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = $"aligned {n} sidedef{(n == 1 ? "" : "s")} {(vertical ? "vertically" : "horizontally")} (tex {tex})";
        Picked?.Invoke(status);
        return status;
    }

    public string FitSelectedTextures()
    {
        if (_map == null) return "No map loaded.";
        if (_map.SelectedLinedefsCount == 0) return "Select one or more linedefs to fit textures.";
        if (_resources == null) return "No resources loaded for texture dimensions.";

        int changed = 0;
        int skipped = 0;
        EditBegun?.Invoke("Fit selected textures");

        foreach (var line in _map.Linedefs)
        {
            if (!line.Selected) continue;
            changed += FitSideTextures(line.Front, ref skipped);
            changed += FitSideTextures(line.Back, ref skipped);
        }

        MarkGeometryDirty();
        Changed?.Invoke();
        string status = changed == 0
            ? $"no selected wall textures fitted ({skipped} missing texture image{(skipped == 1 ? "" : "s")})"
            : $"fit {changed} wall texture{(changed == 1 ? "" : "s")} ({skipped} missing image{(skipped == 1 ? "" : "s")})";
        Picked?.Invoke(status);
        return status;
    }

    public string AlignSelectedThingsToWall()
    {
        if (_map == null) return "No map loaded.";
        if (_gameConfig == null) return "No game configuration loaded.";
        IReadOnlyList<Thing> things = _map.GetSelectedThings();
        if (things.Count == 0 && _editMode == EditMode.Things && NearestVisibleThing(_cursorWorld, ThingHighlightRangeWorld()) is { } highlighted)
            things = new[] { highlighted };

        if (things.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        bool hasEligible = things.Any(thing =>
            ThingWallAlignment.IsAlignable(_gameConfig.GetThing(thing.Type)?.RenderMode ?? DBuilder.IO.ThingRenderMode.Normal));
        if (!hasEligible)
        {
            const string message = "This action only works for models or things with FLATSPRITE/WALLSPRITE flags!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(things.Count == 1 ? "Align thing" : $"Align {things.Count} things");
        ThingWallAlignmentResult result = ThingWallAlignment.AlignThingsToNearestWalls(_map, _gameConfig, things);
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke(result.Message);
        return result.Message;
    }

    private int FitSideTextures(Sidedef? side, ref int skipped)
    {
        if (side == null) return 0;

        int changed = 0;
        changed += FitSideTexturePart(side, SidedefPart.Middle, ref skipped);
        changed += FitSideTexturePart(side, SidedefPart.Upper, ref skipped);
        changed += FitSideTexturePart(side, SidedefPart.Lower, ref skipped);
        return changed;
    }

    private int FitSideTexturePart(Sidedef side, SidedefPart part, ref int skipped)
    {
        string textureName = side.GetTexture(part);
        if (IsBlankTexture(textureName)) return 0;
        if (!side.IsTextureRequired(part) && part != SidedefPart.Middle) return 0;

        var image = _resources?.GetWallTexture(textureName);
        if (image == null)
        {
            skipped++;
            return 0;
        }

        bool changed = SidedefTextureFitting.Fit(
            side,
            part,
            new TextureFitImage(image.Width, image.Height, image.ScaleX, image.ScaleY));
        return changed ? 1 : 0;
    }

    public string AlignSelectedFlatsToLinedefs(bool floors, bool frontSide)
    {
        if (_map == null) return "No map loaded.";
        var lines = _map.GetSelectedLinedefs();
        if (lines.Count == 0) return "This action requires a selection!";

        string target = (floors ? "Floors" : "Ceilings") + " to " + (frontSide ? "Front" : "Back") + " Side";
        EditBegun?.Invoke("Align " + target);
        SectorFlatAlignmentResult result = SectorFlatAlignment.AlignToLinedefs(
            lines,
            floors,
            frontSide,
            sector =>
            {
                var image = _resources?.GetFlat(sector.FloorTexture);
                return image is { Width: > 0, Height: > 0 }
                    ? new SectorFlatAlignmentTexture(image.Width, image.Height)
                    : null;
            });

        if (result.Applied)
        {
            MarkGeometryDirty();
            Changed?.Invoke();
        }

        Picked?.Invoke(result.Message);
        return result.Message;
    }

    public string PointThingsToCursor(bool awayFromCursor)
    {
        if (_map == null) return "No map loaded.";
        if (_gameConfig == null) return "No game configuration loaded.";
        if (!_cursorWorld.IsFinite())
        {
            const string message = "Now click in the editing area!";
            Picked?.Invoke(message);
            return message;
        }

        IReadOnlyList<Thing> things = _map.GetSelectedThings();
        if (things.Count == 0 && _editMode == EditMode.Things && NearestVisibleThing(_cursorWorld, ThingHighlightRangeWorld()) is { } highlighted)
            things = new[] { highlighted };

        if (things.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(things.Count == 1 ? "Rotate thing" : $"Rotate {things.Count} things");
        int changed = ThingCursorRotation.PointThingsToCursor(
            things,
            _cursorWorld,
            _gameConfig,
            awayFromCursor);
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = changed == 1 ? "Rotated a thing." : $"Rotated {changed} things.";
        Picked?.Invoke(status);
        return status;
    }

    // Flips selected linedefs or sector boundary linedefs, matching UDB's fliplinedefs action.
    public string FlipLinedefs()
    {
        if (_map == null) return "No map loaded.";

        if (_editMode == EditMode.Sectors)
            return FlipSectorLinedefs();

        bool deselect = false;
        List<Linedef> selected = _map.GetSelectedLinedefs();
        int selectedCount = selected.Count;
        if (selected.Count == 0 && _editMode == EditMode.Linedefs && _map.NearestLinedef(_cursorWorld, HighlightRangeWorld()) is { } highlighted)
        {
            highlighted.Selected = true;
            selected.Add(highlighted);
            selectedCount = selected.Count;
            deselect = true;
        }

        if (selected.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(selected.Count == 1 ? "Flip linedef" : "Flip " + selected.Count + " linedefs");
        int count = _map.FlipSelectedLinedefs();
        if (deselect)
            selected[0].Selected = false;

        if (count == 0)
        {
            string message = selectedCount == 1
                ? "Selected linedef already points in the right direction!"
                : "Selected linedefs already point in the right direction!";
            Picked?.Invoke(message);
            return message;
        }

        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = count == 1 ? "Flipped a linedef." : "Flipped " + count + " linedefs.";
        Picked?.Invoke(status);
        return status;
    }

    private string FlipSectorLinedefs()
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Sector> sectors = SelectedSectorsOrHighlighted();
        if (sectors.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(sectors.Count == 1 ? "Flip sector linedefs" : "Flip linedefs of " + sectors.Count + " sectors");
        int count = _map.FlipLinedefsOfSectors(sectors);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = sectors.Count == 1 ? "Flipped sector linedefs." : "Flipped linedefs of " + sectors.Count + " sectors.";
        if (count == 0)
            status = "Selected sector linedefs already point in the right direction!";
        Picked?.Invoke(status);
        return status;
    }

    public string FlipSidedefs()
    {
        if (_map == null) return "No map loaded.";

        bool deselect = false;
        List<Linedef> selected = _map.GetSelectedLinedefs();
        if (selected.Count == 0 && _editMode == EditMode.Linedefs && _map.NearestLinedef(_cursorWorld, HighlightRangeWorld()) is { } highlighted)
        {
            highlighted.Selected = true;
            selected.Add(highlighted);
            deselect = true;
        }

        if (selected.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        int count = _map.FlipSelectedSidedefs();
        if (deselect)
            selected[0].Selected = false;

        if (count == 0)
        {
            const string message = "No sidedefs to flip! Only 2-sided linedefs can be flipped.";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(count == 1 ? "Flip sidedef" : "Flip " + count + " sidedefs");
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = count == 1 ? "Flipped a sidedef." : "Flipped " + count + " sidedefs.";
        Picked?.Invoke(status);
        return status;
    }

    public string MoveSelectionByGridSize(int gridX, int gridY)
    {
        if (_map == null) return "No map loaded.";

        HashSet<Vertex> vertices = _map.SelectedGeometryVertices();
        int thingCount = _map.SelectedThingsCount;
        if (vertices.Count == 0 && thingCount == 0)
        {
            const string message = "Select elements to move first.";
            Picked?.Invoke(message);
            return message;
        }

        var delta = new Vec2D(gridX * _grid.GridSizeF, gridY * _grid.GridSizeF);
        EditBegun?.Invoke("Move selection");
        foreach (Vertex vertex in vertices)
            vertex.Position += delta;
        int movedThings = _map.MoveSelectedThingsBy(delta);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();

        string status = movedThings == 0
            ? $"Moved {vertices.Count} {(vertices.Count == 1 ? "vertex" : "vertices")}."
            : vertices.Count == 0
                ? $"Moved {movedThings} {(movedThings == 1 ? "thing" : "things")}."
                : $"Moved {vertices.Count} {(vertices.Count == 1 ? "vertex" : "vertices")} and {movedThings} {(movedThings == 1 ? "thing" : "things")}.";
        Picked?.Invoke(status);
        return status;
    }

    public string CurveSelectedLinedefs()
    {
        if (_map == null) return "No map loaded.";

        bool deselect = false;
        List<Linedef> selected = _map.GetSelectedLinedefs();
        if (selected.Count == 0 && _editMode == EditMode.Linedefs && _map.NearestLinedef(_cursorWorld, HighlightRangeWorld()) is { } highlighted)
        {
            highlighted.Selected = true;
            selected.Add(highlighted);
            deselect = true;
        }

        if (selected.Count == 0)
        {
            const string message = "This action requres a selection!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(selected.Count == 1 ? "Curve linedef" : "Curve " + selected.Count + " linedefs");
        CurveLinedefsResult result = CurveLinedefs.ApplyToSelectedLinedefs(
            _map,
            _curveLinedefsSettings,
            MergeGeometryMode,
            snapToAccuracy: true);
        if (deselect)
            selected[0].Selected = false;

        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = result.CurvedLinedefs == 1
            ? "Curved a linedef."
            : "Curved " + result.CurvedLinedefs + " linedefs.";
        Picked?.Invoke(status);
        return status;
    }

    public string DissolveItem()
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Vertex> vertices = [];
        IReadOnlyList<Linedef> linedefs = [];
        IReadOnlyList<Sector> sectors = [];

        int targetCount = _editMode switch
        {
            EditMode.Vertices => (vertices = SelectedVerticesOrHighlighted()).Count,
            EditMode.Linedefs => (linedefs = SelectedLinedefsOrHighlighted()).Count,
            EditMode.Sectors => (sectors = SelectedSectorsOrHighlighted()).Count,
            _ => 0,
        };

        if (targetCount == 0)
        {
            return "";
        }

        string target = _editMode switch
        {
            EditMode.Vertices => targetCount == 1 ? "vertex" : "vertices",
            EditMode.Linedefs => targetCount == 1 ? "linedef" : "linedefs",
            EditMode.Sectors => targetCount == 1 ? "sector" : "sectors",
            _ => targetCount == 1 ? "item" : "items",
        };

        EditBegun?.Invoke(targetCount == 1 ? "Dissolve " + target : "Dissolve " + targetCount + " " + target);
        int dissolved = _editMode switch
        {
            EditMode.Vertices => DissolveVertices(vertices),
            EditMode.Linedefs => DissolveLinedefs(linedefs),
            EditMode.Sectors => DissolveSectors(sectors),
            _ => 0,
        };
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = dissolved == 1 ? "Dissolved a " + target + "." : "Dissolved " + dissolved + " " + target + ".";
        Picked?.Invoke(status);
        return status;
    }

    private int DissolveVertices(IReadOnlyList<Vertex> vertices)
    {
        if (_map == null) return 0;
        foreach (Vertex vertex in vertices)
            vertex.Selected = true;
        return _map.DissolveSelectedVertices();
    }

    private int DissolveLinedefs(IReadOnlyList<Linedef> linedefs)
    {
        if (_map == null) return 0;
        foreach (Linedef linedef in linedefs)
            linedef.Selected = true;
        return _map.DissolveSelectedLinedefs();
    }

    private int DissolveSectors(IReadOnlyList<Sector> sectors)
    {
        if (_map == null) return 0;
        foreach (Sector sector in sectors)
            sector.Selected = true;
        return _map.DissolveSelectedSectors();
    }

    public string KeepSelectedLinedefsBySidedness(bool doubleSided)
    {
        if (_map == null) return "No map loaded.";

        int kept = _map.KeepSelectedLinedefsBySidedness(doubleSided);
        MarkGeometryDirty();
        Changed?.Invoke();
        string kind = doubleSided ? "double-sided" : "single-sided";
        string status = "Selected only " + kind + " linedefs (" + kept + ")";
        Picked?.Invoke(status);
        return status;
    }

    public string AlignLinedefs()
    {
        if (_map == null) return "No map loaded.";

        if (_editMode == EditMode.Sectors)
            return AlignSectorLinedefs();

        List<Linedef> selected = _map.GetSelectedLinedefs();
        if (selected.Count == 0 && _editMode == EditMode.Linedefs && _map.NearestLinedef(_cursorWorld, HighlightRangeWorld()) is { } highlighted)
        {
            highlighted.Selected = true;
            selected.Add(highlighted);
        }

        if (selected.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(selected.Count == 1 ? "Align linedef" : "Align " + selected.Count + " linedefs");
        int count = _map.AlignSelectedLinedefs();
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = count == 1 ? "Aligned a linedef." : "Aligned " + count + " linedefs.";
        Picked?.Invoke(status);
        return status;
    }

    private string AlignSectorLinedefs()
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Sector> sectors = SelectedSectorsOrHighlighted();
        if (sectors.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(sectors.Count == 1 ? "Align sector linedefs" : "Align linedefs of " + sectors.Count + " sectors");
        int count = _map.AlignLinedefsOfSectors(sectors);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = count == 1 ? "Aligned sector linedefs." : "Aligned linedefs of " + count + " sectors.";
        Picked?.Invoke(status);
        return status;
    }

    public string JoinOrMergeSelectedSectors(bool merge)
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Sector> sectors = _map.GetSelectedSectors();
        if (sectors.Count < 2)
        {
            return "";
        }

        EditBegun?.Invoke(merge ? "Merge sectors" : "Join sectors");
        Sector? keep = merge ? _map.MergeSectors(sectors) : _map.JoinSectors(sectors);
        _map.BuildIndexes();
        if (keep != null)
        {
            _map.ClearAllSelected();
            keep.Selected = true;
        }

        MarkGeometryDirty();
        Changed?.Invoke();
        string status = merge ? "Merged " + sectors.Count + " sectors." : "Joined " + sectors.Count + " sectors.";
        Picked?.Invoke(status);
        return status;
    }

    public string AdjustSectorHeights(SectorHeightPart part, int delta)
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Sector> sectors = SelectedSectorsOrHighlighted();
        if (sectors.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(SectorHeightAdjustment.UndoDescription(part));
        SectorHeightAdjustmentResult result = SectorHeightAdjustment.Apply(sectors, part, delta);
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke(result.StatusMessage);
        return result.StatusMessage;
    }

    public string AdjustSectorBrightness(bool raise)
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Sector> sectors = SelectedSectorsOrHighlighted();
        if (sectors.Count == 0)
        {
            const string message = "This action requires a selection!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(SectorBrightnessAdjustment.UndoDescription);
        SectorBrightnessAdjustmentResult result = SectorBrightnessAdjustment.Apply(sectors, _gameConfig?.BrightnessLevels ?? [], raise);
        MarkGeometryDirty();
        Changed?.Invoke();
        Picked?.Invoke(result.StatusMessage);
        return result.StatusMessage;
    }

    /// <summary>The thing type used by the insert tool; remembers the last value edited via the dialog.</summary>
    public int InsertThingType { get; set; } = 1;

    // Insert tool (I): in Things mode drops a thing at the snapped cursor; otherwise inserts a vertex,
    // splitting the nearest line if the cursor is close to one, else placing a free vertex. Undoable.
    public string InsertAtCursor()
    {
        if (_map == null) return "No map loaded.";
        var pos = SnapToGrid(_cursorWorld);

        if (_editMode == EditMode.Things)
            return InsertThingAt(pos, snap: false);

        var line = _map.NearestLinedef(_cursorWorld, SplitLinedefsRangeWorld());
        if (line != null)
        {
            EditBegun?.Invoke("Insert vertex (split)");
            _map.SplitLinedef(line, NearestPointOnLine(line, _cursorWorld));
            _map.BuildIndexes();
            MarkGeometryDirty();
            Changed?.Invoke();
            const string status = "split linedef";
            Picked?.Invoke(status);
            return status;
        }

        EditBegun?.Invoke("Insert vertex");
        _map.ClearAllSelected();
        var v = _map.AddVertex(pos);
        v.Selected = true;
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string vertexStatus = $"inserted vertex at ({pos.x:0}, {pos.y:0})";
        Picked?.Invoke(vertexStatus);
        return vertexStatus;
    }

    private string InsertThingAt(Vec2D world, bool snap = true, double? height = null)
    {
        if (_map == null) return "No map loaded.";
        var pos = snap ? SnapToGrid(world) : world;

        EditBegun?.Invoke("Insert thing");
        _map.ClearAllSelected();
        var t = _map.AddThing(pos, InsertThingType);
        if (height.HasValue) t.Height = height.Value;
        t.Selected = true;
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = $"inserted thing type {InsertThingType} at ({pos.x:0}, {pos.y:0})";
        Picked?.Invoke(status);
        return status;
    }

    public string PlaceVisualStart()
    {
        if (_map == null) return "No map loaded.";
        if (_gameConfig == null) return "No game configuration loaded.";
        if (_gameConfig.Start3DModeThingType == 0) return "No Visual Mode camera start thing is configured.";

        var pos = SnapToGrid(_cursorWorld);
        EditBegun?.Invoke("Place Visual Mode camera");
        _map.ClearAllSelected();
        Thing thing = _map.PlaceUniqueThing(_gameConfig.Start3DModeThingType, pos);
        thing.DetermineSector(_map);
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = $"Placed Visual Mode camera start thing at ({pos.x:0}, {pos.y:0}).";
        Picked?.Invoke(status);
        return status;
    }

    public string SplitLinedefs()
        => SplitLinedefs(_cursorWorld);

    private string SplitLinedefs(Vec2D cursorWorld)
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Linedef> selected = _map.GetSelectedLinedefs();
        if (selected.Count > 0)
        {
            string editName = selected.Count == 1 ? "Split linedef" : "Split " + selected.Count + " linedefs";
            EditBegun?.Invoke(editName);
            int count = _map.SplitLinedefsAtMidpoints(selected);
            _map.BuildIndexes();
            MarkGeometryDirty();
            Changed?.Invoke();
            string status = count == 1 ? "Split a linedef." : "Split " + count + " linedefs.";
            Picked?.Invoke(status);
            return status;
        }

        Linedef? line = _map.NearestLinedef(cursorWorld, SplitLinedefsRangeWorld());
        if (line == null)
        {
            return "";
        }

        EditBegun?.Invoke("Split linedef");
        _map.SplitLinedef(line, NearestPointOnLine(line, cursorWorld));
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        const string splitStatus = "Split a linedef.";
        Picked?.Invoke(splitStatus);
        return splitStatus;
    }

    public string PlaceThingsFromSelection()
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Vector2D> positions = _editMode switch
        {
            EditMode.Vertices => DrawThingPlacement.PositionsFromVertices(SelectedVerticesOrHighlighted()),
            EditMode.Linedefs => DrawThingPlacement.PositionsFromLinedefs(SelectedLinedefsOrHighlighted()),
            EditMode.Sectors => DrawThingPlacement.PositionsFromSectors(SelectedSectorsOrHighlighted()),
            _ => [],
        };

        if (positions.Count == 0)
        {
            const string message = "This action requires selection of some description!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke(positions.Count == 1 ? "Place thing" : "Place things");
        int count = DrawThingPlacement.PlaceAtPositions(_map, positions, InsertThingType);
        if (count == 0)
        {
            const string message = "This action requires selection of some description!";
            Picked?.Invoke(message);
            return message;
        }

        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = "Placed " + count + " things.";
        Picked?.Invoke(status);
        return status;
    }

    public string SelectThreeDFloorControlSectors()
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Sector> sectors = SelectedSectorsOrHighlightedThreeDFloorTargets();
        if (sectors.Count == 0)
        {
            const string message = "Select sectors with 3D floors.";
            Picked?.Invoke(message);
            return message;
        }

        int count;
        if (_highlightedThreeDFloorCycleIndex.HasValue)
            count = SelectCycledThreeDFloorControlSector(sectors, _highlightedThreeDFloorCycleIndex.Value);
        else
            count = ThreeDFloors.SelectControlSectors(_map, sectors);

        SetEditMode(EditMode.Sectors);
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = count == 0
            ? "No 3D floor control sectors found."
            : "Selected " + count + " 3D floor control sector" + (count == 1 ? "." : "s.");
        Picked?.Invoke(status);
        return status;
    }

    public string CycleHighlightedThreeDFloor(bool up)
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Sector> sectors = SelectedSectorsOrHighlightedThreeDFloorTargets();
        if (sectors.Count == 0)
        {
            const string message = "Select or highlight a sector with 3D floors.";
            Picked?.Invoke(message);
            return message;
        }

        List<ThreeDFloor> floors = ThreeDFloors.GetThreeDFloors(_map, sectors);
        if (floors.Count == 0)
        {
            const string message = "No 3D floors found for highlighted sector.";
            Picked?.Invoke(message);
            return message;
        }

        int current = _highlightedThreeDFloorCycleIndex ?? 0;
        _highlightedThreeDFloorCycleIndex = Mod(current + (up ? 1 : -1), floors.Count);
        string status = ThreeDFloorCycleStatusText(_highlightedThreeDFloorCycleIndex.Value, floors.Count);
        Picked?.Invoke(status);
        return status;
    }

    public static string ThreeDFloorCycleStatusText(int index, int count)
        => "Highlighted 3D floor " + (index + 1) + " of " + count + ".";

    private int SelectCycledThreeDFloorControlSector(IReadOnlyList<Sector> sectors, int index)
    {
        if (_map == null) return 0;

        List<ThreeDFloor> floors = ThreeDFloors.GetThreeDFloors(_map, sectors);
        if (floors.Count == 0) return 0;

        _map.ClearAllSelected();
        floors[Mod(index, floors.Count)].Control.Selected = true;
        return 1;
    }

    private IReadOnlyList<Sector> SelectedSectorsOrHighlightedThreeDFloorTargets()
    {
        if (_map == null) return [];

        List<Sector> sectors = _map.GetSelectedSectors();
        if (sectors.Count == 0 && _map.GetSectorAt(_cursorWorld) is { } highlighted)
            sectors.Add(highlighted);

        return sectors;
    }

    private static int Mod(int value, int divisor)
        => ((value % divisor) + divisor) % divisor;

    public string RelocateThreeDFloorControlSectors()
    {
        if (_map == null) return "No map loaded.";

        EditBegun?.Invoke("Relocate 3D floor control sectors");
        int count = ThreeDFloors.RelocateManagedControlSectors(_map, ThreeDFloorControlSectorAreaSettings);
        if (count > 0)
        {
            MarkGeometryDirty();
            Changed?.Invoke();
        }

        string status = count == 0
            ? "No managed 3D floor control sectors found."
            : "Relocated " + count + " 3D floor control sector" + (count == 1 ? "." : "s.");
        Picked?.Invoke(status);
        return status;
    }

    public string SelectSectorsOutline()
    {
        if (_map == null) return "No map loaded.";

        IReadOnlyList<Sector> sectors = SelectedSectorsOrHighlighted();
        if (sectors.Count == 0)
        {
            const string message = "Select sectors to outline.";
            Picked?.Invoke(message);
            return message;
        }

        IReadOnlyList<Linedef> outline = StairBuilder.SelectSectorsOutline(_map, sectors);
        SetEditMode(EditMode.Linedefs);
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = StairBuilder.SelectSectorsOutlineStatusText(outline.Count);
        Picked?.Invoke(status);
        return status;
    }

    private IReadOnlyList<Vertex> SelectedVerticesOrHighlighted()
    {
        if (_map == null) return [];
        List<Vertex> vertices = _map.GetSelectedVertices();
        if (vertices.Count == 0 && _editMode == EditMode.Vertices && _map.NearestVertex(_cursorWorld, HighlightRangeWorld()) is { } highlighted)
            vertices.Add(highlighted);
        return vertices;
    }

    private IReadOnlyList<Linedef> SelectedLinedefsOrHighlighted()
    {
        if (_map == null) return [];
        List<Linedef> linedefs = _map.GetSelectedLinedefs();
        if (linedefs.Count == 0 && _editMode == EditMode.Linedefs && _map.NearestLinedef(_cursorWorld, HighlightRangeWorld()) is { } highlighted)
            linedefs.Add(highlighted);
        return linedefs;
    }

    private IReadOnlyList<Sector> SelectedSectorsOrHighlighted()
    {
        if (_map == null) return [];
        List<Sector> sectors = _map.GetSelectedSectors();
        if (sectors.Count == 0 && _editMode == EditMode.Sectors && _map.GetSectorAt(_cursorWorld) is { } highlighted)
            sectors.Add(highlighted);
        return sectors;
    }

    public string ApplyLightFogFlag()
    {
        if (_map == null) return "No map loaded.";
        if (_mapFormat != MapFormat.Udmf)
        {
            return "";
        }

        IReadOnlyList<Linedef> linedefs = SelectedLinedefsOrHighlighted();
        if (linedefs.Count == 0)
        {
            const string message = "This action requires selection of some description!";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke("Apply 'lightfog' flag");
        SidedefLightFogFlagResult result = SidedefFogTools.ApplyLightFogFlags(linedefs, CurrentMapInfo(), _gameConfig);
        if (result.Changed)
        {
            MarkGeometryDirty();
            Changed?.Invoke();
        }

        Picked?.Invoke(result.Message);
        return result.Message;
    }

    private MapInfoEntry? CurrentMapInfo()
        => _resources?.GetMapInfo().GetMap(_mapOptions?.CurrentName ?? "");

    // Traces the line loop enclosing the cursor and creates a sector from it (undoable).
    public string MakeSectorAtCursor()
    {
        if (_map == null || _map.Linedefs.Count == 0) return "No linedefs to trace.";
        var path = Tools.FindPotentialSectorAt(_map, _cursorWorld); // hole-aware
        if (path == null || path.Count < 3)
        {
            const string message = "no enclosing loop here";
            Picked?.Invoke(message);
            return message;
        }

        EditBegun?.Invoke("Make sector");
        var tracedLines = new HashSet<Linedef>(path.Select(side => side.Line));
        var nearbyLines = _map.Linedefs.Where(line => !tracedLines.Contains(line)).ToList();
        Sector? sector = Tools.MakeSector(_map, path, nearbyLines, useOverrides: false, options: CreateSectorCreationOptions());
        if (sector != null) Tools.FlipBackOnlyLinedefs(path.Select(side => side.Line));
        _map.BuildIndexes();
        MarkGeometryDirty();
        Changed?.Invoke();
        string status = $"made sector from {path.Count} lines";
        Picked?.Invoke(status);
        return status;
    }

    private Tools.SectorCreationOptions CreateSectorCreationOptions()
        => new()
        {
            DefaultFloorHeight = _defaultSectorFloorHeight,
            DefaultCeilingHeight = _defaultSectorCeilingHeight,
            DefaultBrightness = _defaultSectorBrightness,
            DefaultFloorTexture = FirstNonBlankOr("-", _mapOptions?.DefaultFloorTexture, _gameConfig?.DefaultFloorTexture),
            DefaultCeilingTexture = FirstNonBlankOr("-", _mapOptions?.DefaultCeilingTexture, _gameConfig?.DefaultCeilingTexture),
            DefaultHighTexture = FirstNonBlankOr("-", _mapOptions?.DefaultTopTexture),
            DefaultMiddleTexture = FirstNonBlankOr("-", _mapOptions?.DefaultWallTexture, _gameConfig?.DefaultWallTexture),
            DefaultLowTexture = FirstNonBlankOr("-", _mapOptions?.DefaultBottomTexture),
            OverrideFloorTexture = _mapOptions?.OverrideFloorTexture == true,
            OverrideCeilingTexture = _mapOptions?.OverrideCeilingTexture == true,
            OverrideHighTexture = _mapOptions?.OverrideTopTexture == true,
            OverrideMiddleTexture = _mapOptions?.OverrideMiddleTexture == true,
            OverrideLowTexture = _mapOptions?.OverrideBottomTexture == true,
            OverrideFloorHeight = _mapOptions?.OverrideFloorHeight == true,
            OverrideCeilingHeight = _mapOptions?.OverrideCeilingHeight == true,
            OverrideBrightness = _mapOptions?.OverrideBrightness == true,
            CustomFloorHeight = _mapOptions?.CustomFloorHeight ?? _defaultSectorFloorHeight,
            CustomCeilingHeight = _mapOptions?.CustomCeilingHeight ?? _defaultSectorCeilingHeight,
            CustomBrightness = _mapOptions?.CustomBrightness ?? _defaultSectorBrightness,
        };

    private static string FirstNonBlankOr(string fallback, params string?[] values)
    {
        foreach (string? value in values)
            if (!string.IsNullOrWhiteSpace(value)) return value;
        return fallback;
    }

    // ---- Draw-geometry tool ----

    /// <summary>True when the draw-geometry tool is active (host can reflect it in the status bar).</summary>
    public bool DrawMode => _drawMode;
    public bool DrawLinesOnly => _drawLinesOnly;
    public bool DrawCurve => _drawCurve;
    public bool SnapToGridEnabled => _snapToGrid;
    public bool RenderGridEnabled
    {
        get => _renderGrid;
        set
        {
            if (_renderGrid == value) return;
            _renderGrid = value;
            ActionStateChanged?.Invoke();
            RequestNextFrameRendering();
        }
    }
    public bool DynamicGridSizeEnabled
    {
        get => _dynamicGridSize;
        set
        {
            _dynamicGridSize = value;
            if (_dynamicGridSize) MatchGridSizeToDisplayScale();
        }
    }
    public event Action? DrawModeChanged;

    public void ToggleDrawMode(bool linesOnly = false, bool curve = false)
    {
        // Re-pressing the same draw key exits; switching kind restarts with the new kind.
        if (_drawMode && _drawLinesOnly == linesOnly && _drawCurve == curve) _drawMode = false;
        else
        {
            _drawMode = true;
            _drawLinesOnly = linesOnly;
            _drawCurve = curve;
            _shapeKind = ShapeKind.None;
        } // draw and shape are exclusive
        _drawPoints.Clear();
        _drawClosed = false;
        _drawDirty = true;
        DrawModeChanged?.Invoke();
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
    }

    /// <summary>True while any draw tool (polyline or shape) is active.</summary>
    public bool InDrawMode => _drawMode || _shapeKind != ShapeKind.None;

    /// <summary>Fully exits the polyline and shape draw tools and clears any in-progress preview.</summary>
    public void ExitDrawModes()
    {
        bool was = InDrawMode;
        _drawMode = false;
        _drawCurve = false;
        _shapeKind = ShapeKind.None;
        _drawPoints.Clear();
        _drawClosed = false;
        _drawDirty = true;
        _drawLineCount = 0; // drop the green preview immediately
        if (was) DrawModeChanged?.Invoke();
        if (was) ActionStateChanged?.Invoke();
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
            var v = _map.NearestVertex(world, HighlightRangeWorld());
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

    private bool RemoveDrawPoint(int index)
    {
        if (!_drawMode || index < 0 || index >= _drawPoints.Count) return false;
        _drawPoints.RemoveAt(index);
        _drawClosed = false;
        _drawDirty = true;
        RequestNextFrameRendering();
        return true;
    }

    private void FinishDraw()
    {
        if (_map == null) { CancelDraw(); return; }
        int min = _drawLinesOnly ? 2 : 3;
        if (_drawPoints.Count < min) { CancelDraw(); return; }

        var points = MaterializedDrawPoints(includeCursor: false);
        if (points.Count < min) { CancelDraw(); return; }

        if (_drawCurve && _drawCurveSettings.PlaceThingsAtVertices)
        {
            EditBegun?.Invoke("Place things at curve vertices");
            int count = DrawThingPlacement.PlaceAtPositions(_map, points, InsertThingType);
            if (count == 0) { CancelDraw(); return; }
            _map.BuildIndexes();
            MarkGeometryDirty();
            Changed?.Invoke();
            Picked?.Invoke($"placed {count} thing{(count == 1 ? "" : "s")} at draw vertices");
            CompletePolylineDraw();
            return;
        }

        // Materialize the drawn points as vertices, reusing any that snapped exactly onto existing ones.
        var verts = new System.Collections.Generic.List<Vertex>(points.Count);
        EditBegun?.Invoke(_drawCurve ? "Draw curve" : _drawLinesOnly ? "Draw lines" : "Draw sector");
        foreach (var p in points)
        {
            var existing = _map.NearestVertex(p, 0.01);
            verts.Add(existing ?? _map.AddVertex(p));
        }

        if (_drawLinesOnly)
        {
            for (int i = 0; i < verts.Count - 1; i++) _map.AddLinedef(verts[i], verts[i + 1]);
            if (_drawClosed && verts.Count >= 3 && !SamePoint(verts[0].Position, verts[^1].Position))
                _map.AddLinedef(verts[^1], verts[0]);
        }
        else
        {
            var nearbyLines = _map.Linedefs.Where(line => !LineTouchesOnlyDrawnVertices(line, verts)).ToList();
            Tools.MakeSectorFromLoop(_map, verts, nearbyLines, useOverrides: false, options: CreateSectorCreationOptions());
        }

        _map.MergeOverlappingVertices(0.01);
        _map.SplitLinedefsAtVertices(0.5); // weld drawn vertices that landed on existing walls (T-junctions)
        _map.BuildIndexes();

        MarkGeometryDirty();
        Changed?.Invoke();
        CompletePolylineDraw();
    }

    private void CompletePolylineDraw()
    {
        DrawModeTool tool = _drawCurve ? DrawModeTool.Curve : _drawLinesOnly ? DrawModeTool.Lines : DrawModeTool.Sector;
        bool continuous = tool switch
        {
            DrawModeTool.Curve => _drawCurveSettings.ContinuousDrawing,
            DrawModeTool.Lines => _drawLineSettings.ContinuousDrawing,
            _ => _drawLineSettings.ContinuousDrawing,
        };

        ApplyDrawLifecycle(DrawModeLifecycle.AfterAccept(tool, continuous));
    }

    private void ApplyDrawLifecycle(DrawModeLifecycleState state)
    {
        bool was = InDrawMode;
        _drawMode = state.DrawMode;
        _drawLinesOnly = state.LinesOnly;
        _drawCurve = state.Curve;
        _shapeKind = state.Shape switch
        {
            DrawModeTool.Rectangle => ShapeKind.Rectangle,
            DrawModeTool.Ellipse => ShapeKind.Ellipse,
            DrawModeTool.Grid => ShapeKind.Grid,
            _ => ShapeKind.None,
        };
        _drawPoints.Clear();
        _drawClosed = false;
        _drawDirty = true;
        _drawLineCount = 0;
        if (was != InDrawMode || was) DrawModeChanged?.Invoke();
        ActionStateChanged?.Invoke();
        RequestNextFrameRendering();
    }

    private System.Collections.Generic.List<Vec2D> MaterializedDrawPoints(bool includeCursor)
    {
        var control = new System.Collections.Generic.List<Vec2D>(_drawPoints);
        if (includeCursor && (control.Count == 0 || !SamePoint(control[^1], _drawCursor))) control.Add(_drawCursor);
        if (!_drawCurve) return control;

        if (_drawClosed && control.Count >= 3 && !SamePoint(control[0], control[^1])) control.Add(control[0]);
        return CurveTools.CurveThroughPoints(control, 0.5f, 0.75f, _drawCurveSettings.SegmentLength).Shape;
    }

    private static bool SamePoint(Vec2D a, Vec2D b)
        => Math.Abs(a.x - b.x) < 0.001 && Math.Abs(a.y - b.y) < 0.001;

    private static bool LineTouchesOnlyDrawnVertices(Linedef line, IReadOnlyCollection<Vertex> vertices)
        => vertices.Contains(line.Start) && vertices.Contains(line.End);

    // Builds the in-progress draw overlay: placed-point segments + a preview segment to the cursor.
    private void RebuildDrawPreview()
    {
        _drawLineCount = 0;
        if (_device is null || _drawVb is null || (!_drawMode && _threeDFloorEditMode != ThreeDFloorEditMode.DrawSlopes) || _drawPoints.Count == 0) return;

        const int col = unchecked((int)0xff40ff80);   // bright green polyline
        const int preview = unchecked((int)0xff80ff40);
        var verts = new System.Collections.Generic.List<FlatVertex>();
        var points = MaterializedDrawPoints(includeCursor: _drawCurve);
        for (int i = 0; i < points.Count - 1; i++)
        {
            verts.Add(FV(points[i], col));
            verts.Add(FV(points[i + 1], col));
        }
        if (!_drawCurve)
        {
            // Preview segment from the last placed point to the (snapped) cursor.
            verts.Add(FV(_drawPoints[^1], preview));
            verts.Add(FV(_drawCursor, preview));
        }

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
            var v = _map.NearestVertex(world, HighlightRangeWorld());
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
            var t = _map.NearestThingSquareRange(world, ThingHighlightRangeWorld(), _zoom, _fixedThingsScale);
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

    private bool HasTransformableSelection()
        => _map != null && (_map.SelectedGeometryVertices().Count > 0 || _map.SelectedThingsCount > 0);

    public void BeginClassicPaintSelection()
    {
        _classicPaintSelectPressed = true;
        _classicPaintSelectHighlight = null;
        ActionStateChanged?.Invoke();
    }

    private void EndClassicPaintSelection()
    {
        _classicPaintSelectPressed = false;
        _classicPaintSelectHighlight = null;
        ActionStateChanged?.Invoke();
    }

    private void BeginHeldPanView()
    {
        _heldPanView = true;
        _heldPanViewWaitingForPointer = true;
        ActionStateChanged?.Invoke();
    }

    private void EndHeldPanView()
    {
        _heldPanView = false;
        _heldPanViewWaitingForPointer = false;
        ActionStateChanged?.Invoke();
    }

    private void ApplyClassicPaintSelection(Vec2D world, KeyModifiers modifiers)
    {
        if (_map == null) return;
        if (ClassicPaintSelectionTarget(world) is not { } target) return;
        if (ReferenceEquals(target, _classicPaintSelectHighlight)) return;

        bool add = modifiers.HasFlag(KeyModifiers.Shift);
        bool remove = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        target.Selected = add || (!remove && !target.Selected);
        _classicPaintSelectHighlight = target;
        MarkGeometryDirty();
        Changed?.Invoke();
    }

    private ISelectable? ClassicPaintSelectionTarget(Vec2D world)
        => _map == null ? null : _editMode switch
        {
            EditMode.Vertices => _map.NearestVertex(world, HighlightRangeWorld()),
            EditMode.Linedefs => _map.NearestLinedef(world, HighlightRangeWorld()),
            EditMode.Sectors => _map.GetSectorAt(world),
            EditMode.Things => NearestVisibleThing(world, ThingHighlightRangeWorld()),
            _ => null,
        };

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetCurrentPoint(this).Position;
        UpdateAutoPanPointer(pos);

        // 3D: left-drag rotates the camera; right-drag changes the captured surface/thing height.
        if (_mode3D)
        {
            if (_look3D)
            {
                if (Math.Abs(pos.X - _lastPointer.X) + Math.Abs(pos.Y - _lastPointer.Y) > 2) _lookMoved = true;
                double sens = 0.005 * _mouseSpeed / Settings.DefaultMouseSpeed;
                double dx = pos.X - _lastPointer.X;
                double dy = pos.Y - _lastPointer.Y;
                if (_heldMapCommands.Contains("map3d.orbit") && TryOrbit3D(dx, dy))
                {
                    _lastPointer = pos;
                    RequestNextFrameRendering();
                    return;
                }

                _yaw -= dx * sens;
                _pitch = Math.Clamp(_pitch - dy * sens, -1.5, 1.5);
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

        if (AutomapMode)
        {
            UpdateAutomapHighlight(_cursorWorld, e.KeyModifiers);
            return;
        }

        if (WadAuthorMode) UpdateWadAuthorHighlight(_cursorWorld);

        if (_classicPaintSelectPressed)
        {
            ApplyClassicPaintSelection(_cursorWorld, e.KeyModifiers);
            return;
        }

        if (_drawMode || _threeDFloorEditMode == ThreeDFloorEditMode.DrawSlopes)
        {
            _drawCursor = SnapWorld(ToWorld(pos));
            _drawDirty = true; // GL buffer rebuilt on the render thread
            RequestNextFrameRendering();
        }

        if (_heldPanView)
        {
            if (_heldPanViewWaitingForPointer)
            {
                _heldPanViewWaitingForPointer = false;
                _lastPointer = pos;
                return;
            }

            PanViewByPointerDelta(pos);
            return;
        }

        // Right-drag pans the view (decided once the cursor moves past the click threshold).
        if (_rightPressed)
        {
            if (!_rightDragging && Math.Abs(pos.X - _dragStart.X) + Math.Abs(pos.Y - _dragStart.Y) < 4) return;
            _rightDragging = true;
            PanViewByPointerDelta(pos);
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
            PanViewByPointerDelta(pos);
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

    private void PanViewByPointerDelta(Point pos)
    {
        _camX -= (pos.X - _lastPointer.X) * _zoom;
        _camY += (pos.Y - _lastPointer.Y) * _zoom;
        _lastPointer = pos;
        _geometryDirty = true;
        RequestNextFrameRendering();
    }


    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var wheelKeys = EditorPointerInput.WheelKeys(e.Delta.X, e.Delta.Y);
        if (wheelKeys.Count == 0) return;
        string wheelKey = wheelKeys[0];

        // In 3D, the wheel raises/lowers the targeted floor/ceiling (Shift = fine 1-unit step).
        if (_mode3D)
        {
            int step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1 : 8;
            AdjustTarget3D(wheelKey is EditorPointerInput.ScrollUp or EditorPointerInput.ScrollRight ? step : -step);
            e.Handled = true;
            return;
        }

        bool accel = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        foreach (string shortcutKey in wheelKeys)
        {
            if (EditorCommandCatalog.ResolveShortcut(ShortcutBindings, EditorCommandScope.Map2D, shortcutKey, accel, shift, alt) is { } commandId
                && RunMapCommand(commandId, e.KeyModifiers))
            {
                e.Handled = true;
                return;
            }
        }

        ZoomBy(wheelKey is EditorPointerInput.ScrollUp or EditorPointerInput.ScrollRight ? 0.85 : 1.0 / 0.85);
        e.Handled = true;
    }

    private void ZoomBy(double factor)
    {
        _zoom = Math.Clamp(_zoom * factor, 0.02, 200);
        MatchGridSizeToDisplayScale();
        _geometryDirty = true;
        _soundLeakDirty = true;
        _wadAuthorDirty = true;
        RequestNextFrameRendering();
    }

    private void ScrollView(double screenDx, double screenDy)
    {
        _camX += screenDx * _zoom;
        _camY += screenDy * _zoom;
        _geometryDirty = true;
        RequestNextFrameRendering();
    }

    public static Avalonia.Vector AutoPanDelta(
        Point pointer,
        double width,
        double height,
        int speed,
        double worldUnitsPerPixel,
        double elapsedMilliseconds)
    {
        if (speed <= 0 || width <= 0 || height <= 0 || worldUnitsPerPixel <= 0 || elapsedMilliseconds <= 0)
            return default;

        double x = 0;
        if (pointer.X < AutoPanBorderSize)
            x = -AutoPanBorderSize + pointer.X;
        else if (pointer.X > width - AutoPanBorderSize)
            x = pointer.X - (width - AutoPanBorderSize);

        double y = 0;
        if (pointer.Y < AutoPanBorderSize)
            y = AutoPanBorderSize - pointer.Y;
        else if (pointer.Y > height - AutoPanBorderSize)
            y = -(pointer.Y - (height - AutoPanBorderSize));

        if (x == 0 && y == 0) return default;
        return new Avalonia.Vector(
            x * Math.Abs(x) * AutoPanScale * speed * worldUnitsPerPixel * elapsedMilliseconds,
            y * Math.Abs(y) * AutoPanScale * speed * worldUnitsPerPixel * elapsedMilliseconds);
    }

    private void UpdateAutoPanPointer(Point pointer)
    {
        if (_autoScrollSpeed == 0 || _mode3D || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            StopAutoPan();
            return;
        }

        _autoPanPointer = pointer;
        if (AutoPanDelta(pointer, Bounds.Width, Bounds.Height, _autoScrollSpeed, _zoom, elapsedMilliseconds: 1) == default)
        {
            StopAutoPan();
            return;
        }

        if (!_autoPanTimer.IsEnabled)
        {
            _autoPanClock.Restart();
            _autoPanTimer.Start();
        }
    }

    private void ApplyAutoPanTimerTick()
    {
        if (_autoPanPointer is not { } pointer)
        {
            StopAutoPan();
            return;
        }

        double elapsed = _autoPanClock.Elapsed.TotalMilliseconds;
        _autoPanClock.Restart();
        Avalonia.Vector delta = AutoPanDelta(pointer, Bounds.Width, Bounds.Height, _autoScrollSpeed, _zoom, elapsed);
        if (delta == default)
        {
            StopAutoPan();
            return;
        }

        _camX += delta.X;
        _camY += delta.Y;
        _geometryDirty = true;
        RequestNextFrameRendering();
    }

    private void StopAutoPan()
    {
        _autoPanPointer = null;
        _autoPanTimer.Stop();
        _autoPanClock.Reset();
    }

    // Clears the selection and selects the single nearest element (vertex -> thing -> linedef -> sector).
    private void SelectSingleAt(Vec2D world)
    {
        if (_map == null) return;
        _map.ClearAllSelected();
        switch (_editMode)
        {
            case EditMode.Vertices:
                if (_map.NearestVertex(world, HighlightRangeWorld()) is { } v) v.Selected = true;
                break;
            case EditMode.Things:
                if (NearestVisibleThing(world, ThingHighlightRangeWorld()) is { } t) t.Selected = true;
                break;
            case EditMode.Sectors:
                if (_map.GetSectorAt(world) is { } s) s.Selected = true;
                break;
            default:
                if (_map.NearestLinedef(world, HighlightRangeWorld()) is { } l) l.Selected = true;
                break;
        }
        MarkGeometryDirty();
        Changed?.Invoke();
    }

    private void SelectAtCursor(KeyModifiers modifiers)
    {
        bool additive = modifiers.HasFlag(KeyModifiers.Shift);
        if (!SelectPointElementAt(_cursorWorld, additive))
            Pick(_cursorWorld, additive);
    }

    private Thing? NearestVisibleThing(Vec2D pos, double maxRange)
    {
        if (_map == null) return null;

        return _map.NearestThingSquareRange(pos, maxRange, _zoom, _fixedThingsScale, t => !ThingHidden2D(t));
    }

    private double HighlightRangeWorld()
        => _highlightRange * _zoom;

    private double ThingHighlightRangeWorld()
        => _thingHighlightRange * _zoom;

    private double SplitLinedefsRangeWorld()
        => _splitLinedefsRange * _zoom;

    private void Pick(Vec2D world, bool additive)
    {
        if (_map == null) return;
        if (!additive) _map.ClearAllSelected();

        // Vertices/things are handled on press; here Linedefs/Sectors modes pick their element (else just clear).
        string desc = "nothing";
        if (_editMode == EditMode.Linedefs)
        {
            var l = _map.NearestLinedef(world, HighlightRangeWorld());
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
