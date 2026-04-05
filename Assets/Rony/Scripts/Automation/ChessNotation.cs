using UnityEngine;
using System.Text;
using System.Collections.Generic;

public static class ChessNotation
{
    // Converts grid position (0,0) to algebraic ("a1")
    public static string ToAlgebraic(Vector2Int gridPos)
    {
        char file = (char)('a' + gridPos.x);
        int rank = gridPos.y + 1;
        return $"{file}{rank}";
    }

    // Converts algebraic ("a1") to grid position (0,0)
    public static Vector2Int AlgebraicToGrid(string algebraic)
    {
        if (string.IsNullOrEmpty(algebraic) || algebraic.Length < 2) return new Vector2Int(-1, -1);
        
        // Validate file (a-h)
        int x = algebraic[0] - 'a';
        if (x < 0 || x > 7) return new Vector2Int(-1, -1);

        // Validate rank (1-8)
        if (!char.IsDigit(algebraic[1])) return new Vector2Int(-1, -1);
        int y = (int)char.GetNumericValue(algebraic[1]) - 1;
        if (y < 0 || y > 7) return new Vector2Int(-1, -1);

        return new Vector2Int(x, y);
    }

    // Generates the Standard Algebraic Notation (SAN) for a single move (e.g., "Nf3", "exd5", "O-O")
    public static string GetSAN(BoardManager board, BoardManager.MoveRecord move, bool isCheck, bool isCheckmate, PieceType? promotion = null)
    {
        // 1. Castling
        if (move.piece.pieceType == PieceType.King && Mathf.Abs(move.to.x - move.from.x) == 2)
        {
            string castling = move.to.x > move.from.x ? "O-O" : "O-O-O";
            return castling + (isCheckmate ? "#" : (isCheck ? "+" : ""));
        }

        StringBuilder san = new StringBuilder();
        bool isCapture = move.captured != null || move.wasEnPassant;

        // 2. Piece Identification & Disambiguation
        if (move.piece.pieceType == PieceType.Pawn)
        {
            if (isCapture)
            {
                // Pawn captures always start with the origin file (e.g., "ex")
                san.Append((char)('a' + move.from.x));
            }
        }
        else
        {
            // Add piece letter
            san.Append(GetPieceLetter(move.piece.pieceType, true));

            // Disambiguation (e.g., if two Knights can move to d2, output "Nbd2" or "N1d2")
            // For a basic implementation, we skip deep disambiguation here to keep it simple,
            // but for a strict bot, you would iterate the board to see if another identical piece can reach 'move.to'.
        }

        // 3. Capture flag
        if (isCapture) san.Append("x");

        // 4. Destination square
        san.Append(ToAlgebraic(move.to));

        // 5. Promotion
        if (promotion.HasValue)
        {
            san.Append("=").Append(GetPieceLetter(promotion.Value, true));
        }

        // 6. Check / Checkmate
        if (isCheckmate) san.Append("#");
        else if (isCheck) san.Append("+");

        return san.ToString();
    }

    // Generates the current FEN string
    public static string GetFEN(BoardManager board, PieceColor activeColor, int halfMoveClock, int fullMoveNumber)
    {
        StringBuilder fen = new StringBuilder();

        // 1. Piece Placement (from rank 8 down to 1)
        for (int y = 7; y >= 0; y--)
        {
            int emptySpaces = 0;
            for (int x = 0; x < 8; x++)
            {
                ChessPiece piece = board.GetPieceAt(new Vector2Int(x, y));
                if (piece == null)
                {
                    emptySpaces++;
                }
                else
                {
                    if (emptySpaces > 0)
                    {
                        fen.Append(emptySpaces);
                        emptySpaces = 0;
                    }
                    string pieceLetter = GetPieceLetter(piece.pieceType, false);
                    fen.Append(piece.pieceColor == PieceColor.White ? pieceLetter.ToUpper() : pieceLetter.ToLower());
                }
            }
            if (emptySpaces > 0) fen.Append(emptySpaces);
            if (y > 0) fen.Append("/");
        }

        // 2. Active Color
        fen.Append(activeColor == PieceColor.White ? " w " : " b ");

        // 3. Castling Availability (Simplified: checking if kings/rooks have moved)
        StringBuilder castling = new StringBuilder();
        ChessPiece wk = board.GetPieceAt(new Vector2Int(4, 0));
        if (wk != null && wk.pieceType == PieceType.King && !wk.hasMoved)
        {
            ChessPiece wrK = board.GetPieceAt(new Vector2Int(7, 0)); // Kingside
            ChessPiece wrQ = board.GetPieceAt(new Vector2Int(0, 0)); // Queenside
            if (wrK != null && wrK.pieceType == PieceType.Rook && !wrK.hasMoved) castling.Append("K");
            if (wrQ != null && wrQ.pieceType == PieceType.Rook && !wrQ.hasMoved) castling.Append("Q");
        }
        ChessPiece bk = board.GetPieceAt(new Vector2Int(4, 7));
        if (bk != null && bk.pieceType == PieceType.King && !bk.hasMoved)
        {
            ChessPiece brK = board.GetPieceAt(new Vector2Int(7, 7)); // Kingside
            ChessPiece brQ = board.GetPieceAt(new Vector2Int(0, 7)); // Queenside
            if (brK != null && brK.pieceType == PieceType.Rook && !brK.hasMoved) castling.Append("k");
            if (brQ != null && brQ.pieceType == PieceType.Rook && !brQ.hasMoved) castling.Append("q");
        }
        fen.Append(castling.Length > 0 ? castling.ToString() : "-");

        // 4. En Passant Target
        fen.Append(" ");
        if (board.LastMove.HasValue && board.LastMove.Value.wasDoublePawnPush)
        {
            // Target is the square behind the pawn that just moved
            int dir = board.LastMove.Value.piece.pieceColor == PieceColor.White ? -1 : 1;
            Vector2Int epSquare = new Vector2Int(board.LastMove.Value.to.x, board.LastMove.Value.to.y + dir);
            fen.Append(ToAlgebraic(epSquare));
        }
        else
        {
            fen.Append("-");
        }

        // 5 & 6. Halfmove and Fullmove
        fen.Append($" {halfMoveClock} {fullMoveNumber}");

        return fen.ToString();
    }

    private static string GetPieceLetter(PieceType type, bool isSAN)
    {
        switch (type)
        {
            case PieceType.Knight: return "N";
            case PieceType.Bishop: return "B";
            case PieceType.Rook: return "R";
            case PieceType.Queen: return "Q";
            case PieceType.King: return "K";
            case PieceType.Pawn: return isSAN ? "" : "P"; // SAN doesn't use 'P' for pawns
            default: return "";
        }
    }
}