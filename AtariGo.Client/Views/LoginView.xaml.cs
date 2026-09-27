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

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LoginViewModel viewModel)
            {
                return;
            }

            object password = TxtPassword.Password;
            if (viewModel.SubmitCommand.CanExecute(password))
            {
                viewModel.SubmitCommand.Execute(password);
            }
        }
    }
}
