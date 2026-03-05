using UnityEngine;
using System.Collections.Generic;

public class Bishop : ChessPiece
{
    public override List<Vector2Int> GetValidMoves(BoardManager board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 1),    // Top Right
            new Vector2Int(-1, 1),   // Top Left
            new Vector2Int(1, -1),   // Bottom Right
            new Vector2Int(-1, -1)   // Bottom Left
        };

        foreach (var dir in directions)
        {
            Vector2Int pos = currentGridPosition + dir;

            while (board.IsInBounds(pos))
            {
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

                    // stop scanning this direction
                    break;
                }

                pos += dir;
            }
        }

        return moves;
    }
}