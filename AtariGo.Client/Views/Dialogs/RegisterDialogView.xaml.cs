using System.Windows.Controls;

using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Views.Dialogs
{
    public partial class RegisterDialogView : UserControl
    {
        public RegisterDialogView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) => RestorePasswordFields();
        }

        private void OnRegisterButtonClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not RegisterDialogViewModel viewModel)
            {
                return;
            }

            viewModel.Password = PasswordBox.Password;
            viewModel.PasswordConfirmation = ConfirmPasswordBox.Password;

            if (viewModel.RegisterCommand.CanExecute(null))
            {
                viewModel.RegisterCommand.Execute(null);
            }
        }

        private void OnDataContextChanged(
            object sender,
            System.Windows.DependencyPropertyChangedEventArgs e)
        {
            RestorePasswordFields();
        }

        private void RestorePasswordFields()
        {
            if (DataContext is RegisterDialogViewModel viewModel)
            {
                PasswordBox.Password = viewModel.Password;
                ConfirmPasswordBox.Password = viewModel.PasswordConfirmation;
            }
        }
    }
}
