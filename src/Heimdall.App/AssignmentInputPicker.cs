using System.Windows.Controls;
using System.Windows.Automation;

namespace Heimdall.App
{
    /// <summary>Shared input selector for assignment editors. Each editor supplies native values.</summary>
    public sealed class AssignmentInputPicker : ComboBox
    {
        public AssignmentInputPicker()
        {
            MaxDropDownHeight = 300;
            AutomationProperties.SetName(this, "Controller input");
        }
    }
}
