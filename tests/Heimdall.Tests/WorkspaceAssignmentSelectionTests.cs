using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects shared selection transitions used by all four workspaces.</summary>
    public sealed class WorkspaceAssignmentSelectionTests
    {
        #region BEGIN Public
        /// <summary>Changing workspace or section cannot inherit a command, native alternative or pending input.</summary>
        [Fact]
        public void Scope_transition_resets_the_complete_target()
        {
            var selection = Selected(); selection.SelectScope("profile:flight");
            Assert.Equal("button", selection.InputKey); Assert.Equal("record", selection.AlternativeId);
            selection.SelectScope("profile:menus");
            Assert.Equal("profile:menus", selection.Scope); Assert.Null(selection.CommandId);
            Assert.Null(selection.ControllerId); Assert.Null(selection.AlternativeId); Assert.Null(selection.InputKey);
            Assert.Equal("Primary", selection.Slot); Assert.Empty(selection.InputBlockedReason);
            selection.SelectCommand("new"); selection.SelectInput("new input"); selection.Reset();
            Assert.Empty(selection.Scope); Assert.Null(selection.CommandId); Assert.Null(selection.AlternativeId); Assert.Null(selection.InputKey);
        }
        /// <summary>A controller change preserves the command while invalidating its native record and input.</summary>
        [Fact]
        public void Controller_transition_cannot_reuse_the_previous_native_target()
        {
            var selection = Selected(); selection.SelectController("controller"); Assert.Equal("button", selection.InputKey);
            selection.SelectController("other"); Assert.Equal("command", selection.CommandId);
            Assert.Null(selection.AlternativeId); Assert.Null(selection.InputKey);
            selection.SelectAlternative("new record"); selection.SelectInput("axis");
            selection.SelectAlternative("new record"); Assert.Equal("axis", selection.InputKey);
            selection.SelectAlternative("other record"); Assert.Null(selection.InputKey);
        }
        /// <summary>Untranslated drawing controls block stale input until an explicit new target or context is selected.</summary>
        [Fact]
        public void Failed_input_translation_does_not_leave_an_old_actionable_input()
        {
            var selection = Selected(); selection.BlockInput("No native mapping.");
            Assert.Null(selection.InputKey); Assert.Equal("No native mapping.", selection.InputBlockedReason);
            selection.SelectInput("verified axis"); Assert.Equal("verified axis", selection.InputKey); Assert.Empty(selection.InputBlockedReason);
            selection.BlockInput("blocked"); selection.SelectCommand("other");
            Assert.Null(selection.AlternativeId); Assert.Null(selection.InputKey); Assert.Empty(selection.InputBlockedReason);
            selection.SelectInput("axis"); selection.SelectSlot("Binding"); Assert.Null(selection.InputKey);
            selection.BlockInput("blocked"); selection.SelectScope("another workspace"); Assert.Empty(selection.InputBlockedReason);
        }
        /// <summary>Empty or whitespace input text cannot become a pending assignment target.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Empty_input_is_an_explicit_clear(string? input)
        {
            var selection = Selected(); selection.SelectInput(input); Assert.Null(selection.InputKey);
            selection.SelectCommand(null); selection.SelectInput("must not survive"); Assert.Null(selection.InputKey);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameAssignmentSelection Selected()
        {
            var selection = new GameAssignmentSelection(); selection.SelectScope("profile:flight");
            selection.SelectController("controller"); selection.SelectCommand("command");
            selection.SelectAlternative("record"); selection.SelectInput("button"); return selection;
        }
        #endregion END private helpers
    }
}
