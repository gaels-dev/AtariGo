using System;
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
            Closed += OnClosed;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            if (DataContext is not LoginViewModel viewModel)
            {
                return;
            }

            // A language refresh transfers the same view model to a replacement window.
            bool isTransferred = Application.Current.MainWindow is LoginView replacement
                && !ReferenceEquals(replacement, this)
                && ReferenceEquals(replacement.DataContext, viewModel);
            if (!isTransferred)
            {
                viewModel.Dispose();
            }
        }

        private void OnSubmitButtonClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LoginViewModel viewModel)
            {
                return;
            }

            object password = PasswordBox.Password;
            if (viewModel.SubmitCommand.CanExecute(password))
            {
                viewModel.SubmitCommand.Execute(password);
            }
        }
    }
}
