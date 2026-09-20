using System.Windows;

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
        }

        private void OnLanguageChanged()
        {
            if (DataContext is MainViewModel oldVm)
            {
                oldVm.LanguageChanged -= OnLanguageChanged;

                var newVm = new MainViewModel(oldVm.IsGuest, oldVm.PlayerName);
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
