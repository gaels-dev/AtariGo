namespace AtariGo.Client.ViewModels
{
    public class FriendItemViewModel : ViewModelBase
    {
        private readonly string _username;
        private bool _isOnline;

        public FriendItemViewModel(
            string username,
            bool isOnline)
        {
            _username = username;
            _isOnline = isOnline;
        }

        public string Username
        {
            get
            {
                return _username;
            }
        }

        public bool IsOnline
        {
            get
            {
                return _isOnline;
            }
            set
            {
                SetProperty(ref _isOnline, value);
            }
        }
    }
}