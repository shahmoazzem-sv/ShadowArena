using UnityEngine;
using System.Collections.Generic;

public class Knight : ChessPiece
{
    public override List<Vector2Int> GetValidMoves(BoardManager board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        Vector2Int[] offsets =
        {
            new Vector2Int( 1,  2),
            new Vector2Int( 2,  1),
            new Vector2Int( 2, -1),
            new Vector2Int( 1, -2),
            new Vector2Int(-1, -2),
            new Vector2Int(-2, -1),
            new Vector2Int(-2,  1),
            new Vector2Int(-1,  2)
        };

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int pos = currentGridPosition + offset;

            if (!board.IsInBounds(pos))
                continue;

            ChessPiece target = board.GetPieceAt(pos);

            if (target == null)
            {
                moves.Add(pos);
            }
            else
            {
                if (target.pieceColor != pieceColor)
                {
                    if (target.pieceType == PieceType.King)
                    {
                        board.RaiseKingInCheck(target.pieceColor);
                    }
                    else
                    {
                        moves.Add(pos);
                    }
                }
            }
        }

        return moves;
    }
}