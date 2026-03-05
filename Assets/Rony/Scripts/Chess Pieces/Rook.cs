using UnityEngine;
using System.Collections.Generic;

public class Rook : ChessPiece
{
    public override List<Vector2Int> GetValidMoves(BoardManager board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0),   // Right
            new Vector2Int(-1, 0),  // Left
            new Vector2Int(0, 1),   // Up
            new Vector2Int(0, -1)   // Down
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