namespace DcsIpia.Presentation.ViewModels
{
    public class DiagnosticsViewModel : BaseViewModel
    {
        private string _title = "Diagnostics";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
