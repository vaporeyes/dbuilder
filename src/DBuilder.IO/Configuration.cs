// ABOUTME: Configuration text parser ported from UDB Source/Core/IO/Configuration.cs.
// ABOUTME: UDB's .cfg file format - similar to UDMF but supports null/bare-key entries, include() function, typed ReadSetting/WriteSetting path API, IDictionary-based storage (Hashtable or ListDictionary).

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 *
 * CFG file structure syntax:
 *   - // line comments and /* block * / comments allowed
 *   - simple settings: key = value;
 *   - strings in quotes: nick = "value";
 *   - decimals always use . (never ,)
 *   - true / false / null are keywords; null may be omitted (bare-key terminator: foo;)
 *   - structures use { } and may nest arbitrarily
 *   - include("file.cfg" [, "path"]) merges another config into current scope
 *   - keys may not contain spaces or the assignment operator
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;

namespace DBuilder.IO;

public sealed class Configuration
{
    public const string DEFAULT_SEPERATOR = ".";

    private const string ERROR_KEYMISSING = "Missing key name in assignment or scope.";
    private const string ERROR_KEYSPACES = "Spaces not allowed in key names.";
    private const string ERROR_ASSIGNINVALID = "Invalid assignment. Missing a previous terminator symbol?";
    private const string ERROR_VALUEINVALID = "Invalid value in assignment. Missing a previous terminator symbol?";
    private const string ERROR_VALUETOOBIG = "Value too big.";
    private const string ERROR_KEYWORDUNKNOWN = "Unknown keyword in assignment. Missing a previous terminator symbol?";
    private const string ERROR_UNEXPECTED_END = "Unexpected end of data. Missing a previous terminator symbol?";
    private const string ERROR_UNKNOWN_FUNCTION = "Unknown function call.";
    private const string ERROR_INVALID_ARGS = "Invalid function arguments.";
    private const string ERROR_INCLUDE_UNSUPPORTED = "Include function is not supported in data parsed from stream.";

    public const string NUMBERS = "0123456789";
    public const string NUMBERS2 = "0123456789-.&";

    private bool cpErrorResult;
    private string cpErrorDescription = "";
    private int cpErrorLine;
    private string cpErrorFile = "";
    private static readonly char[] space = new[] { ' ' }; //mxd
    private static readonly char[] newline = new[] { '\n' }; //mxd

    private IDictionary root;

    //mxd. Cache shared across all Configuration instances - matches UDB behavior.
    private static Dictionary<string, IDictionary> cfgcache = new Dictionary<string, IDictionary>(StringComparer.Ordinal);

    public bool ErrorResult => cpErrorResult;
    public string ErrorDescription => cpErrorDescription;
    public int ErrorLine => cpErrorLine;
    public string ErrorFile => cpErrorFile;
    public IDictionary Root { get => root; set => root = value; }
    public bool Sorted => (root is ListDictionary);

    public Configuration()
    {
        root = new Hashtable();
        NewConfiguration();
        GC.SuppressFinalize(this);
    }

    public Configuration(bool sorted)
    {
        root = sorted ? new ListDictionary() : new Hashtable();
        NewConfiguration(sorted);
        GC.SuppressFinalize(this);
    }

    public Configuration(string filename)
    {
        root = new Hashtable();
        LoadConfiguration(filename);
        GC.SuppressFinalize(this);
    }

    public Configuration(string filename, bool sorted)
    {
        root = sorted ? new ListDictionary() : new Hashtable();
        LoadConfiguration(filename, sorted);
        GC.SuppressFinalize(this);
    }

    // Recursively merges two structures
    private static IDictionary Combine(IDictionary d1, IDictionary d2, bool sorted)
    {
        IDictionary result = sorted ? new ListDictionary() : new Hashtable();

        IDictionaryEnumerator d1e = d1.GetEnumerator();
        while (d1e.MoveNext()) result.Add(d1e.Key, d1e.Value);

        IDictionaryEnumerator d2e = d2.GetEnumerator();
        while (d2e.MoveNext())
        {
            if (d2e.Value is IDictionary)
            {
                if (result.Contains(d2e.Key) && (result[d2e.Key] is IDictionary))
                {
                    result[d2e.Key] = Combine((IDictionary)result[d2e.Key]!, (IDictionary)d2e.Value, sorted);
                }
                else
                {
                    result[d2e.Key] = sorted
                        ? Combine(new ListDictionary(), (IDictionary)d2e.Value, true)
                        : Combine(new Hashtable(), (IDictionary)d2e.Value, false);
                }
            }
            else
            {
                if (result.Contains(d2e.Key))
                    result[d2e.Key] = d2e.Value;
                else
                    result.Add(d2e.Key, d2e.Value);
            }
        }

        return result;
    }

    private bool CheckSetting(IDictionary dic, string setting, string pathseperator)
    {
        string[] keys = setting.Split(pathseperator.ToCharArray());

        object item = dic;

        foreach (string key in keys)
        {
            if (item is IDictionary)
            {
                if (ValidateKey(key.Trim(), "", -1))
                {
                    IDictionary cs = (IDictionary)item;

                    if (cs.Contains(key))
                        item = cs[key]!;
                    else
                        return false;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private object? ReadAnySetting(string setting, object? defaultsetting, string pathseperator)
        => ReadAnySetting(root, setting, defaultsetting, pathseperator);
    private object? ReadAnySetting(IDictionary dic, string setting, object? defaultsetting, string pathseperator)
        => ReadAnySetting(dic, "", -1, setting, defaultsetting, pathseperator);

    private object? ReadAnySetting(IDictionary dic, string file, int line, string setting, object? defaultsetting, string pathseperator)
    {
        string[] keys = setting.Split(pathseperator.ToCharArray());

        object? item = dic;

        foreach (string key in keys)
        {
            if (item is IDictionary)
            {
                if (ValidateKey(key.Trim(), file, line))
                {
                    IDictionary cs = (IDictionary)item;

                    if (cs.Contains(key))
                        item = cs[key];
                    else
                        return defaultsetting;
                }
                else
                {
                    return defaultsetting;
                }
            }
            else
            {
                return defaultsetting;
            }
        }

        return item;
    }

    private static string EscapedString(string str)
    {
        // Replace the \ with \\ first!
        str = str.Replace("\\", "\\\\");
        str = str.Replace("\n", "\\n");
        str = str.Replace("\r", "\\r");
        str = str.Replace("\t", "\\t");
        str = str.Replace("\"", "\\\"");
        return str;
    }

    private void RaiseError(string file, int line, string description)
    {
        // First error wins - subsequent errors are silenced so the user sees the root cause.
        if (!cpErrorResult)
        {
            cpErrorResult = true;
            cpErrorDescription = description;
            cpErrorLine = line;
            cpErrorFile = file;
        }
    }

    // Validates a key. When errorline > -1, records the error.
    private bool ValidateKey(string key, string file, int errorline)
    {
        if (string.IsNullOrEmpty(key))
        {
            if (errorline > -1) RaiseError(file, errorline, ERROR_KEYMISSING);
            return false;
        }

        if (key.IndexOfAny(space) > -1)
        {
            if (errorline > -1) RaiseError(file, errorline, ERROR_KEYSPACES);
            return false;
        }

        return true;
    }

    private bool ValidateKeyword(string keyword, string file, int errorline)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            if (errorline > -1) RaiseError(file, errorline, ERROR_ASSIGNINVALID);
            return false;
        }

        if (keyword.IndexOfAny(space) > -1)
        {
            if (errorline > -1) RaiseError(file, errorline, ERROR_ASSIGNINVALID);
            return false;
        }

        return true;
    }

    private object? ParseAssignment(ref string file, ref string data, ref int pos, ref int line)
    {
        object? val = null;

        while ((pos < data.Length) && !cpErrorResult)
        {
            char c = data[pos++];

            if (c == '\"')
            {
                val = ParseString(ref file, ref data, ref pos, ref line);
                if (cpErrorResult) return null;
            }
            else if (NUMBERS2.IndexOf(c.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) > -1)
            {
                pos--; // this byte is part of the number
                val = ParseNumber(ref file, ref data, ref pos, ref line);
                if (cpErrorResult) return null;
            }
            else if (c == '\n')
            {
                line++;
            }
            else if (c == ';')
            {
                return val;
            }
            else if ((c != ' ') && (c != '\t'))
            {
                pos--; // this byte is part of the keyword
                val = ParseKeyword(ref file, ref data, ref pos, ref line);
                if (cpErrorResult) return null;
            }
        }

        RaiseError(file, line, ERROR_UNEXPECTED_END);
        return null;
    }

    private string? ParseString(ref string file, ref string data, ref int pos, ref int line)
    {
        string val = "";
        bool escape = false;

        while ((pos < data.Length) && !cpErrorResult)
        {
            char c = data[pos++];

            if (escape)
            {
                switch (c)
                {
                    case '\\': val += "\\"; break;
                    case 'n': val += "\n"; break;
                    case '\"': val += "\""; break;
                    case 'r': val += "\r"; break;
                    case 't': val += "\t"; break;
                    default:
                        // 3-digit ASCII escape sequence
                        if (NUMBERS.IndexOf(c.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) > -1)
                        {
                            int vv;
                            char vc;

                            string v = data.Substring(pos, 3);
                            try { vv = Convert.ToInt32(v.Trim(), CultureInfo.InvariantCulture); }
                            catch (FormatException)
                            {
                                RaiseError(file, line, ERROR_VALUEINVALID);
                                return null;
                            }

                            try { vc = Convert.ToChar(vv, CultureInfo.InvariantCulture); }
                            catch (FormatException)
                            {
                                RaiseError(file, line, ERROR_VALUEINVALID);
                                return null;
                            }

                            val += vc.ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            val += c.ToString(CultureInfo.InvariantCulture);
                        }
                        break;
                }

                escape = false;
            }
            else
            {
                switch (c) //mxd
                {
                    case '\\':
                        escape = true;
                        break;

                    case '\"':
                        return val;

                    case '\n':
                        line++;
                        break;

                    default:
                        val += c.ToString(CultureInfo.InvariantCulture);
                        break;
                }
            }
        }

        RaiseError(file, line, ERROR_UNEXPECTED_END);
        return null;
    }

    private object? ParseNumber(ref string file, ref string data, ref int pos, ref int line)
    {
        string val = "";

        while ((pos < data.Length) && !cpErrorResult)
        {
            char c = data[pos++];

            if ((c == ';') || (c == ',') || (c == ')'))
            {
                pos--; // not consumed - belongs to caller's terminator

                // Single-precision float? Postfix 'f'
                if (val.IndexOf("f", StringComparison.Ordinal) > -1)
                {
                    float fval;

                    try { fval = Convert.ToSingle(val.Trim().Replace("f", ""), CultureInfo.InvariantCulture); }
                    catch (FormatException)
                    {
                        RaiseError(file, line, ERROR_VALUEINVALID);
                        return null;
                    }
                    return fval;
                }
                else if (val.IndexOf(".", StringComparison.Ordinal) > -1)
                {
                    double dval;
                    try { dval = Convert.ToDouble(val.Trim(), CultureInfo.InvariantCulture); }
                    catch (FormatException)
                    {
                        RaiseError(file, line, ERROR_VALUEINVALID);
                        return null;
                    }
                    return dval;
                }
                else
                {
                    try
                    {
                        int ival = Convert.ToInt32(val.Trim(), CultureInfo.InvariantCulture);
                        return ival;
                    }
                    catch (OverflowException)
                    {
                        // Too large for Int32, try Int64
                        try
                        {
                            long lval = Convert.ToInt64(val.Trim(), CultureInfo.InvariantCulture);
                            return lval;
                        }
                        catch (OverflowException)
                        {
                            RaiseError(file, line, ERROR_VALUETOOBIG);
                            return null;
                        }
                        catch (FormatException)
                        {
                            RaiseError(file, line, ERROR_VALUEINVALID);
                            return null;
                        }
                    }
                    catch (FormatException)
                    {
                        RaiseError(file, line, ERROR_VALUEINVALID);
                        return null;
                    }
                }
            }
            else if (c == '\n')
            {
                line++;
            }
            else
            {
                val += c.ToString(CultureInfo.InvariantCulture);
            }
        }

        RaiseError(file, line, ERROR_UNEXPECTED_END);
        return null;
    }

    private object? ParseKeyword(ref string file, ref string data, ref int pos, ref int line)
    {
        string val = "";

        while ((pos < data.Length) && !cpErrorResult)
        {
            char c = data[pos++];

            if ((c == ';') || (c == ',') || (c == ')'))
            {
                pos--; // not consumed - belongs to caller's terminator

                if (ValidateKeyword(val.Trim(), file, line))
                {
                    switch (val.Trim().ToLowerInvariant())
                    {
                        case "true": return true;
                        case "false": return false;
                        case "null": return null;
                        default:
                            RaiseError(file, line, ERROR_KEYWORDUNKNOWN + "\nUnrecognized token: \"" + val.Trim().ToLowerInvariant() + "\"");
                            return null;
                    }
                }
            }
            else if (c == '\n')
            {
                line++;
            }
            else
            {
                val += c.ToString(CultureInfo.InvariantCulture);
            }
        }

        RaiseError(file, line, ERROR_UNEXPECTED_END);
        return null;
    }

    // include() function: merges another config file into the current scope (with optional sub-path).
    private void FunctionInclude(IDictionary cs, List<object?> args, ref string file, int line)
    {
        string data;

        if (string.IsNullOrEmpty(file)) RaiseError(file, line, ERROR_INCLUDE_UNSUPPORTED);
        if (args.Count < 1) RaiseError(file, line, ERROR_INVALID_ARGS);
        if (args.Count >= 1 && !(args[0] is string)) RaiseError(file, line, ERROR_INVALID_ARGS + " Expected a string for argument 1.");
        if ((args.Count > 1) && !(args[1] is string)) RaiseError(file, line, ERROR_INVALID_ARGS + " Expected a string for argument 2.");
        string? filename = Path.GetFileName(file);
        if (string.IsNullOrEmpty(filename)) RaiseError(file, line, "Invalid include statement: file name is missing."); //mxd
        else if (args[0]!.ToString()!.ToUpperInvariant() == filename.ToUpperInvariant()) RaiseError(file, line, "A file cannot call include() on itself."); //mxd
        if (cpErrorResult) return;

        // Resolve include path relative to the including file
        string includefile = Path.GetDirectoryName(file) + Path.DirectorySeparatorChar + args[0];
        includefile = includefile.Replace('\\', '/');

        //mxd. Caching of parsed includes
        if (cfgcache.ContainsKey(includefile))
        {
            IDictionary cinc = cfgcache[includefile];

            if ((args.Count > 1) && !string.IsNullOrEmpty(args[1]?.ToString()))
            {
                IDictionary def = cs is ListDictionary ? new ListDictionary() : new Hashtable();
                if (CheckSetting(cinc, args[1]!.ToString()!, DEFAULT_SEPERATOR))
                {
                    cinc = (IDictionary)ReadAnySetting(cinc, file, line, args[1]!.ToString()!, def, DEFAULT_SEPERATOR)!;
                }
                else
                {
                    RaiseError(file, line, "Include missing structure \"" + args[1] + "\" in file \"" + includefile + "\"");
                    return;
                }
            }

            IDictionary newcs = Combine(cs, cinc, (cs is ListDictionary));
            cs.Clear();
            foreach (DictionaryEntry de in newcs) cs.Add(de.Key, de.Value);
            return;
        }

        try
        {
            using FileStream fstream = File.OpenRead(includefile);
            byte[] fbuffer = new byte[fstream.Length];
            fstream.Read(fbuffer, 0, fbuffer.Length);
            data = Encoding.UTF8.GetString(fbuffer);
        }
        catch (Exception e)
        {
            RaiseError(file, line, "Unable to include file \"" + includefile + "\". " + e.GetType().Name + ": " + e.Message);
            return;
        }

        // Parser only handles \n line endings; normalize before parsing.
        data = data.Replace("\r", "");
        data = data.Replace("\t", "");

        IDictionary inc = cs is ListDictionary ? new ListDictionary() : new Hashtable();
        int npos = 0, nline = 1;
        InputStructure(inc, ref includefile, ref data, ref npos, ref nline);
        if (!cpErrorResult)
        {
            cfgcache.Add(includefile, inc);

            if ((args.Count > 1) && !string.IsNullOrEmpty(args[1]?.ToString()))
            {
                IDictionary def = cs is ListDictionary ? new ListDictionary() : new Hashtable();
                if (CheckSetting(inc, args[1]!.ToString()!, DEFAULT_SEPERATOR))
                {
                    inc = (IDictionary)ReadAnySetting(inc, file, line, args[1]!.ToString()!, def, DEFAULT_SEPERATOR)!;
                }
                else
                {
                    RaiseError(file, line, "Include missing structure \"" + args[1] + "\" in file \"" + includefile + "\"");
                    return;
                }
            }

            IDictionary newcs = Combine(cs, inc, (cs is ListDictionary));
            cs.Clear();
            foreach (DictionaryEntry de in newcs) cs.Add(de.Key, de.Value);
        }
    }

    private void ParseFunction(IDictionary cs, ref string file, ref string data, ref int pos, ref int line, ref string functionname)
    {
        List<object?> args = new List<object?>();
        while ((pos < data.Length) && !cpErrorResult)
        {
            char c = data[pos++];

            if (c == ')')
            {
                switch (functionname.Trim().ToLowerInvariant())
                {
                    case "include":
                        FunctionInclude(cs, args, ref file, line);
                        return;

                    default:
                        RaiseError(file, line, ERROR_UNKNOWN_FUNCTION);
                        return;
                }
            }

            if (c == '\"')
            {
                object? val = ParseString(ref file, ref data, ref pos, ref line);
                if (cpErrorResult) return;
                args.Add(val);
            }
            else if (NUMBERS2.IndexOf(c.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) > -1)
            {
                pos--;
                object? val = ParseNumber(ref file, ref data, ref pos, ref line);
                if (cpErrorResult) return;
                args.Add(val);
            }
            else if (c == '\n')
            {
                line++;
            }
            else if (c == ',')
            {
                // End of argument
            }
            else if ((c != ' ') && (c != '\t'))
            {
                pos--;
                object? val = ParseKeyword(ref file, ref data, ref pos, ref line);
                if (cpErrorResult) return;
                args.Add(val);
            }
        }

        RaiseError(file, line, ERROR_UNEXPECTED_END);
    }

    private void InputStructure(IDictionary cs, ref string file, ref string data, ref int pos, ref int line)
    {
        string key = "";

        while ((pos < data.Length) && !cpErrorResult)
        {
            char c = data[pos++];

            switch (c)
            {
                case '{':
                    if (ValidateKey(key.Trim(), file, line))
                    {
                        IDictionary cs2 = cs is ListDictionary ? new ListDictionary() : new Hashtable();
                        InputStructure(cs2, ref file, ref data, ref pos, ref line);
                        if (cs.Contains(key.Trim()) && (cs[key.Trim()] is IDictionary))
                            cs[key.Trim()] = Combine((IDictionary)cs[key.Trim()]!, cs2, (cs is ListDictionary));
                        else
                            cs[key.Trim()] = cs2;
                        key = "";
                    }
                    break;

                case '}':
                    return;

                case '(': // function call
                    ParseFunction(cs, ref file, ref data, ref pos, ref line, ref key);
                    key = "";
                    break;

                case '=':
                    if (ValidateKey(key.Trim(), file, line))
                    {
                        object? val = ParseAssignment(ref file, ref data, ref pos, ref line);
                        if (!cpErrorResult)
                        {
                            cs[key.Trim()] = val;
                            key = "";
                        }
                    }
                    break;

                case ';': // bare-key terminator - means null
                    if (!string.IsNullOrEmpty(key))
                    {
                        if (ValidateKey(key.Trim(), file, line))
                        {
                            cs[key.Trim()] = null;
                            key = "";
                        }
                    }
                    break;

                case '\n':
                    line++;
                    // Spaces aren't allowed in keys, but Trim() will strip these.
                    key += " ";
                    break;

                case '\\':
                case '/':
                    // Backtrack to use the current character in the comment-test substring
                    pos--;

                    if (data.Substring(pos, 2) == "//")
                    {
                        int np = data.IndexOf("\n", pos, StringComparison.Ordinal);
                        if (np > -1)
                        {
                            line++;
                            pos = np + 1;
                        }
                        else
                        {
                            pos = data.Length + 1;
                        }
                    }
                    else if (data.Substring(pos, 2) == "/*")
                    {
                        int np = data.IndexOf("*/", pos, StringComparison.Ordinal);

                        if (np > -1)
                        {
                            string blockdata = data.Substring(pos, np - pos + 2);
                            line += (blockdata.Split(newline).Length - 1);
                            pos = np + 2;
                        }
                        else
                        {
                            pos = data.Length + 1;
                        }
                    }
                    else
                    {
                        pos++;
                    }
                    break;

                default:
                    key += c.ToString(CultureInfo.InvariantCulture);
                    break;
            }
        }
    }

    private static string OutputStructure(IDictionary cs, int level, string newline, bool whitespace)
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

            IDictionaryEnumerator de = cs.GetEnumerator();

            for (int i = 0; i < cs.Count; i++)
            {
                de.MoveNext();

                if (de.Value == null)
                {
                    db.Append(leveltabs); db.Append(de.Key); db.Append(";"); db.Append(newline);
                }
                else if (de.Value is IDictionary)
                {
                    if (whitespace) { db.Append(leveltabs); db.Append(newline); }
                    db.Append(leveltabs); db.Append(de.Key); db.Append(newline);
                    db.Append(leveltabs); db.Append("{"); db.Append(newline);
                    db.Append(OutputStructure((IDictionary)de.Value, level + 1, newline, whitespace));
                    db.Append(leveltabs); db.Append("}"); db.Append(newline);
                }
                else if (de.Value is bool)
                {
                    if ((bool)de.Value)
                    {
                        db.Append(leveltabs); db.Append(de.Key); db.Append(spacing);
                        db.Append("="); db.Append(spacing); db.Append("true;"); db.Append(newline);
                    }
                    else
                    {
                        db.Append(leveltabs); db.Append(de.Key); db.Append(spacing);
                        db.Append("="); db.Append(spacing); db.Append("false;"); db.Append(newline);
                    }
                }
                else if (de.Value is float)
                {
                    // Single-precision float gets an 'f' postfix to round-trip distinctly from double.
                    db.Append(leveltabs); db.Append(de.Key); db.Append(spacing); db.Append("=");
                    db.Append(spacing); db.Append(String.Format(CultureInfo.InvariantCulture, "{0}", de.Value)); db.Append("f;"); db.Append(newline);
                }
                else if (de.Value.GetType().IsPrimitive)
                {
                    db.Append(leveltabs); db.Append(de.Key); db.Append(spacing); db.Append("=");
                    db.Append(spacing); db.Append(String.Format(CultureInfo.InvariantCulture, "{0}", de.Value)); db.Append(";"); db.Append(newline);
                }
                else
                {
                    db.Append(leveltabs); db.Append(de.Key); db.Append(spacing); db.Append("=");
                    db.Append(spacing); db.Append("\""); db.Append(EscapedString(de.Value.ToString()!)); db.Append("\";"); db.Append(newline);
                }
            }
        }

        return db.ToString();
    }

    public void ClearError()
    {
        cpErrorResult = false;
        cpErrorDescription = "";
        cpErrorLine = 0;
        cpErrorFile = "";
    }

    public void NewConfiguration() => NewConfiguration(false);

    public void NewConfiguration(bool sorted)
    {
        root = sorted ? new ListDictionary() : new Hashtable();
    }

    public bool SettingExists(string setting) => CheckSetting(root, setting, DEFAULT_SEPERATOR);
    public bool SettingExists(string setting, string pathseperator) => CheckSetting(root, setting, pathseperator);

    // Path-based typed read: never errors, falls back to default if path missing.
    public string? ReadSetting(string setting, string? defaultsetting) { object? r = ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR); return r?.ToString(); }
    public string? ReadSetting(string setting, string? defaultsetting, string pathseperator) { object? r = ReadAnySetting(setting, defaultsetting, pathseperator); return r?.ToString(); }
    public int ReadSetting(string setting, int defaultsetting) => Convert.ToInt32(ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR), CultureInfo.InvariantCulture);
    public int ReadSetting(string setting, int defaultsetting, string pathseperator) => Convert.ToInt32(ReadAnySetting(setting, defaultsetting, pathseperator), CultureInfo.InvariantCulture);
    public float ReadSetting(string setting, float defaultsetting) => Convert.ToSingle(ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR), CultureInfo.InvariantCulture);
    public float ReadSetting(string setting, float defaultsetting, string pathseperator) => Convert.ToSingle(ReadAnySetting(setting, defaultsetting, pathseperator), CultureInfo.InvariantCulture);
    public double ReadSetting(string setting, double defaultsetting) => Convert.ToDouble(ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR), CultureInfo.InvariantCulture);
    public double ReadSetting(string setting, double defaultsetting, string pathseperator) => Convert.ToDouble(ReadAnySetting(setting, defaultsetting, pathseperator), CultureInfo.InvariantCulture);
    public short ReadSetting(string setting, short defaultsetting) => Convert.ToInt16(ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR), CultureInfo.InvariantCulture);
    public short ReadSetting(string setting, short defaultsetting, string pathseperator) => Convert.ToInt16(ReadAnySetting(setting, defaultsetting, pathseperator), CultureInfo.InvariantCulture);
    public long ReadSetting(string setting, long defaultsetting) => Convert.ToInt64(ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR), CultureInfo.InvariantCulture);
    public long ReadSetting(string setting, long defaultsetting, string pathseperator) => Convert.ToInt64(ReadAnySetting(setting, defaultsetting, pathseperator), CultureInfo.InvariantCulture);
    public bool ReadSetting(string setting, bool defaultsetting) => Convert.ToBoolean(ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR), CultureInfo.InvariantCulture);
    public bool ReadSetting(string setting, bool defaultsetting, string pathseperator) => Convert.ToBoolean(ReadAnySetting(setting, defaultsetting, pathseperator), CultureInfo.InvariantCulture);
    public byte ReadSetting(string setting, byte defaultsetting) => Convert.ToByte(ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR), CultureInfo.InvariantCulture);
    public byte ReadSetting(string setting, byte defaultsetting, string pathseperator) => Convert.ToByte(ReadAnySetting(setting, defaultsetting, pathseperator), CultureInfo.InvariantCulture);
    public IDictionary? ReadSetting(string setting, IDictionary? defaultsetting) => (IDictionary?)ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR);
    public IDictionary? ReadSetting(string setting, IDictionary? defaultsetting, string pathseperator) => (IDictionary?)ReadAnySetting(setting, defaultsetting, pathseperator);

    public object? ReadSettingObject(string setting, object? defaultsetting) => ReadAnySetting(setting, defaultsetting, DEFAULT_SEPERATOR);

    public bool WriteSetting(string setting, object? settingvalue) => WriteSetting(setting, settingvalue, DEFAULT_SEPERATOR);

    public bool WriteSetting(string setting, object? settingvalue, string pathseperator)
    {
        IDictionary cs;

        string[] keys = setting.Split(pathseperator.ToCharArray());
        string finalkey = keys[keys.Length - 1];

        object item = root;

        // Walk/create the path structures
        for (int i = 0; i < (keys.Length - 1); i++)
        {
            if (ValidateKey(keys[i].Trim(), "", -1))
            {
                cs = (IDictionary)item;

                if (cs.Contains(keys[i]))
                {
                    if (cs[keys[i]] is IDictionary)
                        item = cs[keys[i]]!;
                    else
                        return false;
                }
                else
                {
                    IDictionary ncs = root is ListDictionary ? new ListDictionary() : new Hashtable();
                    cs.Add(keys[i], ncs);
                    item = cs[keys[i]]!;
                }
            }
            else
            {
                return false;
            }
        }

        cs = (IDictionary)item;

        if (cs.Contains(finalkey))
            cs[finalkey] = settingvalue;
        else
            cs.Add(finalkey, settingvalue);

        return true;
    }

    public bool DeleteSetting(string setting) => DeleteSetting(setting, DEFAULT_SEPERATOR);

    public bool DeleteSetting(string setting, string pathseperator)
    {
        IDictionary cs;

        string[] keys = setting.Split(pathseperator.ToCharArray());
        string finalkey = keys[keys.Length - 1];

        object item = root;

        for (int i = 0; i < (keys.Length - 1); i++)
        {
            if (ValidateKey(keys[i].Trim(), "", -1))
            {
                cs = (IDictionary)item;

                if (cs.Contains(keys[i]))
                {
                    if (cs[keys[i]] is IDictionary)
                        item = cs[keys[i]]!;
                    else
                        return false;
                }
                else
                {
                    // Original UDB creates the missing path even on delete - preserved verbatim.
                    IDictionary ncs = root is ListDictionary ? new ListDictionary() : new Hashtable();
                    cs.Add(keys[i], ncs);
                    item = cs[keys[i]]!;
                }
            }
            else
            {
                return false;
            }
        }

        cs = (IDictionary)item;

        if (cs.Contains(finalkey))
        {
            cs.Remove(finalkey);
            return true;
        }
        return false;
    }

    public bool SaveConfiguration(string filename) => SaveConfiguration(filename, "\r\n", true);
    public bool SaveConfiguration(string filename, string newline) => SaveConfiguration(filename, newline, true);

    public bool SaveConfiguration(string filename, string newline, bool whitespace)
    {
        if (File.Exists(filename)) File.Delete(filename);

        using FileStream fstream = File.OpenWrite(filename);

        string data = OutputConfiguration(newline, whitespace);
        byte[] baData = Encoding.UTF8.GetBytes(data);
        fstream.Write(baData, 0, baData.Length);
        fstream.Flush();

        return !cpErrorResult;
    }

    public string OutputConfiguration() => OutputConfiguration("\r\n", true);
    public string OutputConfiguration(string newline) => OutputConfiguration(newline, true);
    public string OutputConfiguration(string newline, bool whitespace) => OutputStructure(root, 0, newline, whitespace);

    public bool LoadConfiguration(string filename) => LoadConfiguration(filename, false);

    public bool LoadConfiguration(string filename, bool sorted)
    {
        if (!File.Exists(filename))
            throw new FileNotFoundException("File not found \"" + filename + "\"", filename);

        using FileStream fstream = File.OpenRead(filename);
        byte[] fbuffer = new byte[fstream.Length];
        fstream.Read(fbuffer, 0, fbuffer.Length);

        string data = Encoding.UTF8.GetString(fbuffer);

        return InputConfiguration(filename, data, sorted);
    }

    public bool InputConfiguration(string data) => InputConfiguration(data, false);
    public bool InputConfiguration(string data, bool sorted) => InputConfiguration("", data, sorted);

    private bool InputConfiguration(string file, string data, bool sorted)
    {
        // Parser uses \n only; normalize line endings and strip tabs.
        data = data.Replace("\r", "");
        data = data.Replace("\t", "");

        ClearError();

        root = sorted ? new ListDictionary() : new Hashtable();
        int pos = 0, line = 1;
        InputStructure(root, ref file, ref data, ref pos, ref line);

        return !cpErrorResult;
    }
}
