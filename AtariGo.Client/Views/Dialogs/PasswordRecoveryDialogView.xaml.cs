using System.Windows;
using System.Windows.Controls;

using AtariGo.Client.Models;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Views.Dialogs;

public partial class PasswordRecoveryDialogView : UserControl
{
    private PasswordRecoveryDialogViewModel? _viewModel;

    public PasswordRecoveryDialogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnResetButtonClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel is null || !_viewModel.CanReset)
        {
            return;
        }
        var input = new PasswordRecoveryInput
        {
            NewPassword = NewPasswordBox.Password,
            Confirmation = ConfirmationPasswordBox.Password
        };
        if (_viewModel.ResetCommand.CanExecute(input))
        {
            _viewModel.ResetCommand.Execute(input);
        }
        else
        {
            input.Clear();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        AttachViewModel();
    }

    private void OnUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        DetachViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        AttachViewModel();
    }

    private void AttachViewModel()
    {
        DetachViewModel();
        _viewModel = DataContext as PasswordRecoveryDialogViewModel;
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
        NewPasswordBox.Clear();
        ConfirmationPasswordBox.Clear();
    }
}
