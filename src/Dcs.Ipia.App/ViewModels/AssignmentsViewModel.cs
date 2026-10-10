namespace DcsIpia.Presentation.ViewModels
{
    public class AssignmentsViewModel : BaseViewModel
    {
        private string _title = "Assignments";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
