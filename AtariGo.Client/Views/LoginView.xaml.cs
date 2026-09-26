using System.Windows;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();

            DataContext = new LoginViewModel(new NavigationService());
        }
    }
}
