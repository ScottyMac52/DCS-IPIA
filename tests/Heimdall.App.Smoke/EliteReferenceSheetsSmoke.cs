using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Heimdall.App.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Operates the dedicated reference workspace for illustrated and table-only controllers.</summary>
    public static class EliteReferenceSheetsSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies preview, formats, paging, section updates and immutable native state using actual WPF controls.</summary>
        public static void Verify(EliteWorkspaceWindow window, string root, bool artwork)
        {
            var before = JsonSerializer.Serialize(window.Session!.Snapshot);
            Box<ListBox>(window, "FeaturePicker").SelectedIndex = 3;
            Box<ComboBox>(window, "ReferenceLayoutPicker").SelectedIndex = artwork ? 0 : 1;
            ThemeControlSmoke.Pump(window);
            if (!Box<Grid>(window, "ReferenceSheetsPanel").IsVisible || Box<Grid>(window, "AuthoringPanel").IsVisible
                || Box<Image>(window, "ReferencePreview").Source is null || !Box<Button>(window, "ReferencePdfButton").IsEnabled)
                throw new InvalidOperationException("Reference Sheets did not open a dedicated printable preview.");
            if (!artwork && Box<ComboBoxItem>(window, "ReferenceArtworkOption").IsEnabled)
                throw new InvalidOperationException("An unillustrated controller offered an unavailable artwork layout.");
            var pdf = window.ReferencePdf();
            if (Encoding.ASCII.GetString(pdf, 0, 5) != "%PDF-") throw new InvalidOperationException("Reference export was not a PDF.");
            if (artwork)
            {
                if (!Box<Button>(window, "ReferenceNextButton").IsEnabled) throw new InvalidOperationException("The full assignment appendix was missing.");
                Click(window, "ReferenceNextButton");
                if (!Box<TextBlock>(window, "ReferencePageLabel").Text.StartsWith("Page 2", StringComparison.Ordinal))
                    throw new InvalidOperationException("Reference page navigation did not advance.");
                Click(window, "ReferencePreviousButton");
            }
            Box<ComboBox>(window, "ReferencePaperPicker").SelectedIndex = 1;
            Box<ComboBox>(window, "ReferenceOrientationPicker").SelectedIndex = 1;
            using (var data = new MemoryStream(window.ReferencePng()))
            {
                var decoder = BitmapDecoder.Create(data, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (decoder.Frames[0].PixelWidth != 1584 || decoder.Frames[0].PixelHeight != 1224)
                    throw new InvalidOperationException("Reference PNG did not match Letter landscape preview geometry.");
            }
            Box<CheckBox>(window, "ReferenceAssignedOnly").IsChecked = false;
            Box<CheckBox>(window, "ReferenceAssignedOnly").RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            Box<ListBox>(window, "SectionPicker").SelectedIndex = 3;
            if (Box<Image>(window, "ReferencePreview").Source is null) throw new InvalidOperationException("Empty section filters lost the printable preview.");
            Box<ListBox>(window, "SectionPicker").SelectedIndex = 0;
            Box<CheckBox>(window, "ReferenceAssignedOnly").IsChecked = true;
            Box<CheckBox>(window, "ReferenceAssignedOnly").RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            Box<ComboBox>(window, "ReferencePaperPicker").SelectedIndex = 0;
            Box<ComboBox>(window, "ReferenceOrientationPicker").SelectedIndex = 0;
            File.WriteAllBytes(Path.Combine(root, artwork ? "Artwork-reference.pdf" : "Table-reference.pdf"), window.ReferencePdf());
            File.WriteAllBytes(Path.Combine(root, artwork ? "Artwork-reference.png" : "Table-reference.png"), window.ReferencePng());
            if (JsonSerializer.Serialize(window.Session.Snapshot) != before) throw new InvalidOperationException("Reference preview options changed saved/controller/native state.");
            ThemeControlSmoke.Pump(window); ThemeControlSmoke.SaveImage(window, artwork ? "elite-reference-sheets" : "elite-reference-table");
            Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
