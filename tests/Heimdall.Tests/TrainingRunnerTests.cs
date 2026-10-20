using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises playback boundaries independently of WPF and wall-clock pacing.</summary>
    public sealed class TrainingRunnerTests
    {
        #region BEGIN Public
        /// <summary>Single-step playback executes once and remains paused at its explanation.</summary>
        [Fact]
        public async Task Next_executes_one_step_and_completion_does_not_repeat()
        {
            var calls = 0;
            await using var runner = new TrainingRunner(Scenario(false), _ => { calls++; return Task.CompletedTask; });
            await runner.NextAsync();
            Assert.Equal(1, calls);
            Assert.Equal(TrainingState.Paused, runner.State);
            Assert.Equal(1, runner.CompletedSteps);
            await runner.NextAsync();
            Assert.Equal(TrainingState.Completed, runner.State);
            await runner.PlayAsync();
            await runner.NextAsync();
            Assert.Equal(2, calls);
        }

        /// <summary>Pause and duplicate clicks cannot interrupt or duplicate an in-flight action.</summary>
        [Fact]
        public async Task Pause_settles_atomic_action_before_next_step()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            await using var runner = new TrainingRunner(Scenario(false), async _ => { calls++; await gate.Task; });
            var playing = runner.PlayAsync();
            runner.Pause();
            await runner.NextAsync();
            await runner.PlayAsync();
            Assert.Equal(1, calls);
            gate.SetResult();
            await playing;
            Assert.Equal(TrainingState.Paused, runner.State);
            Assert.Equal(1, runner.CompletedSteps);
        }

        /// <summary>Reading checkpoints pause automatic playback and retain the completed step.</summary>
        [Fact]
        public async Task Checkpoints_and_stop_preserve_action_boundaries()
        {
            await using var runner = new TrainingRunner(Scenario(true), _ => Task.CompletedTask);
            await runner.PlayAsync();
            Assert.Equal(TrainingState.Paused, runner.State);
            Assert.Equal("one", runner.CurrentStep!.Id);
            await runner.StopAsync();
            Assert.Equal(TrainingState.Stopped, runner.State);
            await runner.PlayAsync();
            Assert.Equal(1, runner.CompletedSteps);
            await runner.RestartAsync(() => Task.CompletedTask);
            Assert.Equal(TrainingState.Ready, runner.State);
            Assert.Equal(0, runner.CompletedSteps);
            await runner.PlayAsync();
            Assert.Equal(1, runner.CompletedSteps);
        }

        /// <summary>Failures stop subsequent steps and can only be retried through restart.</summary>
        [Fact]
        public async Task Failure_is_actionable_and_restart_resets_it()
        {
            var calls = 0;
            await using var runner = new TrainingRunner(Scenario(false), _ => { calls++; throw new InvalidOperationException("Missing target"); });
            await runner.PlayAsync();
            Assert.Equal(TrainingState.Failed, runner.State);
            Assert.Contains("one", runner.Error);
            Assert.Contains("Missing target", runner.Error);
            await runner.NextAsync();
            Assert.Equal(1, calls);
            await runner.RestartAsync(() => throw new InvalidOperationException("Bad fixture"));
            Assert.Equal(TrainingState.Failed, runner.State);
            Assert.Contains("Bad fixture", runner.Error);
            await runner.RestartAsync(() => Task.CompletedTask);
            Assert.Null(runner.Error);
            Assert.Equal(TrainingState.Ready, runner.State);
        }

        /// <summary>Pause cancels only pacing; stop waits for active operations before cleanup.</summary>
        [Fact]
        public async Task Stop_waits_and_pause_cancels_pacing()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var runner = new TrainingRunner(Scenario(false), _ => gate.Task);
            var playing = runner.PlayAsync();
            var stopped = runner.StopAsync();
            Assert.False(stopped.IsCompleted);
            gate.SetResult();
            await Task.WhenAll(playing, stopped);
            Assert.Equal(1, runner.CompletedSteps);
            await runner.RestartAsync(() => Task.CompletedTask);
            var pacing = runner.PlayAsync();
            runner.Pause();
            await pacing;
            Assert.Equal(TrainingState.Paused, runner.State);
            Assert.Equal(1, runner.CompletedSteps);
            runner.Speed = 2;
            Assert.Equal(2, runner.Speed);
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Speed = 0);
        }

        /// <summary>Unknown actions, invalid schemas and duplicate step identities fail before playback.</summary>
        [Theory]
        [InlineData("{}")]
        [InlineData("{\"SchemaVersion\":2,\"Id\":\"x\",\"Title\":\"X\",\"Steps\":[]}")]
        [InlineData("{\"SchemaVersion\":1,\"Id\":\"x\",\"Title\":\"X\",\"Steps\":[{\"Id\":\"a\",\"Action\":\"exec\",\"Title\":\"A\",\"Explanation\":\"B\",\"Target\":\"X\"}]}")]
        [InlineData("null")]
        public void Invalid_scenarios_fail_validation(string json)
        {
            Assert.ThrowsAny<Exception>(() => TrainingScenario.Read(json, new HashSet<string>(StringComparer.Ordinal) { "one" }));
        }

        /// <summary>Versioned data round-trips and duplicate/missing step metadata is rejected.</summary>
        [Fact]
        public void Scenario_validates_all_steps()
        {
            var actions = new HashSet<string>(StringComparer.Ordinal) { "one", "two" };
            var scenario = Scenario(false);
            var json = System.Text.Json.JsonSerializer.Serialize(scenario);
            Assert.Equal("demo", TrainingScenario.Read(json, actions).Id);
            Assert.Throws<ArgumentException>(() => new TrainingRunner(scenario with { Steps = [] }, _ => Task.CompletedTask));
            Assert.Throws<ArgumentException>(() => TrainingScenario.Read(System.Text.Json.JsonSerializer.Serialize(scenario with
            { Steps = [scenario.Steps[0], scenario.Steps[0]] }), actions));
            Assert.Throws<ArgumentException>(() => TrainingScenario.Read(System.Text.Json.JsonSerializer.Serialize(scenario with
            { Steps = [scenario.Steps[0] with { Explanation = "" }] }), actions));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static TrainingScenario Scenario(bool checkpoint) => new(1, "demo", "Demo",
            [new("one", "one", "First", "Read first", "First", checkpoint), new("two", "two", "Second", "Read second", "Second", false)]);
        #endregion END private helpers
    }
}
