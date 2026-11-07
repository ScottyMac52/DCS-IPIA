using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.Rendering;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Owns ED reference projection, preview cache and PDF/PNG export.</summary>
    internal sealed class EliteReferencePresenter(
        EliteWorkspaceContext context,
        Window owner,
        EliteReferenceSheetsView _referenceView,
        Func<WorkspaceHardware, KneeboardComposite, string, byte[]> RenderDrawing,
        Action<Action> Run,
        Action<string> Status)
    {
        private ReferenceSheetDocument? _referenceSheets;
        private bool _referenceDirty = true;
        private bool _renderingReference;
        private int _referencePage;
        private GameProfileSession? _session => context.Session;
        private bool _refreshing { get => context.Refreshing; set => context.Refreshing = value; }
        private IReadOnlyList<EliteAssignmentRow> _rows => context.Rows;
        private GameDeviceChoice? Device => context.Device;
        private string Section => context.Section;
        #region BEGIN Public
        internal void ExportReference(string path)
        {
            if (_session is null || Device is null) throw new InvalidOperationException("Select a profile and associated device first.");
            _session.Export();
            var document = _session.Snapshot;
            var hardware = context.DrawingFor(document, Device.Id) ?? throw new InvalidOperationException("Choose an optional drawing before exporting an illustrated reference sheet.");
            var page = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, _rows.Where(x => x.DeviceId == Device.Id).ToArray(), Section);
            File.WriteAllBytes(path, RenderDrawing(hardware, page, "Elite Dangerous · " + document.Name));
        }

        internal byte[] ReferencePdf()
        {
            _session?.Export(); RefreshReferenceSheets();
            return _referenceSheets?.Pdf ?? throw new InvalidOperationException("Select a valid controller profile and reference layout first.");
        }

        internal byte[] ReferencePng()
        {
            _session?.Export(); RefreshReferenceSheets();
            return _referenceSheets?.PngPages[_referencePage] ?? throw new InvalidOperationException("Select a valid controller profile and reference layout first.");
        }

        internal void ExportSheetClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Device is null) return;
            var dialog = new SaveFileDialog { Filter = "PNG reference sheet (*.png)|*.png", FileName = "Elite-controller-reference.png" };
            if (dialog.ShowDialog(owner) == true) { ExportReference(dialog.FileName); Status("Reference sheet exported."); }
        });

        internal void InvalidateReferenceSheets()
        { _referenceDirty = true; if (context.Feature == 2) RefreshReferenceSheets(); }

        internal void ReferenceOptionsChanged(object sender, SelectionChangedEventArgs e)
        { if (!_refreshing && !_renderingReference) InvalidateReferenceSheets(); }

        internal void ReferenceAssignedChanged(object sender, RoutedEventArgs e) => InvalidateReferenceSheets();

        internal void RefreshReferenceSheets()
        {
            if (_renderingReference || !_referenceDirty) return;
            _renderingReference = true; _referenceSheets = null; _referencePage = 0;
            try
            {
                if (_session is null || Device is null) throw new InvalidOperationException("Import a controls profile and choose a controller on the left.");
                _session.Export();
                var document = _session.Snapshot;
                var hardware = context.DrawingFor(document, Device.Id);
                _referenceView.ReferenceArtworkOption.IsEnabled = hardware is not null;
                if (hardware is null) _referenceView.ReferenceLayoutPicker.SelectedIndex = 1;
                var content = EliteReferenceSheetBuilder.Build(document.Profile, _session.Catalog, Device.Id, Device.Name, Section, _referenceView.ReferenceAssignedOnly.IsChecked == true)
                    with { Title = "Elite Dangerous / " + (string.IsNullOrWhiteSpace(context.EditedName) ? document.Name : context.EditedName.Trim()) };
                var options = new ReferenceSheetOptions(_referenceView.ReferencePaperPicker.SelectedIndex == 1 ? "Letter" : "A4",
                    _referenceView.ReferenceOrientationPicker.SelectedIndex == 1, _referenceView.ReferenceLayoutPicker.SelectedIndex == 0);
                byte[]? artwork = null;
                if (options.Artwork && hardware is not null)
                {
                    var projection = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, _rows.Where(x => x.DeviceId == Device.Id).ToArray(), Section);
                    artwork = RenderDrawing(hardware, projection, "Elite Dangerous / " + document.Name);
                }
                _referenceSheets = ReferenceSheetRenderer.Render(content, options, artwork);
                _referenceView.ReferenceStatus.Text = content.Rows.Count + " entries · current profile preview" + (options.Artwork ? " · artwork followed by the complete assignment table" : " · assignment table");
            }
            catch (Exception error) { _referenceView.ReferenceStatus.Text = error.Message; }
            finally { _referenceDirty = false; _renderingReference = false; ShowReferencePage(); }
        }

        internal void ReferencePreviousClicked(object sender, RoutedEventArgs e)
        { if (_referencePage > 0) { _referencePage--; ShowReferencePage(); } }

        internal void ReferenceNextClicked(object sender, RoutedEventArgs e)
        { if (_referenceSheets is not null && _referencePage + 1 < _referenceSheets.PageCount) { _referencePage++; ShowReferencePage(); } }

        internal void ReferencePdfClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var data = ReferencePdf();
            var dialog = new SaveFileDialog { Filter = "PDF reference (*.pdf)|*.pdf", FileName = "Elite-controller-reference.pdf" };
            if (dialog.ShowDialog(owner) == true) { File.WriteAllBytes(dialog.FileName, data); Status("Reference PDF exported."); }
        });

        internal void ReferencePngClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var data = ReferencePng();
            var dialog = new SaveFileDialog { Filter = "PNG reference page (*.png)|*.png", FileName = "Elite-reference-page-" + (_referencePage + 1) + ".png" };
            if (dialog.ShowDialog(owner) == true) { File.WriteAllBytes(dialog.FileName, data); Status("Current reference page exported."); }
        });
        internal void MarkDirty() => _referenceDirty = true;
        internal void Clear()
        {
            _referenceSheets = null; _referenceDirty = true;
            _referenceView.ReferencePreview.Source = null; _referenceView.ReferencePageLabel.Text = "No preview";
        }
        #endregion END Public
        #region BEGIN private helpers
        private void ShowReferencePage()
        {
            var ready = _referenceSheets is not null;
            _referenceView.ReferencePdfButton.IsEnabled = _referenceView.ReferencePngButton.IsEnabled = ready;
            _referenceView.ReferencePreviousButton.IsEnabled = ready && _referencePage > 0;
            _referenceView.ReferenceNextButton.IsEnabled = ready && _referencePage + 1 < _referenceSheets!.PageCount;
            _referenceView.ReferencePageLabel.Text = ready ? "Page " + (_referencePage + 1) + " of " + _referenceSheets!.PageCount : "No preview";
            _referenceView.ReferencePreview.Source = null;
            if (!ready) return;
            using var stream = new MemoryStream(_referenceSheets!.PngPages[_referencePage]);
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit(); image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
            _referenceView.ReferencePreview.Source = image;
        }

        #endregion END private helpers
    }
}
