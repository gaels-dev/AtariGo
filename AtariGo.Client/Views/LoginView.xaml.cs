using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Views
{
    /// <summary>
    /// Interaction logic for LoginView.xaml
    /// </summary>
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private void BtnChangeLanguage_Click(object sender, RoutedEventArgs e)
        {
            LanguageOverlay.Visibility = Visibility.Visible;
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            string username = string.IsNullOrWhiteSpace(TxtEmail.Text) ? "Player" : TxtEmail.Text;
            var viewModel = new MainViewModel(isGuest: false, playerName: username);
            var mainWindow = new MainWindow(viewModel);
            mainWindow.Show();
            this.Close();
        }

        private void BtnGuest_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new MainViewModel(isGuest: true, playerName: "Guest");
            var mainWindow = new MainWindow(viewModel);
            mainWindow.Show();
            this.Close();
        }

        private void BtnCancelLanguage_Click(object sender, RoutedEventArgs e)
        {
            LanguageOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnConfirmLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (CmbLanguages.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is string cultureCode)
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureCode);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);

                LanguageOverlay.Visibility = Visibility.Collapsed;
                RefreshWindow();
            }
        }

        private void RefreshWindow()
        {
            var newLogin = new LoginView();
            newLogin.Show();
            this.Close();
        }

    }
}
