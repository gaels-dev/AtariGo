using System.Windows;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
            : this(new LoginViewModel(new NavigationService()))
        {
        }

        public LoginView(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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
