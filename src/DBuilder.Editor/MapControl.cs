// ABOUTME: Avalonia OpenGlControlBase that renders a MapSet's 2D line overlay via the DBuilder.Rendering stack.
// ABOUTME: Bridges Avalonia's GL context to Silk.NET so RenderDevice/Shader/VertexBuffer work unchanged; supports pan/zoom and click-pick.

using System;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using DBuilder.Geometry;
using DBuilder.Map;
using DBuilder.Rendering;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using GlVertexBuffer = DBuilder.Rendering.VertexBuffer;
using DBShader = DBuilder.Rendering.Shader;
using DBPrimitiveType = DBuilder.Rendering.PrimitiveType;
using Vec2D = DBuilder.Geometry.Vector2D;

namespace DBuilder.Editor;

public class MapControl : OpenGlControlBase
{
    private const string VertexSrc = @"#version 330 core
layout(location=0) in vec4 a_pos;
layout(location=1) in vec4 a_color;
layout(location=2) in vec2 a_uv;
uniform mat4 projection;
out vec4 v_color;
void main() { gl_Position = projection * vec4(a_pos.xyz, 1.0); v_color = a_color; }";

    private const string FragmentSrc = @"#version 330 core
in vec4 v_color;
out vec4 frag;
void main() { frag = v_color; }";

    private GL? _gl;
    private RenderDevice? _device;
    private DBShader? _shader;
    private GlVertexBuffer? _linesVb;
    private int _lineCount;
    private bool _geometryDirty = true;

    private MapSet? _map;
    public MapSet? Map
    {
        get => _map;
        set { _map = value; _geometryDirty = true; FitToMap(); RequestNextFrameRendering(); }
    }

    // Camera: world-space center + zoom in world-units-per-DIP.
    private double _camX, _camY, _zoom = 1.0;
    private bool _panning;
    private Point _lastPointer;

    /// <summary>Raised with the world coordinates under the cursor (for the status bar).</summary>
    public event Action<Vec2D>? CursorWorldMoved;

    /// <summary>Raised when a left-click pick selects (or clears) something; carries a short description.</summary>
    public event Action<string>? Picked;

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

    public void MarkGeometryDirty() { _geometryDirty = true; RequestNextFrameRendering(); }

    // ---- GL lifecycle ----

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = new GL(new LamdaNativeContext(name => gl.GetProcAddress(name)));
        _device = new RenderDevice(_gl);
        _shader = new DBShader(_gl, VertexSrc, FragmentSrc);
        _linesVb = new GlVertexBuffer(_gl);
        _device.SetCullMode(Cull.None);
        _device.SetZEnable(false);
        _device.SetAlphaBlendEnable(false);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _linesVb?.Dispose();
        _shader?.Dispose();
        _device?.Dispose();
        _linesVb = null; _shader = null; _device = null; _gl = null;
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

        if (_map != null)
        {
            if (_geometryDirty) { RebuildLines(); _geometryDirty = false; }

            double halfW = Bounds.Width * 0.5 * _zoom;
            double halfH = Bounds.Height * 0.5 * _zoom;
            var proj = Matrix4x4.CreateOrthographicOffCenter(
                (float)(_camX - halfW), (float)(_camX + halfW),
                (float)(_camY - halfH), (float)(_camY + halfH),
                -1, 1);
            _device.SetUniform("projection", proj);

            if (_lineCount > 0 && _linesVb != null)
            {
                _device.SetVertexBuffer(_linesVb);
                _device.Draw(DBPrimitiveType.LineList, 0, _lineCount);
            }
        }
    }

    private void RebuildLines()
    {
        if (_map == null || _device is null || _linesVb is null) return;
        var verts = new FlatVertex[_map.Linedefs.Count * 2];
        for (int i = 0; i < _map.Linedefs.Count; i++)
        {
            var l = _map.Linedefs[i];
            int c = LineColor(l);
            verts[i * 2 + 0] = FV(l.Start.Position, c);
            verts[i * 2 + 1] = FV(l.End.Position, c);
        }
        _device.SetBufferData(_linesVb, verts);
        _lineCount = verts.Length / 2;
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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsLeftButtonPressed)
        {
            _panning = true;
            _lastPointer = pt.Position;
            _panStart = pt.Position;
        }
    }

    private Point _panStart;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panning)
        {
            _panning = false;
            var pos = e.GetCurrentPoint(this).Position;
            double moved = Math.Abs(pos.X - _panStart.X) + Math.Abs(pos.Y - _panStart.Y);
            if (moved < 4) Pick(ToWorld(pos), additive: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetCurrentPoint(this).Position;
        CursorWorldMoved?.Invoke(ToWorld(pos));
        if (_panning)
        {
            double dx = pos.X - _lastPointer.X;
            double dy = pos.Y - _lastPointer.Y;
            _lastPointer = pos;
            _camX -= dx * _zoom;
            _camY += dy * _zoom;
            RequestNextFrameRendering();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        double factor = e.Delta.Y > 0 ? 0.85 : 1.0 / 0.85;
        _zoom *= factor;
        _zoom = Math.Clamp(_zoom, 0.02, 200);
        RequestNextFrameRendering();
    }

    private void Pick(Vec2D world, bool additive)
    {
        if (_map == null) return;
        if (!additive) _map.ClearAllSelected();

        double vr = 10 * _zoom, lr = 8 * _zoom;
        string desc;
        var v = _map.NearestVertex(world, vr);
        if (v != null) { v.Selected = !v.Selected; desc = $"vertex ({v.Position.x:0.#}, {v.Position.y:0.#})"; }
        else
        {
            var l = _map.NearestLinedef(world, lr);
            if (l != null) { l.Selected = !l.Selected; desc = $"linedef {_map.Linedefs.IndexOf(l)}"; }
            else
            {
                var s = _map.GetSectorAt(world);
                if (s != null) { s.Selected = !s.Selected; desc = $"sector {s.Index}"; }
                else desc = "nothing";
            }
        }
        MarkGeometryDirty();
        Picked?.Invoke(desc);
    }
}
