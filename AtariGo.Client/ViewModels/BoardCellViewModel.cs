using AtariGo.Client.Models;

namespace AtariGo.Client.ViewModels
{
    public class BoardCellViewModel : ViewModelBase
    {
        private StoneColor _stone;
        private bool _isLastMove;

        public BoardCellViewModel(int row, int column)
        {
            Row = row;
            Column = column;
            _stone = StoneColor.None;

        }

        public int Row
        {
            get;
        }

        public int Column
        {
            get;
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

    }
}
