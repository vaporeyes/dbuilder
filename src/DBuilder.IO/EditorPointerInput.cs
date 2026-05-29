// ABOUTME: Normalizes pointer gestures into stable editor command keys for configurable mouse shortcuts.
// ABOUTME: Mirrors UDB-style scroll direction names while staying independent of any UI framework event type.

namespace DBuilder.IO;

public static class EditorPointerInput
{
    public const string ScrollUp = "ScrollUp";
    public const string ScrollDown = "ScrollDown";
    public const string ScrollLeft = "ScrollLeft";
    public const string ScrollRight = "ScrollRight";

    public static string? WheelKey(double x, double y)
    {
        if (x == 0 && y == 0) return null;
        if (Math.Abs(y) >= Math.Abs(x)) return y > 0 ? ScrollUp : ScrollDown;
        return x > 0 ? ScrollRight : ScrollLeft;
    }
}
