namespace AtariGo.Client.ViewModels
{
    public class FriendItemViewModel : ViewModelBase
    {
        private readonly int _userId;
        private string _username;
        private bool _isOnline;

        public FriendItemViewModel(
            string username,
            bool isOnline)
            : this(0, username, isOnline)
        {
        }

        public FriendItemViewModel(
            int userId,
            string username,
            bool isOnline)
        {
            _userId = userId;
            _username = username;
            _isOnline = isOnline;
        }

        public int UserId => _userId;

        public string Username
        {
            get
            {
                return _username;
            }
            set
            {
                SetProperty(ref _username, value);
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
