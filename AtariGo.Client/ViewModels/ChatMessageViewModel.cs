namespace AtariGo.Client.ViewModels
{
    public class ChatMessageViewModel : ViewModelBase
    {
        private string _sender;
        private string _text;
        private bool _isSystem;
        private bool _isOpponent;

        public ChatMessageViewModel(
            string sender,
            string text,
            bool isSystem = false,
            bool isOpponent = false)
        {
            _sender = sender;
            _text = text;
            _isSystem = isSystem;
            _isOpponent = isOpponent;
        }

        public string Sender
        {
            get => _sender;
            set => SetProperty(ref _sender, value);
        }

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public bool IsSystem
        {
            get => _isSystem;
            set => SetProperty(ref _isSystem, value);
        }

        public bool IsOpponent
        {
            get => _isOpponent;
            set => SetProperty(ref _isOpponent, value);
        }
    }
}
