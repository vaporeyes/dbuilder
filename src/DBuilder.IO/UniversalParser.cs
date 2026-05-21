// ABOUTME: UDMF text parser ported from UDB Source/Core/IO/UniversalParser.cs.
// ABOUTME: Configuration.NUMBERS/NUMBERS2 constants inlined here as ParserNumbers so the parser is independent of the Configuration port.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DBuilder.IO;

public sealed class UniversalParser
{
    // These are duplicates of Configuration.NUMBERS / NUMBERS2 — kept inline so the parser doesn't depend on the Configuration port.
    internal const string ParserNumbers = "0123456789";
    internal const string ParserNumbers2 = "0123456789-.&";

    // Allowed characters in a key
    private const string KEY_CHARACTERS = "abcdefghijklmnopqrstuvwxyz0123456789_";

    // Parse mode constants
    private const int PM_NOTHING = 0;
    private const int PM_ASSIGNMENT = 1;
    private const int PM_NUMBER = 2;
    private const int PM_STRING = 3;
    private const int PM_KEYWORD = 4;

    private const string ERROR_KEYMISSING = "Missing key name in assignment or scope.";
    private const string ERROR_KEYCHARACTERS = "Invalid characters in key name.";
    private const string ERROR_VALUEINVALID = "Invalid value in assignment. Missing a previous terminator symbol?";
    private const string ERROR_VALUETOOBIG = "Value too big.";
    private const string ERROR_KEYWITHOUTVALUE = "Key has no value assigned.";
    private const string ERROR_KEYWORDUNKNOWN = "Unknown keyword in assignment. Missing a previous terminator symbol?";

    // Error result
    private int cpErrorResult;
    private string cpErrorDescription = "";
    private int cpErrorLine;

    // Warnings
    private List<string> warnings = new List<string>();

    // Configuration root
    private UniversalCollection root = new UniversalCollection();

    private const string newline = "\n";
    private StringBuilder key = new();  //mxd
    private StringBuilder val = new();  //mxd
    private Dictionary<string, UniversalEntry> matches = new(); //mxd

    // Settings
    private bool strictchecking = true;

    public int ErrorResult => cpErrorResult;
    public string ErrorDescription => cpErrorDescription;
    public int ErrorLine => cpErrorLine;
    public UniversalCollection Root => root;
    public bool StrictChecking { get => strictchecking; set => strictchecking = value; }
    public bool HasWarnings => warnings.Count != 0;
    public List<string> Warnings => warnings;

    public UniversalParser()
    {
        NewConfiguration();
        GC.SuppressFinalize(this);
    }

    /// <summary>Load configuration from a file.</summary>
    public UniversalParser(string filename)
    {
        LoadConfiguration(filename);
        GC.SuppressFinalize(this);
    }

    // Adds escape sequences for output
    private static string EscapedString(string str)
    {
        // Replace the \ with \\ first!
        str = str.Replace("\\", "\\\\");
        str = str.Replace(newline, "\\n");
        str = str.Replace("\r", "\\r");
        str = str.Replace("\t", "\\t");
        str = str.Replace("\"", "\\\"");
        return str;
    }

    private void RaiseError(int line, string description)
    {
        cpErrorResult = 1;
        cpErrorDescription = description;
        cpErrorLine = line + 1; //mxd
    }

    // Validates a key. When errorline > -1, records the error.
    private bool ValidateKey(string key, int errorline)
    {
        if (key.Length == 0)
        {
            if (errorline > -1) RaiseError(errorline, ERROR_KEYMISSING);
            return false;
        }

        if (strictchecking)
        {
            // Check if all characters are valid
            string keylc = key.ToLowerInvariant(); //mxd. UDMF key names are case-insensitive
            foreach (char c in keylc)
            {
                if (KEY_CHARACTERS.IndexOf(c) == -1)
                {
                    if (errorline > -1) RaiseError(errorline, ERROR_KEYCHARACTERS);
                    return false;
                }
            }
        }

        return true;
    }

    // Recursive descent through the UDMF grammar; mutates pos/line/error state.
    private UniversalCollection InputStructure(ref string[] data, ref int pos, ref int line, bool topLevel)
    {
        int pm = PM_NOTHING;            // current parse mode
        key.Remove(0, key.Length);
        val.Remove(0, val.Length);
        bool escape = false;            // escape sequence?
        bool endofstruct = false;       // true as soon as this level struct ends
        UniversalCollection cs = new UniversalCollection();

        while ((cpErrorResult == 0) && (endofstruct == false))
        {
            if (pos > data[line].Length - 1)
            {
                pos = 0;
                line++;

                // Stop if we have reached the end of the data
                if (line == data.Length)
                    break;

                if (string.IsNullOrEmpty(data[line])) continue; //mxd. Skip empty lines so error line numbers stay accurate
            }

            char c = data[line][pos]; // current data character

            if (pm == PM_NOTHING)
            {
                switch (c)
                {
                    case '{': // Begin of new struct
                        {
                            string s = key.ToString().Trim();
                            if (ValidateKey(s, line))
                            {
                                pos++;
                                cs.Add(new UniversalEntry(s.ToLowerInvariant(), InputStructure(ref data, ref pos, ref line, false)));
                                pos--;
                                key.Remove(0, key.Length);
                            }
                            break;
                        }

                    case '}': // End of this struct
                        endofstruct = true;
                        break;

                    case '=': // Assignment
                        if (ValidateKey(key.ToString().Trim(), line))
                            pm = PM_ASSIGNMENT;
                        break;

                    case ';': // Terminator with no value
                        if (ValidateKey(key.ToString().Trim(), line))
                            RaiseError(line, ERROR_KEYWITHOUTVALUE);
                        break;

                    case '\n': // New line
                        line++;
                        pos = -1;
                        // Spaces are not allowed in keys, but Trim will strip these.
                        key.Append(" ");
                        break;

                    case '\\': // Possible comment
                    case '/':
                        // Line comment //
                        if (data[line].Substring(pos, 2) == "//")
                        {
                            pos = -1;
                            if (line < data.Length - 1) line++;
                        }
                        // Block comment /* */
                        else if (data[line].Substring(pos, 2) == "/*")
                        {
                            int np = data[line].IndexOf("*/", pos);
                            if (np > -1)
                            {
                                pos = np + 1;
                            }
                            else
                            {
                                line++;
                                while ((np = data[line].IndexOf("*/", 0)) == -1)
                                {
                                    if (line == data.Length - 1) break;
                                    line++;
                                }

                                if (np > -1)
                                    pos = np + 1;
                            }
                        }
                        break;

                    default: // Everything else
                        if (pos != -1) key.Append(c);
                        break;
                }
            }
            else if (pm == PM_ASSIGNMENT)
            {
                // String opening
                if (c == '\"')
                {
                    pm = PM_STRING;
                }
                // Numeric start
                else if (ParserNumbers2.IndexOf(c) > -1)
                {
                    pm = PM_NUMBER;
                    pos--; // this byte is part of the number
                }
                else if (c == '\n')
                {
                    line++;
                }
                // Assignment terminator
                else if (c == ';')
                {
                    pm = PM_NOTHING;
                    key.Remove(0, key.Length);
                    val.Remove(0, val.Length);
                }
                // Anything non-whitespace starts a keyword
                else if ((c != ' ') && (c != '\t'))
                {
                    pm = PM_KEYWORD;
                    pos--; // this byte is part of the keyword
                }
            }
            else if (pm == PM_NUMBER)
            {
                if (c == ';')
                {
                    string s = val.ToString();
                    // Hexadecimal?
                    if ((s.Length > 2) && s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            int ival = Convert.ToInt32(s.Substring(2).Trim(), 16);
                            cs.Add(new UniversalEntry(key.ToString().Trim().ToLowerInvariant(), ival));
                        }
                        catch (OverflowException)
                        {
                            // Too large for Int32, try Int64
                            try
                            {
                                long lval = Convert.ToInt64(s.Substring(2).Trim(), 16);
                                cs.Add(new UniversalEntry(key.ToString().Trim().ToLowerInvariant(), lval));
                            }
                            catch (OverflowException)
                            {
                                RaiseError(line, ERROR_VALUETOOBIG);
                            }
                            catch (FormatException)
                            {
                                RaiseError(line, ERROR_VALUEINVALID + "\n\nUnrecognized token: \"" + s.Trim() + "\"");
                            }
                        }
                        catch (FormatException)
                        {
                            RaiseError(line, ERROR_VALUEINVALID + "\n\nUnrecognized token: \"" + s.Trim() + "\"");
                        }
                    }
                    //mxd. Can also be in scientific notation (e.g. "1E-06")
                    else if (s.IndexOf('.') > -1 || s.ToLowerInvariant().Contains("e-"))
                    {
                        double fval = 0;
                        try { fval = Convert.ToDouble(s.Trim(), CultureInfo.InvariantCulture); }
                        catch (FormatException)
                        {
                            RaiseError(line, ERROR_VALUEINVALID + "\n\nUnrecognized token: \"" + s.Trim() + "\"");
                        }
                        cs.Add(new UniversalEntry(key.ToString().Trim().ToLowerInvariant(), fval));
                    }
                    else
                    {
                        try
                        {
                            int ival = Convert.ToInt32(s.Trim(), CultureInfo.InvariantCulture);
                            cs.Add(new UniversalEntry(key.ToString().Trim().ToLowerInvariant(), ival));
                        }
                        catch (OverflowException)
                        {
                            try
                            {
                                long lval = Convert.ToInt64(s.Trim(), CultureInfo.InvariantCulture);
                                cs.Add(new UniversalEntry(key.ToString().Trim().ToLowerInvariant(), lval));
                            }
                            catch (OverflowException)
                            {
                                RaiseError(line, ERROR_VALUETOOBIG);
                            }
                            catch (FormatException)
                            {
                                RaiseError(line, ERROR_VALUEINVALID + "\n\nUnrecognized token: \"" + s.Trim() + "\"");
                            }
                        }
                        catch (FormatException)
                        {
                            RaiseError(line, ERROR_VALUEINVALID + "\n\nUnrecognized token: \"" + s.Trim() + "\"");
                        }
                    }

                    key.Remove(0, key.Length);
                    val.Remove(0, val.Length);
                    pm = PM_NOTHING;
                }
                else if (c == '\n')
                {
                    line++;
                    pos = -1;
                }
                else
                {
                    val.Append(c);
                }
            }
            else if (pm == PM_STRING)
            {
                if (escape)
                {
                    switch (c)
                    {
                        case '\\': val.Append('\\'); break;
                        case 'n': val.Append(newline); break;
                        case '\"': val.Append('\"'); break;
                        case 'r': val.Append('\r'); break;
                        case 't': val.Append('\t'); break;
                        default:
                            // Numeric character escape: \NNN — 3-digit ASCII code.
                            if (ParserNumbers.IndexOf(c) > -1)
                            {
                                int vv = 0;
                                char vc = '0';

                                string v = data[line].Substring(pos, 3);
                                try { vv = Convert.ToInt32(v.Trim(), CultureInfo.InvariantCulture); }
                                catch (FormatException)
                                {
                                    RaiseError(line, ERROR_VALUEINVALID + "\n\nUnrecognized token: \"" + v.Trim() + "\"");
                                }

                                try { vc = Convert.ToChar(vv, CultureInfo.InvariantCulture); }
                                catch (FormatException)
                                {
                                    RaiseError(line, ERROR_VALUEINVALID + "\n\nUnrecognized token: \"" + v.Trim() + "\"");
                                }

                                val.Append(vc);
                            }
                            else
                            {
                                val.Append(c);
                            }
                            break;
                    }

                    escape = false;
                }
                else
                {
                    if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '\"')
                    {
                        cs.Add(new UniversalEntry(key.ToString().Trim().ToLowerInvariant(), val.ToString()));
                        pm = PM_ASSIGNMENT;
                        key.Remove(0, key.Length);
                        val.Remove(0, val.Length);
                    }
                    else if (c == '\n')
                    {
                        line++;
                        pos = -1;
                    }
                    else
                    {
                        val.Append(c);
                    }
                }
            }
            else if (pm == PM_KEYWORD)
            {
                if (c == ';')
                {
                    switch (val.ToString().Trim().ToLowerInvariant())
                    {
                        case "true":
                            cs.Add(new UniversalEntry(key.ToString().Trim().ToLowerInvariant(), true));
                            break;

                        case "false":
                            cs.Add(new UniversalEntry(key.ToString().Trim().ToLowerInvariant(), false));
                            break;

                        case "nan":
                            // UDMF spec doesn't allow NaN; drop it with a warning rather than crash.
                            warnings.Add("UDMF map data line " + (line + 1) + ": value of field " + key.ToString().Trim().ToLowerInvariant() + " has a value of NaN (not a number). Field is being dropped permanently.");
                            break;

                        default:
                            RaiseError(line, ERROR_KEYWORDUNKNOWN + "\n\nUnrecognized token: \"" + val.ToString().Trim() + "\"");
                            break;
                    }

                    pm = PM_NOTHING;
                    key.Remove(0, key.Length);
                    val.Remove(0, val.Length);
                }
                else if (c == '\n')
                {
                    line++;
                    pos = -1;
                }
                else
                {
                    val.Append(c);
                }
            }

            pos++;
        }

        return cs;
    }

    // Serializes a collection back to UDMF text.
    private string OutputStructure(UniversalCollection cs, int level, string newline, bool whitespace)
    {
        string leveltabs = "";
        string spacing = "";
        StringBuilder db = new StringBuilder("");

        if (cs.Count > 0)
        {
            if (whitespace)
            {
                for (int i = 0; i < level; i++) leveltabs += "\t";
                spacing = " ";
            }

            for (int i = 0; i < cs.Count; i++)
            {
                if (cs[i].Value is UniversalCollection)
                {
                    UniversalCollection c = (UniversalCollection)cs[i].Value;

                    if (whitespace) { db.Append(leveltabs); db.Append(newline); }
                    db.Append(leveltabs); db.Append(cs[i].Key);
                    if (!string.IsNullOrEmpty(c.Comment))
                    {
                        if (whitespace) db.Append("\t");
                        db.Append("// " + c.Comment);
                    }
                    db.Append(newline);
                    db.Append(leveltabs); db.Append("{"); db.Append(newline);
                    db.Append(OutputStructure(c, level + 1, newline, whitespace));
                    db.Append(leveltabs); db.Append("}"); db.Append(newline);
                }
                else if (cs[i].Value is bool)
                {
                    db.Append(leveltabs); db.Append(cs[i].Key); db.Append(spacing); db.Append("="); db.Append(spacing);
                    db.Append((bool)cs[i].Value ? "true;" : "false;"); db.Append(newline);
                }
                else if (cs[i].Value is float)
                {
                    float f = (float)cs[i].Value;
                    db.Append(leveltabs); db.Append(cs[i].Key); db.Append(spacing); db.Append("=");
                    db.Append(spacing); db.Append(f.ToString("0.000", CultureInfo.InvariantCulture)); db.Append(";"); db.Append(newline);
                }
                //mxd. Doubles get more precision
                else if (cs[i].Value is double)
                {
                    double d = (double)cs[i].Value;
                    db.Append(leveltabs); db.Append(cs[i].Key); db.Append(spacing); db.Append("=");
                    db.Append(spacing); db.Append(d.ToString("0.0##############", CultureInfo.InvariantCulture)); db.Append(";"); db.Append(newline);
                }
                else if (cs[i].Value.GetType().IsPrimitive)
                {
                    db.Append(leveltabs); db.Append(cs[i].Key); db.Append(spacing); db.Append("=");
                    db.Append(spacing); db.Append(String.Format(CultureInfo.InvariantCulture, "{0}", cs[i].Value)); db.Append(";"); db.Append(newline);
                }
                else
                {
                    db.Append(leveltabs); db.Append(cs[i].Key); db.Append(spacing); db.Append("=");
                    db.Append(spacing); db.Append("\""); db.Append(EscapedString(cs[i].Value.ToString()!)); db.Append("\";"); db.Append(newline);
                }
            }
        }

        return db.ToString();
    }

    public void ClearError()
    {
        cpErrorResult = 0;
        cpErrorDescription = "";
        cpErrorLine = 0;
    }

    public void NewConfiguration()
    {
        root = new UniversalCollection();
    }

    public bool SaveConfiguration(string filename) => SaveConfiguration(filename, "\r\n", true);
    public bool SaveConfiguration(string filename, string newline) => SaveConfiguration(filename, newline, true);

    public bool SaveConfiguration(string filename, string newline, bool whitespace)
    {
        if (File.Exists(filename)) File.Delete(filename);

        using FileStream fstream = File.OpenWrite(filename);

        string data = OutputConfiguration(newline, whitespace);
        byte[] baData = Encoding.ASCII.GetBytes(data);
        fstream.Write(baData, 0, baData.Length);
        fstream.Flush();

        return cpErrorResult == 0;
    }

    public string OutputConfiguration() => OutputConfiguration("\r\n", true);
    public string OutputConfiguration(string newline) => OutputConfiguration(newline, true);
    public string OutputConfiguration(string newline, bool whitespace) => OutputStructure(root, 0, newline, whitespace);

    public bool LoadConfiguration(string filename)
    {
        if (!File.Exists(filename))
        {
            throw new FileNotFoundException("File not found \"" + filename + "\"", filename);
        }

        List<string> data = new List<string>(100);
        using (FileStream stream = File.OpenRead(filename))
        {
            StreamReader reader = new StreamReader(stream, Encoding.ASCII);

            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrEmpty(line)) continue;
                data.Add(line);
            }
        }

        return InputConfiguration(data.ToArray());
    }

    public bool InputConfiguration(string[] data)
    {
        ClearError();

        int pos = 0;
        int line = 0; //mxd
        matches = new Dictionary<string, UniversalEntry>(StringComparer.Ordinal); //mxd
        key = new StringBuilder(16); //mxd
        val = new StringBuilder(16); //mxd
        root = InputStructure(ref data, ref pos, ref line, true);

        return (cpErrorResult == 0);
    }

    /// <summary>Convenience: parse from a single string buffer (splits on newlines).</summary>
    public bool InputConfiguration(string text)
    {
        // UDMF files in the wild use both \n and \r\n line endings; normalize before splitting.
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return InputConfiguration(text.Split('\n'));
    }
}
