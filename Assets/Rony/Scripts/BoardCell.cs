public class BoardCell
{
    GridSystem<BoardCell> grid;
    int x, y;
    string cellName;
    ChessPiece pieceOnCell;


    public BoardCell(GridSystem<BoardCell> grid, int x, int y)
    {
        this.grid = grid;
        this.x = x;
        this.y = y;
        this.cellName = GenerateCellName(x, y);
    }

    private string GenerateCellName(int x, int y)
    {
        // Map X to a letter (a-h)
        char file = x switch
        {
            0 => 'a',
            1 => 'b',
            2 => 'c',
            3 => 'd',
            4 => 'e',
            5 => 'f',
            6 => 'g',
            7 => 'h',
            _ => '?' // Out of bounds fallback
        };

        // Map Y to a 1-indexed rank (1-8)
        int rank = y + 1;

        return $"{file}{rank}";
    }

    public void SetPiece(ChessPiece piece) => pieceOnCell = piece;
    public ChessPiece GetPiece() => pieceOnCell;
    public void EmptyCell() => pieceOnCell = null;
    public bool IsOccupied() => pieceOnCell != null;
    public string GetCellName() => cellName;
}
