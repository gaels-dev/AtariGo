using System.Globalization;

using AtariGo.Client.Properties;

namespace AtariGo.Client.ViewModels
{
    public class FriendSearchResultViewModel : ViewModelBase
    {
        private readonly int _userId;
        private readonly string _username;
        private bool _isRequestSent;
        private string _actionLabel;

        public FriendSearchResultViewModel(int userId, string username)
        {
            _userId = userId;
            _username = username;
            _isRequestSent = false;
            _actionLabel = GetLocalizedMessage("FriendSearch_Btn_Add");
        }

        public int UserId => _userId;

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
                if (SetProperty(ref _isRequestSent, value))
                {
                    OnPropertyChanged(nameof(CanSendRequest));
                }
            }
        }

        public bool CanSendRequest => !IsRequestSent;

        public string ActionLabel
        {
            get => _actionLabel;
            private set => SetProperty(ref _actionLabel, value);
        }

        public void MarkRequestPending()
        {
            IsRequestSent = true;
            ActionLabel = GetLocalizedMessage(
                "FriendSearch_Btn_Pending");
        }

        public void MarkAlreadyFriend()
        {
            IsRequestSent = true;
            ActionLabel = GetLocalizedMessage(
                "FriendSearch_Btn_AlreadyFriends");
        }

        private static string GetLocalizedMessage(string key)
        {
            return Resources.ResourceManager.GetString(
                key,
                CultureInfo.CurrentUICulture)
                ?? key;
        }
    }
}
