using UnityEngine;
using System.Collections.Generic;

public class King : ChessPiece
{
    public override List<Vector2Int> GetValidMoves(BoardManager board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        // 8 directions
        Vector2Int[] dirs = new Vector2Int[]
        {
            new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1),
            new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1)
        };

        foreach (var d in dirs)
        {
            Vector2Int p = currentGridPosition + d;
            if (!board.IsInBounds(p)) continue;
            ChessPiece occupant = board.GetPieceAt(p);
            if (occupant == null || occupant.pieceColor != this.pieceColor)
            {
                // We don't check "moving into check" here — BoardManager filters that later
                moves.Add(p);
            }
        }

        // Castling: basic rules implemented:
        // - King and chosen rook have not moved (hasMoved == false)
        // - Squares between are empty
        // - No square king passes through or lands on is under attack

        // CASTLING LOGIC
        // Rule 1: King must not have moved
        // Rule 2: King must NOT be currently in check (Using GameManager to avoid StackOverflow)
        bool isCurrentlyInCheck = GameManager.Instance.CheckedKing == this.pieceColor;
        if (!hasMoved && !isCurrentlyInCheck)
        {
            int y = currentGridPosition.y;
            // Kingside castling (rook at x = 7)
            if (TryAddCastle(board, new Vector2Int(7, y), moves, true)) { /*added inside*/ }

            // Queenside castling (rook at x = 0)
            if (TryAddCastle(board, new Vector2Int(0, y), moves, false)) { /*added inside*/ }
        }

        return moves;
    }

    // helper to add castling move if allowed
    // private bool TryAddCastle(BoardManager board, Vector2Int rookPos, List<Vector2Int> moves, bool kingside)
    // {
    //     ChessPiece rook = board.GetPieceAt(rookPos);
    //     if (rook == null || rook.pieceType != PieceType.Rook || rook.pieceColor != this.pieceColor) return false;
    //     if (rook.hasMoved) return false;

    //     int dir = kingside ? 1 : -1;
    //     int steps = kingside ? 2 : 2; // king moves 2 squares either side

    //     // check empty squares between king and rook
    //     int startX = Mathf.Min(currentGridPosition.x, rookPos.x) + 1;
    //     int endX = Mathf.Max(currentGridPosition.x, rookPos.x) - 1;
    //     for (int x = startX; x <= endX; x++)
    //     {
    //         if (board.GetPieceAt(new Vector2Int(x, currentGridPosition.y)) != null) return false;
    //     }

    //     // check squares king will pass through (including destination) are not attacked
    //     // king passes through current + dir and current + dir * 2 (destination)
    //     Vector2Int pass1 = currentGridPosition + new Vector2Int(dir, 0);
    //     Vector2Int dest = currentGridPosition + new Vector2Int(dir * 2, 0);

    //     // if any of these squares is under attack by opponent, castling not allowed
    //     PieceColor opponent = (this.pieceColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;

    //     // We use board.IsKingInCheck(opponent) logic by simulating attacks using other pieces' GetValidMoves().
    //     // To check square attack, we'll simulate: temporarily move king to the square and check IsKingInCheck for that color.
    //     // But using BoardManager's GetLegalMoves would lead to recursion. So we'll use a small simulation.
    //     var bm = board;

    //     // simulate pass1
    //     var rec1 = bm.SimulateMove(this, pass1);
    //     bool attacked1 = bm.IsKingInCheck(this.pieceColor);
    //     bm.UndoSimulatedMove(rec1);
    //     if (attacked1) return false;

    //     // simulate dest
    //     var rec2 = bm.SimulateMove(this, dest);
    //     bool attacked2 = bm.IsKingInCheck(this.pieceColor);
    //     bm.UndoSimulatedMove(rec2);
    //     if (attacked2) return false;

    //     // all checks passed — add castling destination as a valid move (BoardManager will perform rook move on actual drop if desired)
    //     moves.Add(dest);
    //     return true;
    // }

    private bool TryAddCastle(BoardManager board, Vector2Int rookPos, List<Vector2Int> moves, bool kingside)
    {
        ChessPiece rook = board.GetPieceAt(rookPos);
        if (rook == null || rook.pieceType != PieceType.Rook || rook.pieceColor != this.pieceColor) return false;
        if (rook.hasMoved) return false;

        // Check empty squares between king and rook
        int startX = Mathf.Min(currentGridPosition.x, rookPos.x) + 1;
        int endX = Mathf.Max(currentGridPosition.x, rookPos.x) - 1;
        for (int x = startX; x <= endX; x++)
        {
            if (board.GetPieceAt(new Vector2Int(x, currentGridPosition.y)) != null) return false;
        }

        int dir = kingside ? 1 : -1;
        Vector2Int pass1 = currentGridPosition + new Vector2Int(dir, 0);
        Vector2Int dest = currentGridPosition + new Vector2Int(dir * 2, 0);

        // We only simulate these two squares. 
        // Because we are already inside a "if (!hasMoved)" block, 
        // the recursion depth is limited and won't cause a crash in most setups.

        // simulate pass1 (The square the king jumps over)
        var rec1 = board.SimulateMove(this, pass1);
        bool attacked1 = board.IsKingInCheck(this.pieceColor);
        board.UndoSimulatedMove(rec1);
        if (attacked1) return false;

        // simulate dest (The square the king lands on)
        var rec2 = board.SimulateMove(this, dest);
        bool attacked2 = board.IsKingInCheck(this.pieceColor);
        board.UndoSimulatedMove(rec2);
        if (attacked2) return false;

        moves.Add(dest);
        return true;
    }
}