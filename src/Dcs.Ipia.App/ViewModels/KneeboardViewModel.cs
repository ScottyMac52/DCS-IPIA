namespace DcsIpia.Presentation.ViewModels
{
    public class KneeboardViewModel : BaseViewModel
    {
        private string _title = "Kneeboard";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
