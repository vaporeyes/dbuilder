// ABOUTME: Normalizes pointer gestures into stable editor command keys for configurable mouse shortcuts.
// ABOUTME: Mirrors UDB-style scroll direction names while staying independent of any UI framework event type.

namespace DBuilder.IO;

public enum EditorPointerButton
{
    None,
    Left,
    Middle,
    Right,
    XButton1,
    XButton2,
}

public static class EditorPointerInput
{
    public const string LeftButton = "LButton";
    public const string MiddleButton = "MButton";
    public const string RightButton = "RButton";
    public const string ExtendedButton1 = "XButton1";
    public const string ExtendedButton2 = "XButton2";
    public const string ScrollUp = "ScrollUp";
    public const string ScrollDown = "ScrollDown";
    public const string ScrollLeft = "ScrollLeft";
    public const string ScrollRight = "ScrollRight";

    public static string? ButtonKey(EditorPointerButton button) => button switch
    {
        EditorPointerButton.Left => LeftButton,
        EditorPointerButton.Middle => MiddleButton,
        EditorPointerButton.Right => RightButton,
        EditorPointerButton.XButton1 => ExtendedButton1,
        EditorPointerButton.XButton2 => ExtendedButton2,
        _ => null,
    };

    public static string? WheelKey(double x, double y)
    {
        if (x == 0 && y == 0) return null;
        if (Math.Abs(y) >= Math.Abs(x)) return y > 0 ? ScrollUp : ScrollDown;
        return x > 0 ? ScrollRight : ScrollLeft;
    }
}
