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

    public static bool IsButtonKey(string key)
        => string.Equals(key, LeftButton, StringComparison.Ordinal)
            || string.Equals(key, MiddleButton, StringComparison.Ordinal)
            || string.Equals(key, RightButton, StringComparison.Ordinal)
            || string.Equals(key, ExtendedButton1, StringComparison.Ordinal)
            || string.Equals(key, ExtendedButton2, StringComparison.Ordinal);

    public static bool IsScrollKey(string key)
        => string.Equals(key, ScrollUp, StringComparison.Ordinal)
            || string.Equals(key, ScrollDown, StringComparison.Ordinal)
            || string.Equals(key, ScrollLeft, StringComparison.Ordinal)
            || string.Equals(key, ScrollRight, StringComparison.Ordinal);

    public static string? WheelKey(double x, double y)
    {
        var keys = WheelKeys(x, y);
        return keys.Count == 0 ? null : keys[0];
    }

    public static IReadOnlyList<string> WheelKeys(double x, double y)
    {
        if (x == 0 && y == 0) return [];

        string? horizontal = x == 0 ? null : x > 0 ? ScrollRight : ScrollLeft;
        string? vertical = y == 0 ? null : y > 0 ? ScrollUp : ScrollDown;
        if (horizontal is null) return [vertical!];
        if (vertical is null) return [horizontal];
        return Math.Abs(y) >= Math.Abs(x) ? [vertical, horizontal] : [horizontal, vertical];
    }
}
