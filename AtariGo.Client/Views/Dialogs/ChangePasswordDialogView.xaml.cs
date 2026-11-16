using System.Windows;
using System.Windows.Controls;

using AtariGo.Client.Models;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Views.Dialogs;

public partial class ChangePasswordDialogView : UserControl
{
    private ChangePasswordDialogViewModel? _viewModel;

    public ChangePasswordDialogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnSaveButtonClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel is null || !_viewModel.CanEdit)
        {
            return;
        }
        var input = new PasswordChangeInput
        {
            CurrentPassword = CurrentPasswordBox.Password,
            NewPassword = NewPasswordBox.Password,
            Confirmation = ConfirmationPasswordBox.Password
        };
        if (_viewModel.SaveCommand.CanExecute(input))
        {
            _viewModel.SaveCommand.Execute(input);
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
        _viewModel = DataContext as ChangePasswordDialogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PasswordsCleared += ClearPasswordBoxes;
        }
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PasswordsCleared -= ClearPasswordBoxes;
            _viewModel = null;
        }
        ClearPasswordBoxes();
    }

    private void ClearPasswordBoxes()
    {
        CurrentPasswordBox.Clear();
        NewPasswordBox.Clear();
        ConfirmationPasswordBox.Clear();
    }
}
