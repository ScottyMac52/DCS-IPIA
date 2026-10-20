using System.Windows;

namespace Heimdall.App
{
    /// <summary>Gives programmatically constructed dialogs a stable appearance identity.</summary>
    public sealed class AppearanceDialog : Window
    {
        /// <summary>Creates a dialog belonging to a documented appearance target.</summary>
        public AppearanceDialog(string target) => AppearanceManager.SetTarget(this, target);
    }
}
