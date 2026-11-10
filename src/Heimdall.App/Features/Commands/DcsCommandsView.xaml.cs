using System.Windows.Controls;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Features.Commands
{
    /// <summary>Hosts command management within the module workspace.</summary>
    public partial class DcsCommandsView : UserControl, IDisposable, IGameWorkspaceFeatureLifetime
    {
        #region BEGIN Public
        /// <summary>Releases its browser when the owning workspace closes.</summary>
        public void Dispose() { Manager.Dispose(); GC.SuppressFinalize(this); }

        public DcsCommandsView() => InitializeComponent();

        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IDcsModuleWorkspaceContext context) => Manager.Initialize(context);
        void IGameWorkspaceFeatureLifetime.SetFeatureActive(bool active) => Manager.SetFeatureActive(active);
        #endregion END Public
    }
}


