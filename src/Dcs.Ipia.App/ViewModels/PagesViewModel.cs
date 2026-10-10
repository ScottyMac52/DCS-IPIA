namespace DcsIpia.Presentation.ViewModels
{
    public class PagesViewModel : BaseViewModel
    {
        private string _title = "Pages";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
