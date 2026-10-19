using UnityEngine;

[CreateAssetMenu(fileName = "NewChessSetup", menuName = "Chess/Chess Setup", order = 0)]
public class ChessSetupAsset : ScriptableObject
{
    [System.Serializable]
    public class Square
    {
        public bool hasPiece = false;
        public PieceType pieceType = PieceType.Pawn;
        public PieceColor pieceColor = PieceColor.White;
    }

    public Square[] squares = new Square[64];
    public PieceColor startingSide = PieceColor.White;

    // ✅ PUBLIC method you can safely call
    public void EnsureValid()
    {
        if (squares == null || squares.Length != 64)
        {
            Square[] tmp = new Square[64];

            if (squares != null)
            {
                for (int i = 0; i < Mathf.Min(64, squares.Length); i++)
                    tmp[i] = squares[i];
            }

            for (int i = 0; i < 64; i++)
                if (tmp[i] == null)
                    tmp[i] = new Square();

            squares = tmp;
        }
    }

    // Unity-only callback
    private void OnValidate()
    {
        EnsureValid();
    }

    public Square Get(int x, int y)
    {
        return squares[y * 8 + x];
    }

    public void Set(int x, int y, bool hasPiece, PieceType type, PieceColor color)
    {
        Square s = squares[y * 8 + x];
        s.hasPiece = hasPiece;
        s.pieceType = type;
        s.pieceColor = color;
    }
}