using Heimdall.Application.Interfaces;
using Heimdall.App.Features.Assignments;
using Heimdall.App.Features.Commands;
using Heimdall.App.Features.Devices;
using Heimdall.App.Features.Documentation;
using Heimdall.App.Features.InputTest;
using Heimdall.App.Features.Kneeboards;
using Heimdall.App.Features.Modifiers;
using Heimdall.App.Features.Pages;
using System.Windows.Controls;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>Registers DCS feature factories and capability policies without introducing game branches in the base window.</summary>
    internal static class DcsWorkspaceFeatures
    {
        #region BEGIN Public
        internal static IReadOnlyList<WorkspaceFeature> Create(IDcsModuleWorkspaceContext context, bool persistLayout) =>
        [
            new("Devices", () => Prepare(new DcsDevicesView(), view => view.Initialize(context), persistLayout)),
            new("Modifiers", () => Prepare(new DcsModifiersView(), view => view.Initialize(context), persistLayout)),
            new("Assignments", () => Prepare(new DcsAssignmentsView(), view => view.Initialize(context), persistLayout)),
            new("InputTest", () => Prepare(new DcsInputTestView(), view => view.Initialize(context), persistLayout),
                () => EditionAccess.AllowsInputTest(context) ? null : "Live input testing is reserved for Premium. Availability and pricing TBA."),
            new("Commands", () => Prepare(new DcsCommandsView(), view => view.Initialize(context), persistLayout)),
            new("Pages", () => Prepare(new DcsPagesView(), view => view.Initialize(context), persistLayout)),
            new("Kneeboards", () => Prepare(new DcsKneeboardsView(), view => view.Initialize(context), persistLayout)),
            new("Documentation", () => Prepare(new DcsDocumentationView(), view => view.Initialize(context), persistLayout))
        ];
        #endregion END Public

        #region BEGIN private helpers
        private static T Prepare<T>(T view, Action<T> initialize, bool persistLayout) where T : UserControl
        {
            initialize(view);
            WorkspaceLayout.SetPersistenceEnabled(view, persistLayout);
            return view;
        }
        #endregion END private helpers
    }
}
