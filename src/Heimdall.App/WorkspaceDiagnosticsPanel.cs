using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

namespace Heimdall.App
{
    /// <summary>Standard expandable area for import results and workspace diagnostics.</summary>
    public sealed class WorkspaceDiagnosticsPanel : Expander
    {
        public WorkspaceDiagnosticsPanel()
        {
            SetResourceReference(StyleProperty, typeof(Expander));
            Header = "Import results and diagnostics";
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Margin = new Thickness(0, 8, 0, 0);
            IsExpanded = false;
            AutomationProperties.SetName(this, "Import results and diagnostics");
        }
    }
}
