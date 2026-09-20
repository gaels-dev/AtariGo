using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class OptionsDialogViewModel : DialogViewModelBase
    {
        private bool _isMusicEnabled;
        private bool _isSoundEffectsEnabled;
        private string _selectedLanguage;

        public OptionsDialogViewModel() : base("Options")
        {
            _isMusicEnabled = true;
            _isSoundEffectsEnabled = true;

            string currentCulture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
            _selectedLanguage = currentCulture switch
            {
                "en" => "English",
                "de" => "Deutsch",
                _ => "Español"
            };

            Languages = new List<string> { "Español", "English", "Deutsch" };
            SaveAndCloseCommand = new RelayCommand(SaveAndClose);
        }

        public bool IsMusicEnabled
        {
            get => _isMusicEnabled;
            set => SetProperty(ref _isMusicEnabled, value);
        }

        public bool IsSoundEffectsEnabled
        {
            get => _isSoundEffectsEnabled;
            set => SetProperty(ref _isSoundEffectsEnabled, value);
        }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set => SetProperty(ref _selectedLanguage, value);
        }

        public string SelectedCultureCode => SelectedLanguage switch
        {
            "English" => "en",
            "Deutsch" => "de",
            _ => "es"
        };

        public IReadOnlyList<string> Languages { get; }

        public ICommand SaveAndCloseCommand { get; }

        private void SaveAndClose()
        {
            Close(true);
        }
    }
}
