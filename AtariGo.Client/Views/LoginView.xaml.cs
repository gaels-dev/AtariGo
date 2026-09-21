using AtariGo.Client.ViewModels;
using System.Windows;

namespace AtariGo.Client.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();

            DataContext = new LoginViewModel(
                onLoginSuccess: (isGuest, playerName) =>
                {
                    var viewModel = new MainViewModel(isGuest: isGuest, playerName: playerName);
                    var mainWindow = new MainWindow(viewModel);
                    mainWindow.Show();
                    this.Close();
                },
                onExit: () => this.Close(),
                onRefreshWindow: () =>
                {
                    var newLogin = new LoginView();
                    newLogin.Show();
                    this.Close();
                }
            );
        }
    }
}