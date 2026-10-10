namespace DcsIpia.Presentation.ViewModels
{
    public class ModifiersViewModel : BaseViewModel
    {
        private string _title = "Modifiers";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
