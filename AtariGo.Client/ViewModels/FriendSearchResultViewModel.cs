namespace AtariGo.Client.ViewModels
{
    public class FriendSearchResultViewModel : ViewModelBase
    {
        private readonly string _username;
        private bool _isRequestSent;

        public FriendSearchResultViewModel(string username)
        {
            _username = username;
            _isRequestSent = false;
        }

        public string Username
        {
            get
            {
                return _username;
            }
        }

        public bool IsRequestSent
        {
            get
            {
                return _isRequestSent;
            }
            set
            {
                SetProperty(ref _isRequestSent, value);
            }
        }
    }
}