using System.Collections.Generic;
using UnityEngine;

public static class ChessOpeningBook
{
    // Dictionary mapping a move sequence (joined by space) to a list of possible responses in UCI format (e.g. "e2e4")
    private static readonly Dictionary<string, string[]> book = new Dictionary<string, string[]>()
    {
        // --- White First Moves ---
        { "", new string[] { "e2e4", "d2d4", "g1f3", "c2c4" } },

        // --- Responses to e4 ---
        { "e4", new string[] { "e7e5", "c7c5", "e7e6", "c7c6", "g8f6", "d7d5" } },

        // King's Pawn (1. e4 e5)
        { "e4 e5", new string[] { "g1f3", "f2f4", "d2d4", "b1c3" } },
        { "e4 e5 Nf3", new string[] { "b8c6", "g8f6", "d7d6" } },
        { "e4 e5 Nf3 Nc6", new string[] { "f1b5", "f1c4", "d2d4", "b1c3", "c2c3" } },
        { "e4 e5 Nf3 Nc6 Bb5", new string[] { "a7a6", "g8f6", "d7d6", "f7f5" } }, // Ruy Lopez
        { "e4 e5 Nf3 Nc6 Bb5 a6", new string[] { "b5a4" } },
        { "e4 e5 Nf3 Nc6 Bc4", new string[] { "f8c5", "g8f6" } }, // Italian
        { "e4 e5 Nf3 Nc6 Bc4 Bc5", new string[] { "c2c3", "d2d3", "e1g1" } },

        // Sicilian Defense (1. e4 c5)
        { "e4 c5", new string[] { "g1f3", "b1c3", "c2c3", "d2d4" } },
        { "e4 c5 Nf3", new string[] { "d7d6", "e7e6", "b8c6" } },
        { "e4 c5 Nf3 d6", new string[] { "d2d4", "f1b5" } },
        { "e4 c5 Nf3 d6 d4", new string[] { "c5d4" } },
        { "e4 c5 Nf3 d6 d4 cxd4", new string[] { "f3d4" } },
        { "e4 c5 Nf3 d6 d4 cxd4 Nxd4", new string[] { "g8f6" } },
        { "e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6", new string[] { "b1c3" } },

        // French Defense (1. e4 e6)
        { "e4 e6", new string[] { "d2d4" } },
        { "e4 e6 d4", new string[] { "d7d5" } },
        { "e4 e6 d4 d5", new string[] { "b1c3", "e4e5", "b1d2", "e4d5" } },

        // Scandinavian Defense (1. e4 d5)
        { "e4 d5", new string[] { "e4d5" } },
        { "e4 d5 exd5", new string[] { "d8d5", "g8f6" } },
        { "e4 d5 exd5 Qxd5", new string[] { "b1c3" } },

        // Caro-Kann Defense (1. e4 c6)
        { "e4 c6", new string[] { "d2d4" } },
        { "e4 c6 d4", new string[] { "d7d5" } },
        { "e4 c6 d4 d5", new string[] { "e4e5", "b1c3", "e4d5" } },

        // --- Responses to d4 ---
        { "d4", new string[] { "d7d5", "g8f6", "f7f5", "e7e6" } },

        // Queen's Gambit (1. d4 d5)
        { "d4 d5", new string[] { "c2c4", "g1f3", "c1f4" } }, // c2c4=QG, Bf4=London
        { "d4 d5 c4", new string[] { "e7e6", "c7c6", "d5c4", "e7e5" } }, // QGD, Slav, QGA, Albin
        { "d4 d5 c4 e6", new string[] { "b1c3", "g1f3" } },

        // London System (1. d4 ... 2. Bf4)
        { "d4 Nf6", new string[] { "c2c4", "g1f3", "c1f4" } },

        // Nimzo-Indian / King's Indian
        { "d4 Nf6 c4", new string[] { "e7e6", "g7g6" } },
        { "d4 Nf6 c4 e6", new string[] { "b1c3", "g1f3" } },
        { "d4 Nf6 c4 e6 Nc3", new string[] { "f8b4" } }, // Nimzo-Indian

        // --- King's Indian ---
        { "d4 Nf6 c4 g6", new string[] { "b1c3", "g1f3" } },
    };

    public static string GetBookMove(List<string> history)
    {
        if (history == null) return null;

        // Normalizing: strip '+' and '#' from SAN moves so "Bb5+" matches "Bb5"
        List<string> normalizedHistory = new List<string>();
        foreach (var move in history)
        {
            normalizedHistory.Add(move.Replace("+", "").Replace("#", ""));
        }

        string sequence = string.Join(" ", normalizedHistory);
        if (book.ContainsKey(sequence))
        {
            string[] moves = book[sequence];
            return moves[Random.Range(0, moves.Length)];
        }
        return null;
    }

    // Helper to find the piece and destination from a UCI string (e.g. "e2e4")
    public static (ChessPiece piece, Vector2Int to)? UCItoMove(BoardManager board, string uci, PieceColor color)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) return null;

        Vector2Int from = ChessNotation.AlgebraicToGrid(uci.Substring(0, 2));
        Vector2Int to = ChessNotation.AlgebraicToGrid(uci.Substring(2, 2));

        ChessPiece piece = board.GetPieceAt(from);
        if (piece != null && piece.pieceColor == color)
        {
            // Verify it's a legal move
            List<Vector2Int> legal = board.GetLegalMoves(piece);
            if (legal != null && legal.Contains(to))
            {
                return (piece, to);
            }
        }
        return null;
    }
}
