using System.Windows.Controls;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Features.Commands
{
    public partial class CommandsView : UserControl
    {
        public CommandsView() => InitializeComponent();

        public void Initialize(IModuleWorkspaceContext context) => Manager.Initialize(context);
    }
}