// ABOUTME: Models UDB draw-mode accept behavior for continuous and one-shot drawing tools.
// ABOUTME: Keeps editor lifecycle decisions testable without depending on Avalonia controls.

namespace DBuilder.Map;

public enum DrawModeTool
{
    Sector,
    Lines,
    Curve,
    Rectangle,
    Ellipse,
    Grid
}

public readonly record struct DrawModeLifecycleState(
    bool DrawMode,
    bool LinesOnly,
    bool Curve,
    DrawModeTool? Shape);

public static class DrawModeLifecycle
{
    public static DrawModeLifecycleState AfterAccept(
        DrawModeTool tool,
        bool continuousDrawing)
    {
        if (!continuousDrawing) return new DrawModeLifecycleState(false, false, false, null);

        return tool switch
        {
            DrawModeTool.Sector => new DrawModeLifecycleState(true, false, false, null),
            DrawModeTool.Lines => new DrawModeLifecycleState(true, true, false, null),
            DrawModeTool.Curve => new DrawModeLifecycleState(true, true, true, null),
            _ => new DrawModeLifecycleState(false, false, false, tool),
        };
    }
}
