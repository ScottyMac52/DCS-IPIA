using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Supplies a mutable native capability to the production editing base.</summary>
    public sealed class AssignmentEditingProbe(IGameWorkspaceContext context) : GameAssignmentEditingBase(() => context)
    {
        #region BEGIN Public
        /// <summary>Gets or sets whether the native target remains editable.</summary>
        public bool Ready { get; set; } = true;
        /// <summary>Controls whether the operation is offered by the native adapter.</summary>
        public bool Supported { get; set; } = true;
        /// <summary>Gets the number of native editor refreshes.</summary>
        public int Refreshes { get; private set; }
        /// <summary>Gets the last result delivered to native presentation.</summary>
        public DraftResult? Feedback { get; private set; }
        #endregion END Public
        /// <inheritdoc/>
        protected override WorkspaceAssignmentActionSupport ReadNativeSupport(WorkspaceAssignmentAction action) => new(Supported, Ready);
        /// <inheritdoc/>
        protected override void RefreshNativeEditor() => Refreshes++;
        /// <inheritdoc/>
        protected override void ReportNativeResult(DraftResult result) => Feedback = result;
    }
}
