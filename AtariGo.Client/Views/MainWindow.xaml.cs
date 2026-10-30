using System.Windows;

using AtariGo.Client.Models;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() : this(new MainViewModel())
        {
        }

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => viewModel.Dispose();
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
                        SessionToken = oldVm.SessionToken
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
                Close();
            }
        }
    }
}
