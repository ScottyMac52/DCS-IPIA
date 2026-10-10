namespace DcsIpia.Presentation.ViewModels
{
    public class DevicesViewModel : BaseViewModel
    {
        private string _title = "Devices";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
