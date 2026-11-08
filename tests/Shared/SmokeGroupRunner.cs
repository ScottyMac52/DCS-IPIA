using System.IO;
using System.Diagnostics;
using System.Text;

namespace Heimdall.TestSupport
{
    /// <summary>
    /// Runs named smoke groups sequentially, each in its own process, and aggregates one report. A group passes only when its
    /// process exits zero within the timeout and prints its pass marker; anything else (failure, timeout, missing result,
    /// launch failure) is reported and the next group still runs. Any required group that is not PASS makes the run fail,
    /// including groups skipped by an explicit subset, so a subset run can never be full acceptance.
    /// </summary>
    internal sealed class SmokeGroupRunner(IReadOnlyList<string> groups, Func<string, TimeSpan, (int? Exit, string Output, bool TimedOut)> launch,
        TimeSpan timeout, string logDirectory)
    {
        #region BEGIN Public
        /// <summary>Gets the line a group process prints after every scenario in it completed.</summary>
        internal const string PassMarker = "SMOKE-GROUP-PASS ";

        /// <summary>Runs every group (or only the named subset), writes per-group logs and a summary, and returns the exit code.</summary>
        internal int Run(IReadOnlyCollection<string>? only, TextWriter report)
        {
            ArgumentNullException.ThrowIfNull(report);
            var unknown = only?.Where(name => !groups.Contains(name, StringComparer.Ordinal)).ToArray() ?? [];
            if (unknown.Length > 0) { report.WriteLine("Unknown smoke group(s): " + string.Join(", ", unknown)); return 2; }
            Directory.CreateDirectory(logDirectory);
            var summary = new StringBuilder();
            var failed = false;
            foreach (var group in groups)
            {
                var (status, detail) = only is not null && !only.Contains(group) ? ("NOTRUN", "not selected") : Execute(group);
                failed |= status != "PASS";
                var line = $"{status,-7} {group} {detail}".TrimEnd();
                summary.AppendLine(line); report.WriteLine("Smoke group: " + line);
            }
            if (only is not null) summary.AppendLine("SUBSET RUN: NOT FULL ACCEPTANCE");
            summary.AppendLine(failed ? "RESULT: FAIL" : "RESULT: PASS");
            File.WriteAllText(Path.Combine(logDirectory, "summary.txt"), summary.ToString());
            report.Write(summary.ToString()); report.WriteLine("Smoke logs: " + logDirectory);
            return failed ? 1 : 0;
        }

        /// <summary>Starts one process, captures combined output and kills the whole process tree when it exceeds the timeout.</summary>
        internal static (int? Exit, string Output, bool TimedOut) LaunchProcess(string fileName, string arguments, TimeSpan timeout)
        {
            var output = new StringBuilder();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(fileName, arguments) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }
            };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            if (!process.WaitForExit(timeout))
            {
                process.Kill(entireProcessTree: true); process.WaitForExit();
                lock (output) return (null, output.ToString(), true);
            }
            process.WaitForExit();
            lock (output) return (process.ExitCode, output.ToString(), false);
        }
        #endregion END Public

        #region BEGIN private helpers
        private (string Status, string Detail) Execute(string group)
        {
            (int? Exit, string Output, bool TimedOut) result;
            try { result = launch(group, timeout); }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                File.WriteAllText(Path.Combine(logDirectory, group + ".log"), error.ToString());
                return ("BLOCKED", "could not start: " + error.Message);
            }
            File.WriteAllText(Path.Combine(logDirectory, group + ".log"), result.Output);
            if (result.TimedOut) return ("FAIL", "timed out after " + timeout + "; process tree killed");
            if (result.Exit is not 0) return ("FAIL", "exit code " + (result.Exit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"));
            return result.Output.Contains(PassMarker + group, StringComparison.Ordinal) ? ("PASS", "") : ("FAIL", "exited without a pass result");
        }
        #endregion END private helpers
    }
}
