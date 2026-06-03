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
        var builder = new StringBuilder(MaxClassicNameLength);
        foreach (char ch in source.ToUpperInvariant())
        {
            if (builder.Length == MaxClassicNameLength) break;
            if (IsMarkerChar(ch)) builder.Append(ch);
        }

        return builder.Length == 0 ? NormalizeMarker(fallback, "MAP01") : builder.ToString();
    }

    public static bool IsValidMarker(string? value, GameConfiguration? config = null)
    {
        string marker = NormalizeMarker(value);
        return config?.ValidateMapName(marker) != false;
    }

    private static bool IsMarkerChar(char ch)
        => ch is >= 'A' and <= 'Z'
           || ch is >= '0' and <= '9'
           || ch == '_';
}
