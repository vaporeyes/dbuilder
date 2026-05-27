// ABOUTME: Shared parser for ZDoom color strings used by resource metadata.
// ABOUTME: Matches UDB/ZDoom #RGB, #RRGGBB, bare hex, and X11 color-name behavior.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DBuilder.IO;

internal static class ZDoomColorParser
{
    public static bool TryParse(string value, IReadOnlyDictionary<string, X11Color>? knownColors, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        string name = value.Replace(" ", "", StringComparison.Ordinal);
        bool htmlColor = name.StartsWith('#');
        if (htmlColor)
        {
            name = name.Substring(1);
            if (name.Length == 3)
                name = string.Concat(name[0], name[0], name[1], name[1], name[2], name[2]);
            else if (name.Length != 6)
                return true;
        }

        if (int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            red = (byte)((rgb >> 16) & 0xff);
            green = (byte)((rgb >> 8) & 0xff);
            blue = (byte)(rgb & 0xff);
            return true;
        }

        if (!htmlColor && TryLookupKnownColor(name, knownColors, out var known))
        {
            red = (byte)known.R;
            green = (byte)known.G;
            blue = (byte)known.B;
            return true;
        }

        return false;
    }

    private static bool TryLookupKnownColor(string normalizedName, IReadOnlyDictionary<string, X11Color>? knownColors, out X11Color color)
    {
        color = default!;
        if (knownColors == null) return false;
        if (knownColors.TryGetValue(normalizedName, out var exact))
        {
            color = exact;
            return true;
        }
        foreach (var entry in knownColors)
        {
            if (entry.Key.Replace(" ", "", StringComparison.Ordinal).Equals(normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                color = entry.Value;
                return true;
            }
        }
        return false;
    }
}
