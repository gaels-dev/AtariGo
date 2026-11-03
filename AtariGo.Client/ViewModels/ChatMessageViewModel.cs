using AtariGo.Client.Models;

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
            ChatMessageKind kind = ChatMessageKind.Player)
        {
            _sender = sender;
            _text = text;
            _isSystem = kind == ChatMessageKind.System;
            _isOpponent = kind == ChatMessageKind.Opponent;
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
