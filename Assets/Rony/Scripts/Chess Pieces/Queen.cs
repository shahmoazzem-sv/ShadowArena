using UnityEngine;
using System.Collections.Generic;

public class Queen : ChessPiece
{
    public override List<Vector2Int> GetValidMoves(BoardManager board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        Vector2Int[] directions =
        {
            new Vector2Int(1,0),   // Right
            new Vector2Int(-1,0),  // Left
            new Vector2Int(0,1),   // Up
            new Vector2Int(0,-1),  // Down

            new Vector2Int(1,1),   // Up Right
            new Vector2Int(-1,1),  // Up Left
            new Vector2Int(1,-1),  // Down Right
            new Vector2Int(-1,-1)  // Down Left
        };

        foreach (Vector2Int dir in directions)
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

                    break; // stop ray when hitting any piece
                }

                pos += dir;
            }
        }

        return moves;
    }
}