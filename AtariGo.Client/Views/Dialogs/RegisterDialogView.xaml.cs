using System.Windows;
using System.Windows.Controls;

using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Views.Dialogs
{
    public partial class RegisterDialogView : UserControl
    {
        private RegisterDialogViewModel? _viewModel;

        public RegisterDialogView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnRegisterButtonClick(object sender, RoutedEventArgs eventArgs)
        {
            if (DataContext is not RegisterDialogViewModel viewModel)
            {
                return;
            }

            if (!viewModel.CanEdit)
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
            DependencyPropertyChangedEventArgs eventArgs)
        {
            AttachViewModel();
        }

        private void OnLoaded(object sender, RoutedEventArgs eventArgs)
        {
            AttachViewModel();
        }

        private void OnUnloaded(object sender, RoutedEventArgs eventArgs)
        {
            DetachViewModel();
        }

        private void AttachViewModel()
        {
            DetachViewModel();
            _viewModel = DataContext as RegisterDialogViewModel;
            if (_viewModel is not null)
            {
                _viewModel.SecretsCleared += ClearPasswordBoxes;
            }
        }

        private void DetachViewModel()
        {
            if (_viewModel is not null)
            {
                _viewModel.SecretsCleared -= ClearPasswordBoxes;
                _viewModel = null;
            }
            ClearPasswordBoxes();
        }

        private void ClearPasswordBoxes()
        {
            PasswordBox.Clear();
            ConfirmPasswordBox.Clear();
        }
    }
}
