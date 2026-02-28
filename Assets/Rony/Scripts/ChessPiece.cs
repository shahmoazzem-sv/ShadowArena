using UnityEngine;
using System.Collections.Generic;

public enum PieceColor { White, Black }
public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King }
public abstract class ChessPiece : MonoBehaviour
{
    public PieceColor pieceColor;
    public PieceType pieceType;
    public Vector2Int currentGridPosition;

    // Every specific piece will override this to define how it moves
    public abstract List<Vector2Int> GetValidMoves(BoardManager board);
}
