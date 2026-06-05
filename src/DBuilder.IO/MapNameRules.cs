// ABOUTME: Small validation helper for WAD map marker names used by editor map options.
// ABOUTME: Keeps user-entered map names within the classic 8-character lump-name boundary.

using System.Text;

namespace DBuilder.IO;

public static class MapNameRules
{
    public const int MaxClassicNameLength = 8;

    public static string NormalizeMarker(string? value, string fallback = "MAP01")
    {
        string source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        string marker = MarkerChars(source);
        return marker.Length == 0 ? NormalizeMarker(fallback, "MAP01") : marker;
    }

    public static bool IsValidMarker(string? value, GameConfiguration? config = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string marker = MarkerChars(value.Trim());
        return marker.Length > 0 && config?.ValidateMapName(marker) != false;
    }

    private static string MarkerChars(string source)
    {
        var builder = new StringBuilder(MaxClassicNameLength);
        foreach (char ch in source.ToUpperInvariant())
        {
            if (builder.Length == MaxClassicNameLength) break;
            if (IsMarkerChar(ch)) builder.Append(ch);
        }

        return builder.ToString();
    }

    private static bool IsMarkerChar(char ch)
        => ch is >= 'A' and <= 'Z'
           || ch is >= '0' and <= '9'
           || ch == '_';
}
