// ABOUTME: Parses and executes UDB-style external command settings from map options.
// ABOUTME: Supports one quoted command line per row and reports failures from exit codes or stderr.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DBuilder.IO;

public sealed record ExternalCommandInvocation(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory);

public sealed record ExternalCommandResult(bool Success, string Message)
{
    public static ExternalCommandResult Ok(string message) => new(true, message);
    public static ExternalCommandResult Fail(string message) => new(false, message);
}

public static class ExternalCommandLaunch
{
    public static ProcessStartInfo CreateStartInfo(ExternalCommandInvocation invocation)
    {
        var startInfo = new ProcessStartInfo(invocation.FileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (!string.IsNullOrWhiteSpace(invocation.WorkingDirectory))
            startInfo.WorkingDirectory = invocation.WorkingDirectory;
        foreach (string argument in invocation.Arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }
}

public static class ExternalCommand
{
    public static List<ExternalCommandInvocation> BuildInvocations(ExternalCommandSettings settings)
    {
        var result = new List<ExternalCommandInvocation>();
        foreach (string line in CommandLines(settings.Commands))
        {
            var tokens = SplitArguments(line);
            if (tokens.Count == 0) continue;
            result.Add(new ExternalCommandInvocation(tokens[0], tokens.GetRange(1, tokens.Count - 1), settings.WorkingDirectory));
        }
        return result;
    }

    public static ExternalCommandResult Run(ExternalCommandSettings settings, string label)
    {
        var invocations = BuildInvocations(settings);
        if (invocations.Count == 0) return ExternalCommandResult.Ok($"{label}: no command configured.");

        foreach (var invocation in invocations)
        {
            var startInfo = ExternalCommandLaunch.CreateStartInfo(invocation);

            try
            {
                using var process = Process.Start(startInfo);
                if (process is null) return ExternalCommandResult.Fail($"{label}: failed to start {invocation.FileName}.");
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                stdout.GetAwaiter().GetResult();
                string errorText = stderr.GetAwaiter().GetResult();
                if (settings.ExitCodeIsError && process.ExitCode != 0)
                    return ExternalCommandResult.Fail($"{label}: {invocation.FileName} exited with code {process.ExitCode}.");
                if (settings.StdErrIsError && !string.IsNullOrWhiteSpace(errorText))
                    return ExternalCommandResult.Fail($"{label}: {invocation.FileName} wrote to stderr.");
            }
            catch (Exception ex)
            {
                return ExternalCommandResult.Fail($"{label}: {ex.Message}");
            }
        }

        return ExternalCommandResult.Ok($"{label}: ran {invocations.Count} command(s).");
    }

    private static IEnumerable<string> CommandLines(string commands)
    {
        foreach (string line in commands.Replace("\r\n", "\n").Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0) yield return trimmed;
        }
    }

    private static List<string> SplitArguments(string command)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inQuote = false;
        bool hasToken = false;
        foreach (char ch in command)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(ch) && !inQuote)
            {
                if (hasToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(ch);
                hasToken = true;
            }
        }
        if (hasToken) tokens.Add(current.ToString());
        return tokens;
    }
}
