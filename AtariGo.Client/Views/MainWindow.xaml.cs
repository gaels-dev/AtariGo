using System.ComponentModel;
using System.Windows;

using AtariGo.Client.Models;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Views
{
    public partial class MainWindow : Window
    {
        private bool _allowClose;
        private bool _closePending;

        public MainWindow() : this(new MainViewModel())
        {
        }

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.LanguageChanged += OnLanguageChanged;
            Closing += OnClosing;
            Closed += OnClosed;
        }

        private void OnLanguageChanged()
        {
            if (DataContext is MainViewModel oldVm)
            {
                oldVm.LanguageChanged -= OnLanguageChanged;
                oldVm.Dispose();

                var newVm = new MainViewModel(
                    new ClientSessionContext
                    {
                        IsGuest = oldVm.IsGuest,
                        PlayerName = oldVm.PlayerName,
                        SessionToken = oldVm.SessionToken,
                        GuestCredential = oldVm.GuestCredential
                    });
                var newWindow = new MainWindow(newVm)
                {
                    Left = Left,
                    Top = Top,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState
                };

                Application.Current.MainWindow = newWindow;
                newWindow.Show();
                _allowClose = true;
                Close();
            }
        }

        private async void OnClosing(object? sender, CancelEventArgs eventArgs)
        {
            if (_allowClose)
            {
                return;
            }

            eventArgs.Cancel = true;
            if (_closePending)
            {
                return;
            }

            _closePending = true;
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Dispose();
                await viewModel.RevokeGuestIdentityAsync();
            }

            _allowClose = true;
            await Dispatcher.InvokeAsync(new Action(Close));
        }

        private void OnClosed(object? sender, EventArgs eventArgs)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Dispose();
            }
        }
    }
}
