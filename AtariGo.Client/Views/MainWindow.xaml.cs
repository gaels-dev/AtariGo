using System.Windows;

using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() : this(new MainViewModel())
        {
        }

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
