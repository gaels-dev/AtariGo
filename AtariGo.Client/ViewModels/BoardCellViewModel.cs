using System;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;

namespace AtariGo.Client.ViewModels
{
    public class BoardCellViewModel : ViewModelBase
    {
        private int _row;
        private int _column;
        private StoneColor _stone;
        private bool _isLastMove;

        public BoardCellViewModel(int row, int column, Action<BoardCellViewModel> onCellClicked)
        {
            _row = row;
            _column = column;
            _stone = StoneColor.None;

            CellClickCommand = new RelayCommand(() => onCellClicked(this));
        }

        public int Row
        {
            get => _row;
            set => SetProperty(ref _row, value);
        }

        public int Column
        {
            get => _column;
            set => SetProperty(ref _column, value);
        }

        public StoneColor Stone
        {
            get => _stone;
            set => SetProperty(ref _stone, value);
        }

        public bool IsLastMove
        {
            get => _isLastMove;
            set => SetProperty(ref _isLastMove, value);
        }

        public ICommand CellClickCommand { get; }
    }
}
