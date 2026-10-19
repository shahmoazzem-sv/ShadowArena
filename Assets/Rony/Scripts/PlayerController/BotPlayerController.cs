using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI player using Minimax with Alpha-Beta pruning.
///
/// Difficulty levels drive search depth:
///   Easy   → depth 2
///   Medium → depth 3
///   Hard   → depth 4
///
/// Phase 3 improvements:
///   • Move ordering  — captures first (MVV-LVA), then check-giving moves, then rest.
///                      Better ordering = more alpha-beta cut-offs = faster/deeper search.
///   • Check/checkmate awareness — evaluation gives a bonus for putting the opponent in
///                      check and a large bonus/penalty for checkmate positions.
///   • Endgame awareness — detects when queens are off the board (endgame phase) and
///                      applies king-activity bonus, passed-pawn bonus, and mop-up score
///                      to guide the bot toward winning endgames.
/// </summary>
public class BotPlayerController : MonoBehaviour, IPlayerController
{
    public enum Difficulty { Easy, Medium, Hard }

    [Header("Bot Settings")]
    public PieceColor ControlledColor;
    public float ThinkDelay = 0.6f;
    public Difficulty BotDifficulty = Difficulty.Medium;

    // ──────────────────────────────────────────────
    // Material values
    // ──────────────────────────────────────────────
    private const int VAL_PAWN = 100;
    private const int VAL_KNIGHT = 320;
    private const int VAL_BISHOP = 330;
    private const int VAL_ROOK = 500;
    private const int VAL_QUEEN = 900;
    private const int VAL_KING = 20000;

    // Bonus for giving check (scaled so it doesn't override material)
    private const int CHECK_BONUS = 50;
    // Score returned for checkmate positions inside minimax
    private const int CHECKMATE_SCORE = 100000;

    // ──────────────────────────────────────────────
    // Piece-Square Tables (from White's perspective; flip row for Black)
    // ──────────────────────────────────────────────
    // Indexed [y, x] — y=0 is White's back rank.

    private static readonly int[,] PST_Pawn = {
        {  0,  0,  0,  0,  0,  0,  0,  0 },
        { 50, 50, 50, 50, 50, 50, 50, 50 },
        { 10, 10, 20, 30, 30, 20, 10, 10 },
        {  5,  5, 10, 25, 25, 10,  5,  5 },
        {  0,  0,  0, 20, 20,  0,  0,  0 },
        {  5, -5,-10,  0,  0,-10, -5,  5 },
        {  5, 10, 10,-20,-20, 10, 10,  5 },
        {  0,  0,  0,  0,  0,  0,  0,  0 },
    };

    private static readonly int[,] PST_Knight = {
        {-50,-40,-30,-30,-30,-30,-40,-50 },
        {-40,-20,  0,  0,  0,  0,-20,-40 },
        {-30,  0, 10, 15, 15, 10,  0,-30 },
        {-30,  5, 15, 20, 20, 15,  5,-30 },
        {-30,  0, 15, 20, 20, 15,  0,-30 },
        {-30,  5, 10, 15, 15, 10,  5,-30 },
        {-40,-20,  0,  5,  5,  0,-20,-40 },
        {-50,-40,-30,-30,-30,-30,-40,-50 },
    };

    private static readonly int[,] PST_Bishop = {
        {-20,-10,-10,-10,-10,-10,-10,-20 },
        {-10,  0,  0,  0,  0,  0,  0,-10 },
        {-10,  0,  5, 10, 10,  5,  0,-10 },
        {-10,  5,  5, 10, 10,  5,  5,-10 },
        {-10,  0, 10, 10, 10, 10,  0,-10 },
        {-10, 10, 10, 10, 10, 10, 10,-10 },
        {-10,  5,  0,  0,  0,  0,  5,-10 },
        {-20,-10,-10,-10,-10,-10,-10,-20 },
    };

    private static readonly int[,] PST_Rook = {
        {  0,  0,  0,  0,  0,  0,  0,  0 },
        {  5, 10, 10, 10, 10, 10, 10,  5 },
        { -5,  0,  0,  0,  0,  0,  0, -5 },
        { -5,  0,  0,  0,  0,  0,  0, -5 },
        { -5,  0,  0,  0,  0,  0,  0, -5 },
        { -5,  0,  0,  0,  0,  0,  0, -5 },
        { -5,  0,  0,  0,  0,  0,  0, -5 },
        {  0,  0,  0,  5,  5,  0,  0,  0 },
    };

    private static readonly int[,] PST_Queen = {
        {-20,-10,-10, -5, -5,-10,-10,-20 },
        {-10,  0,  0,  0,  0,  0,  0,-10 },
        {-10,  0,  5,  5,  5,  5,  0,-10 },
        { -5,  0,  5,  5,  5,  5,  0, -5 },
        {  0,  0,  5,  5,  5,  5,  0, -5 },
        {-10,  5,  5,  5,  5,  5,  0,-10 },
        {-10,  0,  5,  0,  0,  0,  0,-10 },
        {-20,-10,-10, -5, -5,-10,-10,-20 },
    };

    // King: middlegame — stay safe behind pawns
    private static readonly int[,] PST_King_Mid = {
        {-30,-40,-40,-50,-50,-40,-40,-30 },
        {-30,-40,-40,-50,-50,-40,-40,-30 },
        {-30,-40,-40,-50,-50,-40,-40,-30 },
        {-30,-40,-40,-50,-50,-40,-40,-30 },
        {-20,-30,-30,-40,-40,-30,-30,-20 },
        {-10,-20,-20,-20,-20,-20,-20,-10 },
        { 20, 20,  0,  0,  0,  0, 20, 20 },
        { 20, 30, 10,  0,  0, 10, 30, 20 },
    };

    // King: endgame — become active
    private static readonly int[,] PST_King_End = {
        {-50,-40,-30,-20,-20,-30,-40,-50 },
        {-30,-20,-10,  0,  0,-10,-20,-30 },
        {-30,-10, 20, 30, 30, 20,-10,-30 },
        {-30,-10, 30, 40, 40, 30,-10,-30 },
        {-30,-10, 30, 40, 40, 30,-10,-30 },
        {-30,-10, 20, 30, 30, 20,-10,-30 },
        {-30,-30,  0,  0,  0,  0,-30,-30 },
        {-50,-30,-30,-30,-30,-30,-30,-50 },
    };

    bool thinking = false;

    // ──────────────────────────────────────────────
    // IPlayerController interface
    // ──────────────────────────────────────────────
    public void Initialize(PieceColor color)
    {
        ControlledColor = color;
        if (GameManager.Instance != null)
            BotDifficulty = GameManager.Instance.BotDifficulty;
    }

    public void OnTurnStarted()
    {
        if (GameManager.Instance.GetCurrentTurnColor() != ControlledColor) return;
        if (thinking) return;
        StartCoroutine(ThinkAndPlay());
    }

    public void OnTurnEnded()
    {
        StopAllCoroutines();
        thinking = false;
    }

    // ──────────────────────────────────────────────
    // Main coroutine
    // ──────────────────────────────────────────────
    IEnumerator ThinkAndPlay()
    {
        thinking = true;
        yield return new WaitForSeconds(ThinkDelay);

        int depth = BotDifficulty == Difficulty.Easy ? 2 :
                    BotDifficulty == Difficulty.Medium ? 3 : 4;

        bool botIsWhite = (ControlledColor == PieceColor.White);

        // ──────────────────────────────────────────────
        // Phase 4: Opening Book
        // ──────────────────────────────────────────────
        (ChessPiece piece, Vector2Int to)? bestMove = null;

        // Try to find a move in the opening book first
        if (GameManager.Instance != null && GameManager.Instance.PgnMoves != null)
        {
            string bookUCI = ChessOpeningBook.GetBookMove(GameManager.Instance.PgnMoves);
            if (!string.IsNullOrEmpty(bookUCI))
            {
                bestMove = ChessOpeningBook.UCItoMove(BoardManager.Instance, bookUCI, ControlledColor);
                if (bestMove.HasValue)
                {
                    Debug.Log($"<color=cyan>[Opening Book]</color> Playing: {bookUCI}");
                }
            }
        }

        // If no book move, fall back to Minimax search
        if (!bestMove.HasValue)
        {
            bestMove = FindBestMove(depth, botIsWhite);
        }

        if (bestMove.HasValue)
        {
            bool ok = GameManager.Instance.TryMakeMove(bestMove.Value.piece, bestMove.Value.to);
            if (!ok)
                Debug.LogWarning("Bot: TryMakeMove failed for selected move.");
        }
        else
        {
            Debug.LogWarning("Bot: No legal moves found (stalemate or checkmate).");
        }

        thinking = false;
    }

    // ──────────────────────────────────────────────
    // Move search — root with move ordering
    // ──────────────────────────────────────────────
    private (ChessPiece piece, Vector2Int to)? FindBestMove(int depth, bool maximisingIsWhite)
    {
        BoardManager board = BoardManager.Instance;
        var allMoves = GatherAllMoves(board, ControlledColor);
        if (allMoves.Count == 0) return null;

        // Order moves at the root: captures first, then checks, then rest
        OrderMoves(board, allMoves, ControlledColor);

        int bestScore = int.MinValue;
        (ChessPiece piece, Vector2Int to)? bestMove = null;

        foreach (var move in allMoves)
        {
            var rec = board.SimulateMove(move.piece, move.to);
            int score = Minimax(board, depth - 1, int.MinValue, int.MaxValue,
                                /*maximising=*/ false, maximisingIsWhite);
            board.UndoSimulatedMove(rec);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        Debug.Log($"[Bot] Best move score: {bestScore}  depth: {depth}");
        return bestMove;
    }

    // ──────────────────────────────────────────────
    // Minimax with Alpha-Beta pruning
    // ──────────────────────────────────────────────
    private int Minimax(BoardManager board, int depth, int alpha, int beta, bool maximising, bool maximisingIsWhite)
    {
        if (depth == 0) return Evaluate(board, maximisingIsWhite);

        PieceColor sideToMove = maximising
            ? (maximisingIsWhite ? PieceColor.White : PieceColor.Black)
            : (maximisingIsWhite ? PieceColor.Black : PieceColor.White);

        var moves = GatherAllMoves(board, sideToMove);

        // Terminal: checkmate or stalemate
        if (moves.Count == 0)
        {
            bool inCheck = board.IsKingInCheck(sideToMove);
            if (inCheck)
            {
                // Prefer faster mates → subtract depth so shallower mate = higher score
                return maximising
                    ? -(CHECKMATE_SCORE + depth)
                    : (CHECKMATE_SCORE + depth);
            }
            return 0; // stalemate
        }

        // Move ordering inside tree (cheaper: just sort by capture MVV-LVA)
        OrderMoves(board, moves, sideToMove);

        if (maximising)
        {
            int maxEval = int.MinValue;
            foreach (var move in moves)
            {
                var rec = board.SimulateMove(move.piece, move.to);
                int eval = Minimax(board, depth - 1, alpha, beta, false, maximisingIsWhite);
                board.UndoSimulatedMove(rec);

                maxEval = Mathf.Max(maxEval, eval);
                alpha = Mathf.Max(alpha, eval);
                if (beta <= alpha) break; // β cut-off
            }
            return maxEval;
        }
        else
        {
            int minEval = int.MaxValue;
            foreach (var move in moves)
            {
                var rec = board.SimulateMove(move.piece, move.to);
                int eval = Minimax(board, depth - 1, alpha, beta, true, maximisingIsWhite);
                board.UndoSimulatedMove(rec);

                minEval = Mathf.Min(minEval, eval);
                beta = Mathf.Min(beta, eval);
                if (beta <= alpha) break; // α cut-off
            }
            return minEval;
        }
    }

    // ──────────────────────────────────────────────
    // Move Ordering — Phase 3
    // ──────────────────────────────────────────────
    /// <summary>
    /// Sorts moves in-place so the search explores promising moves first,
    /// dramatically improving alpha-beta cut-off efficiency.
    ///
    /// Priority (highest first):
    ///   1. Captures ordered by MVV-LVA  (capture a Queen with a Pawn = best)
    ///   2. Moves that put the opponent in check
    ///   3. All other quiet moves (shuffled to break ties randomly)
    /// </summary>
    private void OrderMoves(BoardManager board, List<(ChessPiece piece, Vector2Int to)> moves, PieceColor movingColor)
    {
        // Shuffle first so equal-score moves have random order (avoids repetition)
        Shuffle(moves);

        moves.Sort((a, b) =>
        {
            int scoreA = MoveOrderScore(board, a.piece, a.to, movingColor);
            int scoreB = MoveOrderScore(board, b.piece, b.to, movingColor);
            return scoreB.CompareTo(scoreA); // descending: higher score = try first
        });
    }

    private int MoveOrderScore(BoardManager board, ChessPiece piece, Vector2Int to, PieceColor movingColor)
    {
        int score = 0;

        // 1. Capture bonus: MVV-LVA
        //    Most Valuable Victim / Least Valuable Attacker
        //    Score = victimValue * 10 - attackerValue
        ChessPiece victim = board.GetPieceAt(to);
        if (victim != null && victim.pieceColor != movingColor)
        {
            int victimVal = GetPieceValue(victim.pieceType);
            int attackerVal = GetPieceValue(piece.pieceType);
            score += victimVal * 10 - attackerVal + 10000; // base 10000 to keep all captures above quiet moves
        }

        // 2. Check bonus: simulate the move and see if it gives check
        //    Only do this at shallow depths (expensive) — here we use a lightweight ray test
        //    rather than full simulation to keep ordering fast.
        PieceColor opponent = movingColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
        var rec = board.SimulateMove(piece, to);
        bool givesCheck = board.IsKingInCheck(opponent);
        board.UndoSimulatedMove(rec);
        if (givesCheck) score += 5000;

        return score;
    }

    // ──────────────────────────────────────────────
    // Evaluation — Phase 3
    // ──────────────────────────────────────────────
    /// <summary>
    /// Returns a score that is positive when White is better and negative when Black is better.
    /// If maximisingIsWhite is false (bot plays Black) the sign is flipped at the end
    /// so the bot always maximises a score where positive = "bot is winning".
    ///
    /// Components:
    ///   • Material balance (piece values)
    ///   • Piece-square table bonuses (positional)
    ///   • Check bonus (giving check = slightly better)
    ///   • Endgame detection → king activity + passed pawn + mop-up bonuses
    /// </summary>
    private int Evaluate(BoardManager board, bool maximisingIsWhite)
    {
        // ─── Endgame detection ───────────────────────────────────────────────
        // We're in the endgame when both sides have no queens, or one side's
        // material (excl. king & pawns) is very low.
        bool whiteQueenPresent = false;
        bool blackQueenPresent = false;
        int whiteMaterial = 0;
        int blackMaterial = 0;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece p = board.GetPieceAt(new Vector2Int(x, y));
                if (p == null) continue;
                if (p.pieceType == PieceType.Queen)
                {
                    if (p.pieceColor == PieceColor.White) whiteQueenPresent = true;
                    else blackQueenPresent = true;
                }
                if (p.pieceType != PieceType.King && p.pieceType != PieceType.Pawn)
                {
                    if (p.pieceColor == PieceColor.White) whiteMaterial += GetPieceValue(p.pieceType);
                    else blackMaterial += GetPieceValue(p.pieceType);
                }
            }
        }

        bool isEndgame = (!whiteQueenPresent && !blackQueenPresent)
                      || (whiteQueenPresent && whiteMaterial <= VAL_ROOK)
                      || (blackQueenPresent && blackMaterial <= VAL_ROOK);

        // ─── Main scoring loop ───────────────────────────────────────────────
        int score = 0;

        // Track king positions for mop-up heuristic
        Vector2Int whiteKingPos = new Vector2Int(4, 0);
        Vector2Int blackKingPos = new Vector2Int(4, 7);

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece p = board.GetPieceAt(new Vector2Int(x, y));
                if (p == null) continue;

                int pieceVal = GetPieceValue(p.pieceType);
                int pstBonus = GetPSTBonus(p, x, y, isEndgame);

                int contribution = pieceVal + pstBonus;
                if (p.pieceColor == PieceColor.White)
                {
                    score += contribution;
                    if (p.pieceType == PieceType.King) whiteKingPos = new Vector2Int(x, y);
                }
                else
                {
                    score -= contribution;
                    if (p.pieceType == PieceType.King) blackKingPos = new Vector2Int(x, y);
                }
            }
        }

        // ─── Check bonus ─────────────────────────────────────────────────────
        // Small bonus for putting the opponent in check
        if (board.IsKingInCheck(PieceColor.Black)) score += CHECK_BONUS;
        if (board.IsKingInCheck(PieceColor.White)) score -= CHECK_BONUS;

        // ─── Endgame: Passed Pawn bonus ──────────────────────────────────────
        if (isEndgame)
        {
            score += EvaluatePassedPawns(board, PieceColor.White);
            score -= EvaluatePassedPawns(board, PieceColor.Black);
        }

        // ─── Endgame: Mop-up (push losing king to corner) ────────────────────
        // When we're clearly winning, push opponent king to edge/corner.
        if (isEndgame)
        {
            // Positive score → White winning → push Black king to corner
            if (score > 200)
            {
                score += MopUpScore(whiteKingPos, blackKingPos);
            }
            // Negative score → Black winning → push White king to corner
            else if (score < -200)
            {
                score -= MopUpScore(blackKingPos, whiteKingPos);
            }
        }

        // Flip sign if bot plays Black
        return maximisingIsWhite ? score : -score;
    }

    // ──────────────────────────────────────────────
    // Piece-Square Table lookup
    // ──────────────────────────────────────────────
    private int GetPSTBonus(ChessPiece p, int x, int y, bool isEndgame)
    {
        // White PSTs are indexed with y=0 at White's back rank.
        // For Black pieces we mirror the row (7-y).
        int row = (p.pieceColor == PieceColor.White) ? y : (7 - y);

        switch (p.pieceType)
        {
            case PieceType.Pawn: return PST_Pawn[row, x];
            case PieceType.Knight: return PST_Knight[row, x];
            case PieceType.Bishop: return PST_Bishop[row, x];
            case PieceType.Rook: return PST_Rook[row, x];
            case PieceType.Queen: return PST_Queen[row, x];
            case PieceType.King: return isEndgame ? PST_King_End[row, x] : PST_King_Mid[row, x];
            default: return 0;
        }
    }

    // ──────────────────────────────────────────────
    // Passed Pawn evaluation — Phase 3 endgame
    // ──────────────────────────────────────────────
    /// <summary>
    /// A passed pawn is a pawn with no opposing pawns blocking it or on adjacent files ahead.
    /// Bonuses scale with how advanced the pawn is.
    /// </summary>
    private int EvaluatePassedPawns(BoardManager board, PieceColor color)
    {
        int bonus = 0;
        PieceColor opponent = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        int forward = (color == PieceColor.White) ? 1 : -1;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece p = board.GetPieceAt(new Vector2Int(x, y));
                if (p == null || p.pieceType != PieceType.Pawn || p.pieceColor != color)
                    continue;

                bool passed = true;

                // Check all squares ahead (including flanking files) for opponent pawns
                for (int yAhead = y + forward; yAhead >= 0 && yAhead < 8; yAhead += forward)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= 8) continue;
                        ChessPiece blocker = board.GetPieceAt(new Vector2Int(nx, yAhead));
                        if (blocker != null && blocker.pieceType == PieceType.Pawn && blocker.pieceColor == opponent)
                        {
                            passed = false;
                            break;
                        }
                    }
                    if (!passed) break;
                }

                if (passed)
                {
                    // More advanced = bigger bonus
                    int advancement = (color == PieceColor.White) ? y : (7 - y);
                    bonus += 20 + advancement * 15;
                }
            }
        }
        return bonus;
    }

    // ──────────────────────────────────────────────
    // Mop-up score — Phase 3 endgame
    // ──────────────────────────────────────────────
    /// <summary>
    /// Encourages the winning king to approach the losing king (to assist mating),
    /// and rewards pushing the losing king to the edge/corner.
    /// </summary>
    private int MopUpScore(Vector2Int ourKing, Vector2Int theirKing)
    {
        // Reward pushing their king to the edge
        int centerDist = Mathf.Abs(theirKing.x - 3) + Mathf.Abs(theirKing.y - 3);
        int edgeBonus = centerDist * 10;   // further from center = bigger bonus

        // Reward our king approaching their king (for mating net)
        int kingDist = Mathf.Abs(ourKing.x - theirKing.x) + Mathf.Abs(ourKing.y - theirKing.y);
        int proximity = (14 - kingDist) * 4;  // closer = bigger bonus

        return edgeBonus + proximity;
    }

    // ──────────────────────────────────────────────
    // Move generation
    // ──────────────────────────────────────────────
    private List<(ChessPiece piece, Vector2Int to)> GatherAllMoves(BoardManager board, PieceColor color)
    {
        var result = new List<(ChessPiece, Vector2Int)>();
        var pieces = board.GetPieces(color);

        foreach (var piece in pieces)
        {
            var legalMoves = board.GetLegalMoves(piece);
            if (legalMoves == null) continue;
            foreach (var to in legalMoves)
                result.Add((piece, to));
        }
        return result;
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────
    private static int GetPieceValue(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return VAL_PAWN;
            case PieceType.Knight: return VAL_KNIGHT;
            case PieceType.Bishop: return VAL_BISHOP;
            case PieceType.Rook: return VAL_ROOK;
            case PieceType.Queen: return VAL_QUEEN;
            case PieceType.King: return VAL_KING;
            default: return 0;
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}