using System.Collections.Generic;
using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class ReportPlayerDialogViewModel : DialogViewModelBase
    {
        private string _targetPlayerName;
        private string _selectedReason;
        private string _details = string.Empty;

        public ReportPlayerDialogViewModel(string targetPlayerName)
            : base(Properties.Resources.ReportPlayer_Lbl_Title)
        {
            _targetPlayerName = targetPlayerName;
            Reasons = new List<string>
            {
                Properties.Resources.ReportPlayer_Reason_OffensiveLanguage,
                Properties.Resources.ReportPlayer_Reason_Cheating,
                Properties.Resources.ReportPlayer_Reason_Unsportsmanlike,
                Properties.Resources.ReportPlayer_Reason_Other
            };
            _selectedReason = Reasons[0];

            SubmitCommand = new RelayCommand(() => Close(true));
            CancelCommand = new RelayCommand(() => Close(false));
        }

        public string TargetPlayerName
        {
            get => _targetPlayerName;
            set => SetProperty(ref _targetPlayerName, value);
        }

        public IReadOnlyList<string> Reasons { get; }

        public string SelectedReason
        {
            get => _selectedReason;
            set => SetProperty(ref _selectedReason, value);
        }

        public string Details
        {
            get => _details;
            set => SetProperty(ref _details, value);
        }

        public ICommand SubmitCommand { get; }

        public ICommand CancelCommand { get; }
    }
}
