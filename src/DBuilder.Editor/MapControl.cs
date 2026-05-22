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
    private GlVertexBuffer? _fillsVb;
    private int _fillTris;
    private GlVertexBuffer? _linesVb;
    private int _lineCount;
    private GlVertexBuffer? _thingsVb;
    private int _thingTris;
    private GlVertexBuffer? _selVertsVb;
    private int _selVertTris;
    private bool _geometryDirty = true;

    private MapSet? _map;
    public MapSet? Map
    {
        get => _map;
        set { _map = value; _geometryDirty = true; FitToMap(); RequestNextFrameRendering(); }
    }

    // Camera: world-space center + zoom in world-units-per-DIP.
    private double _camX, _camY, _zoom = 1.0;
    private enum DragKind { None, Pan, Move }
    private bool _pressed;
    private DragKind _drag = DragKind.None;
    private bool _moveCandidate;
    private Point _dragStart;
    private Point _lastPointer;

    /// <summary>Raised with the world coordinates under the cursor (for the status bar).</summary>
    public event Action<Vec2D>? CursorWorldMoved;

    /// <summary>Raised when a left-click pick selects (or clears) something; carries a short description.</summary>
    public event Action<string>? Picked;

    /// <summary>Raised just before an interactive edit mutates the map, so the host can snapshot for undo.</summary>
    public event Action<string>? EditBegun;

    /// <summary>Raised after the map changes (move/pick) so the host can refresh its panels.</summary>
    public event Action? Changed;

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
        _fillsVb = new GlVertexBuffer(_gl);
        _linesVb = new GlVertexBuffer(_gl);
        _thingsVb = new GlVertexBuffer(_gl);
        _selVertsVb = new GlVertexBuffer(_gl);
        _device.SetCullMode(Cull.None);
        _device.SetZEnable(false);
        _device.SetAlphaBlendEnable(false);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _fillsVb?.Dispose();
        _linesVb?.Dispose();
        _thingsVb?.Dispose();
        _selVertsVb?.Dispose();
        _shader?.Dispose();
        _device?.Dispose();
        _fillsVb = null; _linesVb = null; _thingsVb = null; _selVertsVb = null; _shader = null; _device = null; _gl = null;
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
            if (_geometryDirty) { RebuildGeometry(); _geometryDirty = false; }

            double halfW = Bounds.Width * 0.5 * _zoom;
            double halfH = Bounds.Height * 0.5 * _zoom;
            var proj = Matrix4x4.CreateOrthographicOffCenter(
                (float)(_camX - halfW), (float)(_camX + halfW),
                (float)(_camY - halfH), (float)(_camY + halfH),
                -1, 1);
            _device.SetUniform("projection", proj);

            // Draw order: sector fills -> things -> lines -> selection markers.
            if (_fillTris > 0 && _fillsVb != null)
            {
                _device.SetVertexBuffer(_fillsVb);
                _device.Draw(DBPrimitiveType.TriangleList, 0, _fillTris);
            }
            if (_thingTris > 0 && _thingsVb != null)
            {
                _device.SetVertexBuffer(_thingsVb);
                _device.Draw(DBPrimitiveType.TriangleList, 0, _thingTris);
            }
            if (_lineCount > 0 && _linesVb != null)
            {
                _device.SetVertexBuffer(_linesVb);
                _device.Draw(DBPrimitiveType.LineList, 0, _lineCount);
            }
            if (_selVertTris > 0 && _selVertsVb != null)
            {
                _device.SetVertexBuffer(_selVertsVb);
                _device.Draw(DBPrimitiveType.TriangleList, 0, _selVertTris);
            }
        }
    }

    private void RebuildGeometry()
    {
        if (_map == null || _device is null) return;

        // Sector fills (brightness-shaded; selected sectors tinted cyan). Triangulated per sector.
        if (_fillsVb != null)
        {
            var fv = new System.Collections.Generic.List<FlatVertex>();
            foreach (var sector in _map.Sectors)
            {
                if (sector.Sidedefs.Count == 0) continue;
                Triangulation tri;
                try { tri = Triangulation.Create(sector); }
                catch { continue; }
                if (tri.Vertices.Count == 0) continue;

                int c = SectorFillColor(sector);
                for (int i = 0; i < tri.Vertices.Count; i++)
                    fv.Add(FV(tri.Vertices[i], c));
            }
            var arr = fv.ToArray();
            if (arr.Length > 0) _device.SetBufferData(_fillsVb, arr);
            _fillTris = arr.Length / 3;
        }

        // Lines.
        if (_linesVb != null)
        {
            var lv = new FlatVertex[_map.Linedefs.Count * 2];
            for (int i = 0; i < _map.Linedefs.Count; i++)
            {
                var l = _map.Linedefs[i];
                int c = LineColor(l);
                lv[i * 2 + 0] = FV(l.Start.Position, c);
                lv[i * 2 + 1] = FV(l.End.Position, c);
            }
            _device.SetBufferData(_linesVb, lv);
            _lineCount = lv.Length / 2;
        }

        // Thing markers (filled diamonds, colored by type; selected = yellow).
        if (_thingsVb != null)
        {
            var tv = new System.Collections.Generic.List<FlatVertex>(_map.Things.Count * 12);
            const double s = 10;
            foreach (var t in _map.Things)
            {
                int c = t.Selected ? unchecked((int)0xffffee00) : ThingColor(t.Type);
                var p = t.Position;
                var n = new Vec2D(p.x, p.y + s);
                var e = new Vec2D(p.x + s, p.y);
                var so = new Vec2D(p.x, p.y - s);
                var w = new Vec2D(p.x - s, p.y);
                tv.Add(FV(p, c)); tv.Add(FV(n, c)); tv.Add(FV(e, c));
                tv.Add(FV(p, c)); tv.Add(FV(e, c)); tv.Add(FV(so, c));
                tv.Add(FV(p, c)); tv.Add(FV(so, c)); tv.Add(FV(w, c));
                tv.Add(FV(p, c)); tv.Add(FV(w, c)); tv.Add(FV(n, c));
            }
            var arr = tv.ToArray();
            if (arr.Length > 0) _device.SetBufferData(_thingsVb, arr);
            _thingTris = arr.Length / 3;
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

    private static int SectorFillColor(Sector s)
    {
        // Dim brightness-shaded gray so the line/thing overlays stay legible on top.
        double br = Math.Clamp(s.Brightness / 255.0, 0.12, 1.0) * 0.45;
        byte g = (byte)Math.Clamp(br * 255, 0, 255);
        if (s.Selected) // blend toward cyan to flag the selection as a fill
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

    private bool _selectionDoneOnPress;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            _drag = DragKind.None;
            _dragStart = pt.Position;
            _lastPointer = pt.Position;
            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            // Select a vertex/thing immediately on press so a single press-drag moves it.
            _selectionDoneOnPress = SelectPointElementAt(ToWorld(pt.Position), shift);
            _moveCandidate = _selectionDoneOnPress;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed) return;
        _pressed = false;
        var pos = e.GetCurrentPoint(this).Position;
        if (_drag == DragKind.None)
        {
            // A click. Point elements were already handled on press; otherwise pick a line/sector (or clear).
            if (!_selectionDoneOnPress)
                Pick(ToWorld(pos), additive: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
        else if (_drag == DragKind.Move)
        {
            Changed?.Invoke();
        }
        _drag = DragKind.None;
    }

    // Selects the nearest vertex/thing under the cursor on press. Returns true if one ended up selected
    // (so a subsequent drag should move it). Non-additive clicks replace the selection only when the hit
    // element wasn't already selected, preserving an existing multi-selection for dragging.
    private bool SelectPointElementAt(Vec2D world, bool additive)
    {
        if (_map == null) return false;

        var v = _map.NearestVertex(world, 10 * _zoom);
        if (v != null)
        {
            if (additive) v.Selected = !v.Selected;
            else if (!v.Selected) { _map.ClearAllSelected(); v.Selected = true; }
            MarkGeometryDirty();
            Picked?.Invoke($"vertex ({v.Position.x:0.#}, {v.Position.y:0.#})");
            return v.Selected;
        }

        var t = _map.NearestThing(world, 12 * _zoom);
        if (t != null)
        {
            if (additive) t.Selected = !t.Selected;
            else if (!t.Selected) { _map.ClearAllSelected(); t.Selected = true; }
            MarkGeometryDirty();
            Picked?.Invoke($"thing type {t.Type} ({t.Position.x:0.#}, {t.Position.y:0.#})");
            return t.Selected;
        }

        return false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetCurrentPoint(this).Position;
        CursorWorldMoved?.Invoke(ToWorld(pos));
        if (!_pressed) return;

        if (_drag == DragKind.None)
        {
            double moved = Math.Abs(pos.X - _dragStart.X) + Math.Abs(pos.Y - _dragStart.Y);
            if (moved < 4) return;
            _drag = _moveCandidate ? DragKind.Move : DragKind.Pan;
            if (_drag == DragKind.Move) EditBegun?.Invoke("Move selection");
        }

        if (_drag == DragKind.Pan)
        {
            _camX -= (pos.X - _lastPointer.X) * _zoom;
            _camY += (pos.Y - _lastPointer.Y) * _zoom;
            RequestNextFrameRendering();
        }
        else if (_drag == DragKind.Move && _map != null)
        {
            var delta = ToWorld(pos) - ToWorld(_lastPointer);
            _map.MoveSelectedVerticesBy(delta);
            _map.MoveSelectedThingsBy(delta);
            MarkGeometryDirty();
            Changed?.Invoke();
        }
        _lastPointer = pos;
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

        // Point elements (vertices/things) are picked on press; this handles line then sector.
        string desc;
        var l = _map.NearestLinedef(world, 8 * _zoom);
        if (l != null) { l.Selected = !l.Selected; desc = $"linedef {_map.Linedefs.IndexOf(l)}"; }
        else
        {
            var s = _map.GetSectorAt(world);
            if (s != null) { s.Selected = !s.Selected; desc = $"sector {s.Index}"; }
            else desc = "nothing";
        }
        MarkGeometryDirty();
        Picked?.Invoke(desc);
    }
}
