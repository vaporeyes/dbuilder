// ABOUTME: Parser for DeHackEd patch data used by Doom-family source ports.
// ABOUTME: Captures thing, frame, text, and sprite replacement blocks without applying them to game config.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DBuilder.IO;

public sealed class DehackedPatch
{
    public List<DehackedThing> Things { get; } = new();
    public Dictionary<int, DehackedFrame> Frames { get; } = new();
    public Dictionary<string, string> Texts { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, string> NewSprites { get; } = new();
    public Dictionary<string, string> SpriteReplacements { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? DoomVersion { get; set; }
    public string? PatchFormat { get; set; }

    public Dictionary<int, string> Sprites => NewSprites;
}

public sealed class DehackedThing
{
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DehackedFrame
{
    public int Number { get; init; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class DehackedParser
{
    private static readonly Regex ThingHeader = new(@"^thing\s+(-?\d+)(?:\s+\((.+)\))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FrameHeader = new(@"^frame\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TextHeader = new(@"^text\s+(\d+)\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static DehackedPatch Parse(string text)
    {
        var patch = new DehackedPatch();
        var state = new ParserState(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
        while (!state.End)
        {
            string line = state.ReadCleanLine();
            if (line.Length == 0) continue;
            string lower = line.ToLowerInvariant();
            if (lower.StartsWith("thing", StringComparison.Ordinal)) ParseThing(patch, state, line);
            else if (lower.StartsWith("frame", StringComparison.Ordinal)) ParseFrame(patch, state, line);
            else if (lower.StartsWith("[sprites]", StringComparison.Ordinal)) ParseSprites(patch, state);
            else if (lower.StartsWith("text", StringComparison.Ordinal)) ParseText(patch, state, line);
            else if (lower.StartsWith("doom version", StringComparison.Ordinal)) patch.DoomVersion = ReadValue(line);
            else if (lower.StartsWith("patch format", StringComparison.Ordinal)) patch.PatchFormat = ReadValue(line);
            else SkipBlock(state);
        }
        return patch;
    }

    private static void ParseThing(DehackedPatch patch, ParserState state, string header)
    {
        var match = ThingHeader.Match(header);
        if (!match.Success) return;
        int number = int.Parse(match.Groups[1].Value);
        string name = match.Groups[2].Success ? match.Groups[2].Value : "<DeHackEd thing " + number + ">";
        var thing = new DehackedThing { Number = number, Name = name };
        ReadProperties(state, thing.Properties, allowEditorKeys: true);
        patch.Things.Add(thing);
    }

    private static void ParseFrame(DehackedPatch patch, ParserState state, string header)
    {
        var match = FrameHeader.Match(header);
        if (!match.Success) return;
        var frame = new DehackedFrame { Number = int.Parse(match.Groups[1].Value) };
        ReadProperties(state, frame.Properties, allowEditorKeys: false);
        patch.Frames[frame.Number] = frame;
    }

    private static void ParseSprites(DehackedPatch patch, ParserState state)
    {
        while (!state.End)
        {
            string line = state.ReadCleanLine();
            if (line.Length == 0) break;
            if (!TryReadKeyValue(line, out string key, out string value)) continue;
            if (value.Length != 4) continue;
            if (int.TryParse(key, out int index)) patch.NewSprites[index] = value;
            else if (key.Length == 4) patch.SpriteReplacements[key] = value;
        }
    }

    private static void ParseText(DehackedPatch patch, ParserState state, string header)
    {
        var match = TextHeader.Match(header);
        if (!match.Success) return;
        int oldLength = int.Parse(match.Groups[1].Value);
        int newLength = int.Parse(match.Groups[2].Value);
        string oldText = state.ReadChars(oldLength);
        string newText = state.ReadChars(newLength);
        state.ConsumeLineBreak();
        patch.Texts[oldText] = newText;
    }

    private static void ReadProperties(ParserState state, Dictionary<string, string> properties, bool allowEditorKeys)
    {
        while (!state.End)
        {
            string line = state.ReadCleanLine();
            if (line.Length == 0) break;
            if (allowEditorKeys && line.StartsWith("#$", StringComparison.Ordinal)) line = line.Substring(1);
            if (line.StartsWith("#", StringComparison.Ordinal)) continue;
            if (TryReadKeyValue(line, out string key, out string value)) properties[key] = value;
        }
    }

    private static void SkipBlock(ParserState state)
    {
        while (!state.End && state.ReadCleanLine().Length > 0) { }
    }

    private static bool TryReadKeyValue(string line, out string key, out string value)
    {
        int pos = line.IndexOf('=');
        if (pos < 0)
        {
            pos = line.IndexOf(':');
        }
        if (pos < 0)
        {
            key = "";
            value = "";
            return false;
        }
        key = line.Substring(0, pos).Trim();
        value = line.Substring(pos + 1).Trim();
        return key.Length > 0;
    }

    private static string ReadValue(string line)
    {
        if (!TryReadKeyValue(line, out _, out string value)) return "";
        return value;
    }

    private static string CleanLine(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith("#$", StringComparison.Ordinal)) return trimmed;
        int comment = trimmed.IndexOf('#');
        if (comment < 0) return trimmed;
        if (trimmed.StartsWith("ID #", StringComparison.OrdinalIgnoreCase))
        {
            int nextComment = trimmed.IndexOf('#', comment + 1);
            return nextComment < 0 ? trimmed : trimmed.Substring(0, nextComment).Trim();
        }
        return trimmed.Substring(0, comment).Trim();
    }

    private sealed class ParserState
    {
        private readonly string text;
        private int position;

        public ParserState(string text)
        {
            this.text = text;
        }

        public bool End => position >= text.Length;

        public string ReadCleanLine()
        {
            return CleanLine(ReadRawLine());
        }

        public string ReadChars(int count)
        {
            int available = Math.Min(count, text.Length - position);
            string value = text.Substring(position, available);
            position += available;
            return value;
        }

        public void ConsumeLineBreak()
        {
            if (position < text.Length && text[position] == '\n') position++;
        }

        private string ReadRawLine()
        {
            if (End) return "";
            int start = position;
            while (position < text.Length && text[position] != '\n') position++;
            string line = text.Substring(start, position - start);
            ConsumeLineBreak();
            return line;
        }
    }
}
