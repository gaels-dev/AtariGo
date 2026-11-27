namespace AtariGo.Client.ViewModels
{
    public class FriendItemViewModel : ViewModelBase
    {
        private readonly int _userId;
        private string _userName;
        private bool _isOnline;

        public FriendItemViewModel(
            string userName,
            bool isOnline)
            : this(0, userName, isOnline)
        {
        }

        public FriendItemViewModel(
            int userId,
            string userName,
            bool isOnline)
        {
            _userId = userId;
            _userName = userName;
            _isOnline = isOnline;
        }

        public int UserId => _userId;

        public string UserName
        {
            get
            {
                return _userName;
            }
            set
            {
                SetProperty(ref _userName, value);
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
