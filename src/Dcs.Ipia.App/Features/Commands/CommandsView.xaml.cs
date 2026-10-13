using System.Windows.Controls;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Features.Commands
{
    /// <summary>Hosts command management within the module workspace.</summary>
    public partial class CommandsView : UserControl
    {
        #region BEGIN Public
        public CommandsView() => InitializeComponent();

        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IModuleWorkspaceContext context) => Manager.Initialize(context);
        #endregion END Public
    }
}
