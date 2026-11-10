using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Aggregation, exit, timeout and missing-result semantics of the isolated smoke group runner.</summary>
    public sealed class SmokeGroupRunnerTests : IDisposable
    {
        private readonly string _logs = Path.Combine(Path.GetTempPath(), "heimdall-runner-" + Guid.NewGuid().ToString("N"));
        private static readonly string[] Groups = ["one", "two", "three"];

        #region BEGIN Public
        /// <summary>Every group passing with its marker is the only zero exit; logs and summary are written.</summary>
        [Fact]
        public void All_groups_passing_returns_zero_and_writes_logs()
        {
            var (code, text) = Run((group, _) => (0, SmokeGroupRunner.PassMarker + group, false));
            Assert.Equal(0, code); Assert.Contains("RESULT: PASS", text, StringComparison.Ordinal);
            Assert.All(Groups, group => Assert.True(File.Exists(Path.Combine(_logs, group + ".log"))));
            Assert.Contains("PASS    three", File.ReadAllText(Path.Combine(_logs, "summary.txt")), StringComparison.Ordinal);
        }

        /// <summary>A failing, timed-out, unstartable or result-less group fails the run, and later groups still run.</summary>
        [Fact]
        public void Independent_failures_are_all_collected_and_fail_the_run()
        {
            var launched = new List<string>();
            var (code, text) = Run((group, _) =>
            {
                launched.Add(group);
                return group switch
                {
                    "one" => (3, "boom", false),
                    "two" => throw new FileNotFoundException("missing exe"),
                    _ => (null, "hung", true)
                };
            });
            Assert.Equal(1, code); Assert.Equal(Groups, launched);
            Assert.Contains("FAIL    one exit code 3", text, StringComparison.Ordinal);
            Assert.Contains("boom", text, StringComparison.Ordinal);
            Assert.Contains("BLOCKED two could not start: missing exe", text, StringComparison.Ordinal);
            Assert.Contains("FAIL    three timed out", text, StringComparison.Ordinal);
            Assert.Contains("RESULT: FAIL", text, StringComparison.Ordinal);
        }

        /// <summary>A zero exit without the group's own pass marker (or another group's marker) is not a pass.</summary>
        [Fact]
        public void Zero_exit_without_the_groups_marker_is_a_failure()
        {
            var (code, text) = Run((group, _) => (0, group == "two" ? SmokeGroupRunner.PassMarker + "one" : SmokeGroupRunner.PassMarker + group, false));
            Assert.Equal(1, code); Assert.Contains("FAIL    two exited without a pass result", text, StringComparison.Ordinal);
        }

        /// <summary>An explicit subset reports the others NOTRUN, is never full acceptance and always exits nonzero.</summary>
        [Fact]
        public void Subset_run_is_never_full_acceptance()
        {
            var (code, text) = Run((group, _) => (0, SmokeGroupRunner.PassMarker + group, false), ["two"]);
            Assert.Equal(1, code);
            Assert.Contains("NOTRUN  one not selected", text, StringComparison.Ordinal); Assert.Contains("PASS    two", text, StringComparison.Ordinal);
            Assert.Contains("SUBSET RUN: NOT FULL ACCEPTANCE", text, StringComparison.Ordinal);
            Assert.Equal(2, Run((_, _) => throw new InvalidOperationException(), ["bogus"]).Code);
        }

        /// <summary>The real launcher captures exit code and output, and kills a process that exceeds its timeout.</summary>
        [Fact]
        public void Process_launcher_reports_exit_output_and_kills_on_timeout()
        {
            if (OperatingSystem.IsWindows()) return; // Portable POSIX shell fixture; Windows exercises the launcher through the smoke itself.
            var done = SmokeGroupRunner.LaunchProcess("/bin/sh", "-c \"echo hello; echo oops 1>&2; exit 3\"", TimeSpan.FromSeconds(30));
            Assert.Equal((3, false), (done.Exit, done.TimedOut));
            Assert.Contains("hello", done.Output, StringComparison.Ordinal); Assert.Contains("oops", done.Output, StringComparison.Ordinal);
            var started = DateTime.UtcNow;
            var hung = SmokeGroupRunner.LaunchProcess("/bin/sh", "-c \"sleep 30\"", TimeSpan.FromMilliseconds(300));
            Assert.True(hung.TimedOut); Assert.Null(hung.Exit); Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(20));
        }

        /// <inheritdoc/>
        public void Dispose() { if (Directory.Exists(_logs)) Directory.Delete(_logs, true); }
        #endregion END Public

        #region BEGIN private helpers
        private (int Code, string Text) Run(Func<string, TimeSpan, (int? Exit, string Output, bool TimedOut)> launch, string[]? only = null)
        {
            using var report = new StringWriter();
            var code = new SmokeGroupRunner(Groups, launch, TimeSpan.FromMinutes(1), _logs).Run(only, report);
            return (code, report.ToString());
        }
        #endregion END private helpers
    }
}
