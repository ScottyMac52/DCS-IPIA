using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Defines common capability and state behavior independently of each native schema and WPF surface.</summary>
    public sealed class WorkspaceAssignmentActionStateTests
    {
        #region BEGIN Public
        /// <summary>Every mutating action shares the same open, read-only, busy and live-input rules.</summary>
        [Theory]
        [InlineData(WorkspaceAssignmentAction.Apply)]
        [InlineData(WorkspaceAssignmentAction.AddAlternative)]
        [InlineData(WorkspaceAssignmentAction.Clear)]
        [InlineData(WorkspaceAssignmentAction.TuneAxis)]
        [InlineData(WorkspaceAssignmentAction.ReverseAxis)]
        public void Mutation_gates_are_identical_for_all_operations(WorkspaceAssignmentAction action)
        {
            var ready = new WorkspaceAssignmentContext("scope", true, false, false, false);
            foreach (var context in new[] { ready with { Open = false }, ready with { ReadOnly = true },
                ready with { Busy = true }, ready with { Testing = true }, ready with { ReadOnly = true, Busy = true, Testing = true } })
            {
                var state = WorkspaceAssignmentActionState.For(action, context, new(true, true));
                Assert.True(state.Supported); Assert.False(state.Enabled); Assert.Equal(context.BlockedReason, state.Reason);
            }
            Assert.Equal(new WorkspaceAssignmentActionState(true, true, ""), WorkspaceAssignmentActionState.For(action, ready, new(true, true)));
        }
        /// <summary>Unsupported actions remain hidden even when a target exists and the workspace is editable.</summary>
        [Fact]
        public void Unsupported_actions_cannot_become_enabled()
        {
            foreach (var action in Enum.GetValues<WorkspaceAssignmentAction>())
            {
                var state = WorkspaceAssignmentActionState.For(action, new("scope", true, false, false, true), new(false, true));
                Assert.False(state.Supported); Assert.False(state.Enabled); Assert.NotEmpty(state.Reason);
            }
        }
        /// <summary>Supported operations on unbound, ambiguous or untranslated targets remain visible with native explanations.</summary>
        [Fact]
        public void Target_reasons_are_preserved_and_missing_reasons_have_a_shared_fallback()
        {
            var context = new WorkspaceAssignmentContext("scope", true, false, false, false);
            var native = WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.Clear, context, new(true, false, "Native record is ambiguous."));
            Assert.True(native.Supported); Assert.False(native.Enabled); Assert.Equal("Native record is ambiguous.", native.Reason);
            var fallback = WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.Clear, context, new(true, false));
            Assert.Equal("Select a compatible target for this action.", fallback.Reason);
            Assert.Equal(fallback, WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.Clear, context, new(true, false, null!)));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceAssignmentActionState.For((WorkspaceAssignmentAction)999, context, new(true, true)));
        }
        /// <summary>Read-only configuration does not prevent inspecting or fitting drawings during live input.</summary>
        [Theory]
        [InlineData(WorkspaceAssignmentAction.Inspect)]
        [InlineData(WorkspaceAssignmentAction.Fit)]
        public void Read_actions_remain_available_without_enabling_edits(WorkspaceAssignmentAction action)
        {
            var context = new WorkspaceAssignmentContext("scope", true, true, true, true);
            Assert.True(WorkspaceAssignmentActionState.For(action, context, new(true, true)).Enabled);
            Assert.False(WorkspaceAssignmentActionState.For(action, context with { Open = false }, new(true, true)).Enabled);
            Assert.False(WorkspaceAssignmentActionState.For(action, context, new(true, false)).Enabled);
        }
        /// <summary>Stop is always recoverable while sampling, including a busy or closed workspace.</summary>
        [Fact]
        public void Stop_has_a_recovery_path_independent_of_editing_state()
        {
            var context = new WorkspaceAssignmentContext("", false, true, true, true);
            Assert.True(WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StopInput, context, new(true, true)).Enabled);
            var stopped = WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StopInput, context with { Testing = false }, new(true, true));
            Assert.False(stopped.Enabled); Assert.Equal("Input testing is not running.", stopped.Reason);
            Assert.False(WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StopInput, context, new(true, false)).Enabled);
        }
        /// <summary>Starting requires current hardware evidence and idle acquisition, but not permission to edit a profile.</summary>
        [Fact]
        public void Start_requires_idle_state_and_a_ready_controller()
        {
            var context = new WorkspaceAssignmentContext("scope", true, true, false, false);
            Assert.True(WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StartInput, context, new(true, true)).Enabled);
            Assert.False(WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StartInput, context, new(true, false)).Enabled);
            var testing = WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StartInput, context with { Testing = true }, new(true, true));
            Assert.False(testing.Enabled); Assert.Equal("Input testing is already running.", testing.Reason);
            var busy = WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StartInput, context with { Busy = true }, new(true, true));
            Assert.False(busy.Enabled); Assert.Contains("Finish", busy.Reason, StringComparison.Ordinal);
            Assert.False(WorkspaceAssignmentActionState.For(WorkspaceAssignmentAction.StartInput, context with { Open = false }, new(true, true)).Enabled);
        }
        /// <summary>Immutable evidence and state retain all values through copies, comparisons and deconstruction.</summary>
        [Fact]
        public void Capability_value_contracts_retain_support_target_and_reason()
        {
            var support = new WorkspaceAssignmentActionSupport(true, false, "native reason"); var supportCopy = support with { };
            Assert.Equal(support, supportCopy); Assert.True(support == supportCopy); Assert.False(support != supportCopy);
            Assert.Equal(support.GetHashCode(), supportCopy.GetHashCode()); Assert.Equal(support.ToString(), supportCopy.ToString());
            var (supported, targetReady, reason) = support;
            Assert.Equal((true, false, "native reason"), (supported, targetReady, reason));
            Assert.NotEqual(support, support with { Reason = "other" });
            var state = new WorkspaceAssignmentActionState(true, false, reason); var stateCopy = state with { };
            Assert.Equal(state, stateCopy); Assert.True(state == stateCopy); Assert.False(state != stateCopy);
            Assert.Equal(state.GetHashCode(), stateCopy.GetHashCode()); Assert.Equal(state.ToString(), stateCopy.ToString());
            var (stateSupported, enabled, stateReason) = state;
            Assert.Equal((true, false, reason), (stateSupported, enabled, stateReason));
            Assert.NotEqual(state, state with { Enabled = true });
        }
        #endregion END Public
    }
}
