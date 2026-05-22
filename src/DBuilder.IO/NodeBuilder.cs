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

    /// <summary>Substitutes the %FI/%FO placeholders in a parameter template (testable in isolation).</summary>
    public static string BuildArguments(string parameters, string inputFile, string outputFile)
        => parameters.Replace("%FI", inputFile).Replace("%FO", outputFile);

    /// <summary>True when the template produces a separate output file rather than editing the input in place.</summary>
    public static bool HasSeparateOutput(string parameters) => parameters.Contains("%FO");

    /// <summary>
    /// Writes <paramref name="wadBytes"/> to a temp file, runs the configured node builder, and returns the
    /// rebuilt WAD bytes. Never throws for the common failures (missing executable, non-zero exit, timeout);
    /// those come back as <see cref="Result"/> with Success=false and a descriptive message.
    /// </summary>
    public static Result Build(byte[] wadBytes, NodebuilderConfig cfg, int timeoutMs = 120000)
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
            File.WriteAllBytes(input, wadBytes);

            var psi = new ProcessStartInfo
            {
                FileName = cfg.Executable,
                Arguments = BuildArguments(cfg.Parameters, input, output),
                WorkingDirectory = dir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

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

            string msg = (stdout.ToString() + stderr.ToString()).Trim();
            if (process.ExitCode != 0)
                return new Result(false, null, $"Node builder exited with code {process.ExitCode}. {msg}");
            if (!File.Exists(output))
                return new Result(false, null, $"Node builder produced no output file. {msg}");

            return new Result(true, File.ReadAllBytes(output), msg);
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
}
