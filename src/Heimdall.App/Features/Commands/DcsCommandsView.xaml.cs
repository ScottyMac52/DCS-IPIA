using System.Windows.Controls;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Features.Commands
{
    /// <summary>Hosts command management within the module workspace.</summary>
    public partial class DcsCommandsView : UserControl, IDisposable
    {
        #region BEGIN Public
        /// <summary>Releases its browser when the owning workspace closes.</summary>
        public void Dispose() { Manager.Dispose(); GC.SuppressFinalize(this); }

        public DcsCommandsView() => InitializeComponent();

        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IDcsModuleWorkspaceContext context) => Manager.Initialize(context);
        #endregion END Public
    }
}
