// ABOUTME: Parser for X11 rgb.txt color definitions used by ZDoom data.
// ABOUTME: Captures color names and RGB values from whitespace-separated rows.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

public sealed class X11Rgb
{
    public Dictionary<string, X11Color> Colors { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record X11Color(string Name, int R, int G, int B);

public static class X11RgbParser
{
    public static X11Rgb Parse(string text)
    {
        var rgb = new X11Rgb();
        foreach (var rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("!", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal)) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) continue;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int g)) continue;
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b)) continue;
            string name = string.Join(' ', parts, 3, parts.Length - 3);
            rgb.Colors[name] = new X11Color(name, r, g, b);
        }
        return rgb;
    }
}
