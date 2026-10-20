using System.Windows;

namespace Heimdall.App
{
    /// <summary>Configures the shared, skinned file and folder picker used by all application browse actions.</summary>
    public sealed class ThemeBrowseDialog
    {
        #region BEGIN Public
        /// <summary>Gets or sets the dialog caption.</summary>
        public string Title { get; set; } = "Browse";
        /// <summary>Gets or sets whether a new output file is being chosen.</summary>
        public bool Save { get; set; }
        /// <summary>Gets or sets whether the result is a directory.</summary>
        public bool SelectFolder { get; set; }
        /// <summary>Gets or sets the initial directory.</summary>
        public string InitialDirectory { get; set; } = "";
        /// <summary>Gets or sets the selected or suggested file path.</summary>
        public string FileName { get; set; } = "";
        /// <summary>Gets the accepted file paths for a multiple-file import.</summary>
        public string[] FileNames { get; internal set; } = [];
        /// <summary>Gets the accepted folder path.</summary>
        public string FolderName { get; internal set; } = "";
        /// <summary>Gets or sets the familiar label|pattern pairs used to filter files.</summary>
        public string Filter { get; set; } = "All files|*.*";
        /// <summary>Gets or sets whether opening multiple files is permitted.</summary>
        public bool Multiselect { get; set; }
        /// <summary>Gets or sets whether an existing file is required when opening.</summary>
        public bool CheckFileExists { get; set; } = true;
        /// <summary>Gets or sets whether saving over an existing file requires confirmation.</summary>
        public bool OverwritePrompt { get; set; } = true;
        /// <summary>Displays the shared picker without changing files or accepting a result on cancellation.</summary>
        public bool? ShowDialog(Window? owner = null)
        {
            var window = new BrowseWindow(this);
            if (owner?.IsVisible == true) window.Owner = owner;
            return window.ShowDialog();
        }
        #endregion END Public
    }
}
