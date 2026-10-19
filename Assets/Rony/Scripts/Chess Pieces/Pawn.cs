using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPiece
{
    public override List<Vector2Int> GetValidMoves(BoardManager board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        int dir = (pieceColor == PieceColor.White) ? 1 : -1;

        // Forward one
        Vector2Int oneForward = currentGridPosition + new Vector2Int(0, dir);
        if (board.IsInBounds(oneForward) && board.GetPieceAt(oneForward) == null)
        {
            moves.Add(oneForward);

            // Two-step (only if hasn't moved)
            Vector2Int twoForward = currentGridPosition + new Vector2Int(0, 2 * dir);
            if (!hasMoved && board.IsInBounds(twoForward) && board.GetPieceAt(twoForward) == null)
            {
                moves.Add(twoForward);
            }
        }

        // Diagonal captures
        Vector2Int[] diagonals = new Vector2Int[]
        {
            currentGridPosition + new Vector2Int(-1, dir),
            currentGridPosition + new Vector2Int( 1, dir)
        };

        foreach (var pos in diagonals)
        {
            if (!board.IsInBounds(pos)) continue;
            ChessPiece target = board.GetPieceAt(pos);
            if (target == null) continue;
            if (target.pieceColor == pieceColor) continue;

            // If capturing a king -> notify and skip adding (king capture is not allowed; it's check detection)
            if (target.pieceType == PieceType.King)
            {
                board.RaiseKingInCheck(target.pieceColor);
                continue;
            }

            moves.Add(pos);
        }

        // En-passant
        if (board.LastMove.HasValue)
        {
            var lm = board.LastMove.Value;
            if (lm.piece is Pawn && lm.wasDoublePawnPush && lm.piece.pieceColor != this.pieceColor)
            {
                // last moved pawn is adjacent horizontally?
                if (lm.to.y == currentGridPosition.y &&
                    Mathf.Abs(lm.to.x - currentGridPosition.x) == 1)
                {
                    Vector2Int epCaptureSquare = new Vector2Int(lm.to.x, currentGridPosition.y + dir);
                    // The capture square must be inside board and empty (since en-passant moves to the square behind pawn)
                    if (board.IsInBounds(epCaptureSquare) && board.GetPieceAt(epCaptureSquare) == null)
                    {
                        moves.Add(epCaptureSquare);
                    }
                }
            }
        }

        return moves;
    }
}