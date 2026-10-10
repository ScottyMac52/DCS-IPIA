namespace DcsIpia.Presentation.ViewModels
{
    public class CommandsViewModel : BaseViewModel
    {
        private string _title = "Commands";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
