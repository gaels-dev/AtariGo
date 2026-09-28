using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using AtariGo.Client.ViewModels.Dialogs;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AtariGo.Client.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for RegisterDialogView.xaml
    /// </summary>
    public partial class RegisterDialogView : UserControl
    {
        public RegisterDialogView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) => RestorePasswordFields();
        }

        private void RegisterButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not RegisterDialogViewModel viewModel)
            {
                return;
            }

            viewModel.Password = RegPasswordBox.Password;
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
                RegPasswordBox.Password = viewModel.Password;
                ConfirmPasswordBox.Password = viewModel.PasswordConfirmation;
            }
        }
    }
}
