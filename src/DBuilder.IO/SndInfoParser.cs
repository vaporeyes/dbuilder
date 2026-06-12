// ABOUTME: Parser for ZDoom SNDINFO logical sound declarations.
// ABOUTME: Captures sound mappings, aliases, random groups, and ambient sound editor radii.

using System;
using System.Collections.Generic;
using System.Text;

namespace DBuilder.IO;

public sealed class SndInfo
{
    public Dictionary<string, string> Sounds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> RandomGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, AmbientSoundInfo> AmbientSounds { get; } = new();
}

public enum AmbientSoundType
{
    None,
    Point,
    Surround,
    World,
}

public enum AmbientSoundMode
{
    None,
    Continuous,
    Random,
    Periodic,
}

public sealed record AmbientSoundInfo(
    int Index,
    string SoundName,
    AmbientSoundType Type,
    AmbientSoundMode Mode,
    double Volume,
    double Attenuation,
    double SecondsMin,
    double SecondsMax,
    double Seconds,
    double MinimumRadius,
    double MaximumRadius);

public static class SndInfoParser
{
    private enum AssignmentFormat { None, Old, New }
    private enum SoundRolloffType { None, Custom, Linear, Log }

    private sealed class SoundProperties
    {
        public double Attenuation { get; set; } = 1.0;
        public int MinimumDistance { get; set; } = 200;
        public int MaximumDistance { get; set; } = 1200;
        public SoundRolloffType Rolloff { get; set; }
        public double RolloffFactor { get; set; } = 1.0;
    }

    private sealed record AmbientSoundDraft(
        int Index,
        string SoundName,
        AmbientSoundType Type,
        AmbientSoundMode Mode,
        double Volume,
        double Attenuation,
        double SecondsMin,
        double SecondsMax,
        double Seconds);

    public static SndInfo Parse(string text) => Parse(text, baseGame: null);

    public static SndInfo Parse(string text, TerrainBaseGame? baseGame)
    {
        var result = new SndInfo();
        var soundProperties = new Dictionary<string, SoundProperties>(StringComparer.OrdinalIgnoreCase);
        var ambientDrafts = new Dictionary<int, AmbientSoundDraft>();
        TerrainBaseGame? conditionalGame = null;
        AssignmentFormat format = AssignmentFormat.None;
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string rawLine = lines[lineIndex];
            var t = Tokenize(StripLineComment(rawLine));
            if (t.Count == 0) continue;

            string first = t[0];
            if (TryReadConditional(first, out var game))
            {
                conditionalGame = game;
                continue;
            }
            if (first.Equals("$endif", StringComparison.OrdinalIgnoreCase))
            {
                conditionalGame = null;
                continue;
            }
            if (baseGame.HasValue && conditionalGame.HasValue && conditionalGame.Value != baseGame.Value) continue;

            if (first.Equals("$alias", StringComparison.OrdinalIgnoreCase))
            {
                if (t.Count >= 3) result.Aliases[t[1]] = t[2];
                continue;
            }
            if (first.Equals("$ambient", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseAmbient(t, baseGame, out AmbientSoundDraft? ambient)
                    && ambient is not null
                    && !ambient.SoundName.StartsWith("*", StringComparison.Ordinal))
                {
                    ambientDrafts[ambient.Index] = ambient;
                }
                continue;
            }
            if (first.Equals("$attenuation", StringComparison.OrdinalIgnoreCase))
            {
                if (!ParseAttenuation(t, soundProperties)) return result;
                continue;
            }
            if (first.Equals("$rolloff", StringComparison.OrdinalIgnoreCase))
            {
                if (!ParseRolloff(t, soundProperties)) return result;
                continue;
            }
            if (first.Equals("$random", StringComparison.OrdinalIgnoreCase))
            {
                CollectRandomTokens(lines, ref lineIndex, t);
                if (!ParseRandom(result, t)) return result;
                continue;
            }

            if (first.StartsWith("$", StringComparison.Ordinal)) continue;
            if (t.Count >= 3 && t[1] == "=")
            {
                if (!TrySetAssignmentFormat(ref format, AssignmentFormat.New)) return result;
                result.Sounds[first] = t[2];
                EnsureSoundProperties(soundProperties, first);
            }
            else if (t.Count >= 2)
            {
                if (!TrySetAssignmentFormat(ref format, AssignmentFormat.Old)) return result;
                result.Sounds[first] = t[1];
                EnsureSoundProperties(soundProperties, first);
            }
        }
        ApplyAmbientSounds(result, ambientDrafts, soundProperties);
        return result;
    }

    private static bool TrySetAssignmentFormat(ref AssignmentFormat current, AssignmentFormat next)
    {
        if (current == AssignmentFormat.None)
        {
            current = next;
            return true;
        }

        return current == next;
    }

    private static void CollectRandomTokens(string[] lines, ref int lineIndex, List<string> tokens)
    {
        while (!tokens.Contains("}") && lineIndex + 1 < lines.Length)
        {
            lineIndex++;
            tokens.AddRange(Tokenize(StripLineComment(lines[lineIndex])));
        }
    }

    private static bool TryReadConditional(string token, out TerrainBaseGame game)
    {
        if (token.Equals("$ifdoom", StringComparison.OrdinalIgnoreCase)) { game = TerrainBaseGame.Doom; return true; }
        if (token.Equals("$ifheretic", StringComparison.OrdinalIgnoreCase)) { game = TerrainBaseGame.Heretic; return true; }
        if (token.Equals("$ifhexen", StringComparison.OrdinalIgnoreCase)) { game = TerrainBaseGame.Hexen; return true; }
        if (token.Equals("$ifstrife", StringComparison.OrdinalIgnoreCase)) { game = TerrainBaseGame.Strife; return true; }
        game = default;
        return false;
    }

    private static bool ParseRandom(SndInfo result, List<string> tokens)
    {
        if (tokens.Count < 4) return false;
        string name = tokens[1];
        var sounds = new List<string>();
        for (int i = 2; i < tokens.Count; i++)
        {
            if (tokens[i] is "{" or "}") continue;
            sounds.Add(tokens[i]);
        }
        if (name.Length == 0 || sounds.Count == 0 || sounds.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        result.RandomGroups[name] = sounds;
        return true;
    }

    private static void EnsureSoundProperties(Dictionary<string, SoundProperties> properties, string name)
    {
        if (!properties.ContainsKey(name)) properties[name] = new SoundProperties();
    }

    private static bool ParseAttenuation(List<string> tokens, Dictionary<string, SoundProperties> properties)
    {
        if (tokens.Count < 3) return false;
        if (!TryParseDouble(tokens[2], out double attenuation) || attenuation < 0.0) return false;
        SoundProperties props = GetSoundProperties(properties, tokens[1]);
        props.Attenuation = attenuation;
        return true;
    }

    private static bool ParseRolloff(List<string> tokens, Dictionary<string, SoundProperties> properties)
    {
        if (tokens.Count < 3) return false;
        SoundProperties props = GetSoundProperties(properties, tokens[1]);
        string token = tokens[2].ToLowerInvariant();
        if (token is "custom" or "linear" or "log")
        {
            if (token == "linear")
            {
                if (tokens.Count < 5
                    || !int.TryParse(tokens[3], out int minimum)
                    || !int.TryParse(tokens[4], out int maximum)
                    || minimum < 0
                    || maximum < 0) return false;
                props.MinimumDistance = minimum;
                props.MaximumDistance = maximum;
            }
            else if (token == "log")
            {
                if (tokens.Count < 5
                    || !int.TryParse(tokens[3], out int minimum)
                    || !TryParseDouble(tokens[4], out double factor)
                    || minimum < 0
                    || factor < 0.0) return false;
                props.MinimumDistance = minimum;
                props.RolloffFactor = factor;
            }

            props.Rolloff = token switch
            {
                "custom" => SoundRolloffType.Custom,
                "linear" => SoundRolloffType.Linear,
                "log" => SoundRolloffType.Log,
                _ => SoundRolloffType.None,
            };
            return true;
        }

        if (tokens.Count < 4
            || !int.TryParse(tokens[2], out int minDistance)
            || !int.TryParse(tokens[3], out int maxDistance)
            || minDistance < 0
            || maxDistance < 0) return false;
        props.MinimumDistance = minDistance;
        props.MaximumDistance = maxDistance;
        return true;
    }

    private static SoundProperties GetSoundProperties(Dictionary<string, SoundProperties> properties, string name)
    {
        if (!properties.TryGetValue(name, out SoundProperties? props))
        {
            props = new SoundProperties();
            properties[name] = props;
        }
        return props;
    }

    private static bool TryParseAmbient(List<string> tokens, TerrainBaseGame? baseGame, out AmbientSoundDraft? ambient)
    {
        ambient = null;
        if (tokens.Count < 5 || !int.TryParse(tokens[1], out int index)) return false;
        if (baseGame == TerrainBaseGame.Doom && (index < 1 || index > 64)) return false;
        if (baseGame == TerrainBaseGame.Hexen && (index < 1 || index > 256)) return false;

        int i = 3;
        AmbientSoundType type = AmbientSoundType.None;
        double attenuation = 1.0;
        string typeToken = tokens[i].ToLowerInvariant();
        if (typeToken is "point" or "surround" or "world")
        {
            type = typeToken switch
            {
                "point" => AmbientSoundType.Point,
                "surround" => AmbientSoundType.Surround,
                "world" => AmbientSoundType.World,
                _ => AmbientSoundType.None,
            };
            i++;
            if (type == AmbientSoundType.Point && i < tokens.Count && TryParseDouble(tokens[i], out double parsedAttenuation) && parsedAttenuation >= 0.0)
            {
                attenuation = parsedAttenuation;
                i++;
            }
        }

        if (i >= tokens.Count) return false;
        AmbientSoundMode mode;
        double secondsMin = 0.0;
        double secondsMax = 0.0;
        double seconds = 0.0;
        string modeToken = tokens[i++].ToLowerInvariant();
        if (modeToken == "continuous")
        {
            mode = AmbientSoundMode.Continuous;
        }
        else if (modeToken == "random")
        {
            if (i + 1 >= tokens.Count
                || !TryParseDouble(tokens[i++], out secondsMin)
                || !TryParseDouble(tokens[i++], out secondsMax)
                || secondsMin < 0.0
                || secondsMax < 0.0) return false;
            mode = AmbientSoundMode.Random;
        }
        else if (modeToken == "periodic")
        {
            if (i >= tokens.Count || !TryParseDouble(tokens[i++], out seconds) || seconds < 0.0) return false;
            mode = AmbientSoundMode.Periodic;
        }
        else
        {
            return false;
        }

        if (i >= tokens.Count || !TryParseDouble(tokens[i], out double volume) || volume < 0.0) return false;
        ambient = new AmbientSoundDraft(index, tokens[2], type, mode, volume, attenuation, secondsMin, secondsMax, seconds);
        return true;
    }

    private static void ApplyAmbientSounds(
        SndInfo result,
        Dictionary<int, AmbientSoundDraft> ambientDrafts,
        Dictionary<string, SoundProperties> soundProperties)
    {
        foreach (AmbientSoundDraft draft in ambientDrafts.Values)
        {
            if (!TryResolveSoundProperties(draft.SoundName, result, soundProperties, new HashSet<string>(StringComparer.OrdinalIgnoreCase), out SoundProperties? props)
                || props is null)
            {
                continue;
            }

            double attenuation = props.Attenuation <= 0.0 ? 1.0 : props.Attenuation;
            double minimumRadius = props.MinimumDistance / attenuation;
            double maximumDistance = props.Rolloff == SoundRolloffType.Log
                ? props.MinimumDistance + props.RolloffFactor * props.MinimumDistance
                : props.MaximumDistance;
            double maximumRadius = maximumDistance / attenuation;
            result.AmbientSounds[draft.Index] = new AmbientSoundInfo(
                draft.Index,
                draft.SoundName,
                draft.Type,
                draft.Mode,
                draft.Volume,
                draft.Attenuation,
                draft.SecondsMin,
                draft.SecondsMax,
                draft.Seconds,
                minimumRadius,
                maximumRadius);
        }
    }

    private static bool TryResolveSoundProperties(
        string soundName,
        SndInfo result,
        Dictionary<string, SoundProperties> soundProperties,
        HashSet<string> visited,
        out SoundProperties? props)
    {
        props = null;
        if (!visited.Add(soundName)) return false;
        if (result.Sounds.ContainsKey(soundName) && soundProperties.TryGetValue(soundName, out props)) return true;
        if (result.Aliases.TryGetValue(soundName, out string? aliasTarget))
            return TryResolveSoundProperties(aliasTarget, result, soundProperties, visited, out props);
        if (result.RandomGroups.TryGetValue(soundName, out List<string>? randomSounds) && randomSounds.Count > 0)
            return TryResolveSoundProperties(randomSounds[0], result, soundProperties, visited, out props);
        return false;
    }

    private static bool TryParseDouble(string text, out double value)
        => double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);

    private static string StripLineComment(string line)
    {
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            if (quoted) continue;
            if (line[i] == '#') return line.Substring(0, i);
            if (line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/') return line.Substring(0, i);
        }
        return line;
    }

    private static List<string> Tokenize(string s)
    {
        var toks = new List<string>();
        int n = s.Length;
        for (int p = 0; p < n;)
        {
            char c = s[p];
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == '"')
            {
                var sb = new StringBuilder();
                p++;
                while (p < n && s[p] != '"')
                {
                    if (s[p] == '\\' && p + 1 < n) { sb.Append(s[p + 1]); p += 2; }
                    else sb.Append(s[p++]);
                }
                if (p < n) p++;
                toks.Add(sb.ToString());
                continue;
            }
            if (c is '{' or '}')
            {
                toks.Add(c.ToString());
                p++;
                continue;
            }

            int b = p;
            while (p < n && !char.IsWhiteSpace(s[p]) && s[p] != '"' && s[p] != '{' && s[p] != '}') p++;
            toks.Add(s.Substring(b, p - b));
        }
        return toks;
    }
}
