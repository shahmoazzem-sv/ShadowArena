using UnityEngine;
using System.Collections.Generic;

public enum PieceColor { White, Black }
public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King }
public abstract class ChessPiece : MonoBehaviour
{
    public PieceData pieceData;
    public PieceColor pieceColor;
    public PieceType pieceType;
    public Vector2Int currentGridPosition;

    public bool hasMoved = false;

    // Every specific piece will override this to define how it moves
    public abstract List<Vector2Int> GetValidMoves(BoardManager board);
}
