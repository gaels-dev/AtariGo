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
    /// Interaction logic for VerificationDialogView.xaml
    /// </summary>
    public partial class VerificationDialogView : UserControl
    {
        public VerificationDialogView()
        {
            InitializeComponent();
        }

        private void ConfirmButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not VerificationDialogViewModel viewModel)
            {
                return;
            }

            object confirmationText = ConfirmationCodeBox.Text;
            if (viewModel.ConfirmCommand.CanExecute(confirmationText))
            {
                viewModel.ConfirmCommand.Execute(confirmationText);
            }
        }
    }
}
