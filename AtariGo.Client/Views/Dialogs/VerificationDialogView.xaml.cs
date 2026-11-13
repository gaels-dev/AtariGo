using System.Windows.Controls;

using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Views.Dialogs
{
    public partial class VerificationDialogView : UserControl
    {
        public VerificationDialogView()
        {
            InitializeComponent();
        }

        private void OnConfirmButtonClick(object sender, System.Windows.RoutedEventArgs e)
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
