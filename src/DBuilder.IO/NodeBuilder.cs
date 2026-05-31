// ABOUTME: Runs an external node builder (ZDBSP/glbsp/...) over a WAD to generate NODES/SEGS/BLOCKMAP/REJECT.
// ABOUTME: Ports UDB's NodesCompiler approach: %FI/%FO parameter substitution, run the exe, read the result back.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DBuilder.IO;

/// <summary>Configuration for an external node builder: its executable and a parameter template.</summary>
/// <remarks>
/// Parameters use the UDB placeholders <c>%FI</c> (input WAD) and <c>%FO</c> (output WAD). A template that
/// omits <c>%FO</c> means the builder edits the input WAD in place (e.g. <c>"%FI"</c>); one that includes it
/// writes a separate output (e.g. <c>-o "%FO" "%FI"</c> for zdbsp).
/// </remarks>
public sealed record NodebuilderConfig(string Executable, string Parameters);

public static class NodeBuilder
{
    /// <summary>The outcome of a node build: the rebuilt WAD bytes on success, plus the tool's output.</summary>
    public sealed record Result(bool Success, byte[]? Output, string Message);

    public sealed record ProcessResult(bool Success, string Message);

    /// <summary>Substitutes the %FI/%FO placeholders in a parameter template (testable in isolation).</summary>
    public static string BuildArguments(string parameters, string inputFile, string outputFile)
        => parameters.Replace("%FI", inputFile).Replace("%FO", outputFile);

    /// <summary>True when the template produces a separate output file rather than editing the input in place.</summary>
    public static bool HasSeparateOutput(string parameters) => parameters.Contains("%FO");

    public static ProcessStartInfo CreateStartInfo(NodebuilderConfig config, string inputFile, string outputFile, string workingDirectory)
        => new()
        {
            FileName = config.Executable,
            Arguments = BuildArguments(config.Parameters, inputFile, outputFile),
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            ErrorDialog = false,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

    public static ProcessResult AnalyzeProcessResult(int exitCode, string standardOutput, string standardError)
    {
        string outMsg = CleanProcessOutput(standardOutput);
        string outErr = CleanProcessOutput(standardError);
        bool errorsInNormalOutput = outMsg.Length > 0 && outMsg.Contains("error", StringComparison.OrdinalIgnoreCase);
        bool errorsInErrorOutput = outErr.Length > 0 && outErr.Contains("error", StringComparison.OrdinalIgnoreCase);

        if (exitCode > 0 || errorsInNormalOutput || errorsInErrorOutput)
        {
            var message = new StringBuilder();
            if (errorsInNormalOutput) message.Append(outMsg);
            if (errorsInErrorOutput)
            {
                if (message.Length > 0) message.AppendLine();
                message.Append(outErr);
            }
            if (message.Length == 0)
                message.Append("Node builder exited with code ").Append(exitCode).Append('.');
            return new ProcessResult(false, message.ToString());
        }

        string successMessage = (outMsg + Environment.NewLine + outErr).Trim();
        return new ProcessResult(true, successMessage);
    }

    /// <summary>
    /// Writes <paramref name="wadBytes"/> to a temp file, runs the configured node builder, and returns the
    /// rebuilt WAD bytes. Never throws for the common failures (missing executable, non-zero exit, timeout);
    /// those come back as <see cref="Result"/> with Success=false and a descriptive message.
    /// </summary>
    public static Result Build(byte[] wadBytes, NodebuilderConfig cfg, int timeoutMs = 120000, string? mapMarker = null, GameConfiguration? config = null)
    {
        if (string.IsNullOrWhiteSpace(cfg.Executable) || !File.Exists(cfg.Executable))
            return new Result(false, null, $"Node builder executable not found: {cfg.Executable}");

        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_nodes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string input = Path.Combine(dir, "input.wad");
        bool separate = HasSeparateOutput(cfg.Parameters);
        string output = separate ? Path.Combine(dir, "output.wad") : input;

        try
        {
            File.WriteAllBytes(input, PrepareInputWad(wadBytes, mapMarker, config));

            var psi = CreateStartInfo(cfg, input, output, dir);

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            try { process.Start(); }
            catch (Exception ex) { return new Result(false, null, $"Failed to start node builder: {ex.Message}"); }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return new Result(false, null, $"Node builder timed out after {timeoutMs} ms");
            }
            process.WaitForExit(); // flush async output buffers

            ProcessResult processResult = AnalyzeProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
            if (!processResult.Success)
                return new Result(false, null, processResult.Message);
            if (!File.Exists(output))
                return new Result(false, null, $"Node builder produced no output file. {processResult.Message}");

            byte[] outputBytes = File.ReadAllBytes(output);
            if (!RequiredNodeBuildLumpsPresent(outputBytes, mapMarker, config))
                return new Result(false, null, $"Node builder failed to build the expected data structures. {processResult.Message}");

            return new Result(true, outputBytes, processResult.Message);
        }
        catch (Exception ex)
        {
            return new Result(false, null, $"Node build error: {ex.Message}");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort cleanup */ }
        }
    }

    private static byte[] PrepareInputWad(byte[] wadBytes, string? mapMarker, GameConfiguration? config)
    {
        if (string.IsNullOrWhiteSpace(mapMarker) || config is null || config.MapLumpNames.Count == 0)
            return wadBytes;

        try
        {
            using var ms = new MemoryStream();
            ms.Write(wadBytes, 0, wadBytes.Length);
            ms.Position = 0;
            using (var wad = new WAD(ms))
                WadMaps.RemoveUnneededMapLumps(wad, mapMarker, config, glNodesOnly: false);
            return ms.ToArray();
        }
        catch
        {
            return wadBytes;
        }
    }

    private static bool RequiredNodeBuildLumpsPresent(byte[] wadBytes, string? mapMarker, GameConfiguration? config)
    {
        if (string.IsNullOrWhiteSpace(mapMarker) || config is null || config.MapLumpNames.Count == 0)
            return true;

        try
        {
            using var wad = new WAD(new MemoryStream(wadBytes), openreadonly: true);
            return WadMaps.RequiredNodeBuildLumpsPresent(wad, mapMarker, config);
        }
        catch
        {
            return false;
        }
    }

    private static string CleanProcessOutput(string value)
        => value.Trim().Replace("\b", "", StringComparison.Ordinal);
}
