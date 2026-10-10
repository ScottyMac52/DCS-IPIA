namespace DcsIpia.Presentation.ViewModels
{
    public class SourcesViewModel : BaseViewModel
    {
        private string _title = "Sources";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
