using System;
using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels
{
    public abstract class DialogViewModelBase : ViewModelBase
    {
        private string _title = string.Empty;

        public DialogViewModelBase(string title)
        {
            _title = title;
            CloseCommand = new RelayCommand(() => Close(false));
        }

        public event Action<bool?>? DialogClosed;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ICommand CloseCommand { get; }

        public void Close(bool? result = null)
        {
            DialogClosed?.Invoke(result);
        }
    }
}
