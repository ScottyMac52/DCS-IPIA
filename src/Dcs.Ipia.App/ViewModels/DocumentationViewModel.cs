namespace DcsIpia.Presentation.ViewModels
{
    public class DocumentationViewModel : BaseViewModel
    {
        private string _title = "Documentation";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
