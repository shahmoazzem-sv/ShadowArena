using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI player using Minimax with Alpha-Beta pruning.
/// Difficulty levels drive search depth:
///   Easy   → depth 1-2
///   Medium → depth 3
///   Hard   → depth 4+
/// Evaluation is material-based with a small center-control positional bonus.
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
    private const int VAL_PAWN   =  100;
    private const int VAL_KNIGHT =  300;
    private const int VAL_BISHOP =  300;
    private const int VAL_ROOK   =  500;
    private const int VAL_QUEEN  =  900;
    private const int VAL_KING   = 20000;

    // Centre squares get a tiny positional bonus
    private static readonly int[,] CenterBonus = {
        // x →  0    1    2    3    4    5    6    7
        /* y=0 */ { 0,   0,   0,   0,   0,   0,   0,   0 },
        /* y=1 */ { 0,   5,   5,   5,   5,   5,   5,   0 },
        /* y=2 */ { 0,   5,  10,  10,  10,  10,   5,   0 },
        /* y=3 */ { 0,   5,  10,  15,  15,  10,   5,   0 },
        /* y=4 */ { 0,   5,  10,  15,  15,  10,   5,   0 },
        /* y=5 */ { 0,   5,  10,  10,  10,  10,   5,   0 },
        /* y=6 */ { 0,   5,   5,   5,   5,   5,   5,   0 },
        /* y=7 */ { 0,   0,   0,   0,   0,   0,   0,   0 },
    };

    bool thinking = false;

    // ──────────────────────────────────────────────
    // IPlayerController interface
    // ──────────────────────────────────────────────
    public void Initialize(PieceColor color)
    {
        ControlledColor = color;

        // Pull difficulty from GameManager so a MainMenu selection takes effect.
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

        // Depth from difficulty
        int depth = BotDifficulty == Difficulty.Easy   ? 2 :
                    BotDifficulty == Difficulty.Medium  ? 3 : 4;

        // Bot is always the maximising player from its own colour perspective.
        bool botIsWhite = (ControlledColor == PieceColor.White);

        var bestMove = FindBestMove(depth, botIsWhite);

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
    // Move search
    // ──────────────────────────────────────────────
    private (ChessPiece piece, Vector2Int to)? FindBestMove(int depth, bool maximisingIsWhite)
    {
        BoardManager board = BoardManager.Instance;
        var allMoves = GatherAllMoves(board, ControlledColor);

        if (allMoves.Count == 0) return null;

        int bestScore = int.MinValue;
        (ChessPiece piece, Vector2Int to)? bestMove = null;

        // Shuffle to avoid always picking the same move when scores are equal
        Shuffle(allMoves);

        foreach (var move in allMoves)
        {
            var rec = board.SimulateMove(move.piece, move.to);

            PieceColor opponent = ControlledColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
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
    /// <param name="maximisingIsWhite">
    ///   True  → maximising node evaluates White advantage (White is the bot).
    ///   False → maximising node evaluates Black advantage (Black is the bot).
    ///   When the bot is Black, maximising means we want a *low* raw score
    ///   (from White's perspective), so we flip internally.
    /// </param>
    private int Minimax(BoardManager board, int depth, int alpha, int beta, bool maximising, bool maximisingIsWhite)
    {
        if (depth == 0) return Evaluate(board, maximisingIsWhite);

        // Determine whose turn it is in this simulated node
        PieceColor sideToMove = maximising
            ? (maximisingIsWhite ? PieceColor.White : PieceColor.Black)
            : (maximisingIsWhite ? PieceColor.Black : PieceColor.White);

        var moves = GatherAllMoves(board, sideToMove);

        // Terminal node (no legal moves) = checkmate or stalemate
        if (moves.Count == 0)
        {
            bool inCheck = board.IsKingInCheck(sideToMove);
            if (inCheck)
            {
                // Current side is checkmated – worst result for the side to move
                return maximising ? int.MinValue + 1 : int.MaxValue - 1;
            }
            return 0; // stalemate
        }

        if (maximising)
        {
            int maxEval = int.MinValue;
            foreach (var move in moves)
            {
                var rec = board.SimulateMove(move.piece, move.to);
                int eval = Minimax(board, depth - 1, alpha, beta, false, maximisingIsWhite);
                board.UndoSimulatedMove(rec);

                maxEval = Mathf.Max(maxEval, eval);
                alpha   = Mathf.Max(alpha, eval);
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
                beta    = Mathf.Min(beta, eval);
                if (beta <= alpha) break; // α cut-off
            }
            return minEval;
        }
    }

    // ──────────────────────────────────────────────
    // Evaluation
    // ──────────────────────────────────────────────
    /// <summary>
    /// Positive score is good for White.  
    /// If the bot plays Black we negate at the call site.
    /// </summary>
    private int Evaluate(BoardManager board, bool maximisingIsWhite)
    {
        int score = 0;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece p = board.GetPieceAt(new Vector2Int(x, y));
                if (p == null) continue;

                int pieceVal = GetPieceValue(p.pieceType);
                int posBonus = CenterBonus[y, x]; // [row, col]

                int contribution = pieceVal + posBonus;
                if (p.pieceColor == PieceColor.White) score += contribution;
                else                                   score -= contribution;
            }
        }

        // If bot is Black, flip sign so higher = better for bot
        return maximisingIsWhite ? score : -score;
    }

    private static int GetPieceValue(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn:   return VAL_PAWN;
            case PieceType.Knight: return VAL_KNIGHT;
            case PieceType.Bishop: return VAL_BISHOP;
            case PieceType.Rook:   return VAL_ROOK;
            case PieceType.Queen:  return VAL_QUEEN;
            case PieceType.King:   return VAL_KING;
            default:               return 0;
        }
    }

    // ──────────────────────────────────────────────
    // Move generation helpers
    // ──────────────────────────────────────────────
    // We use pseudo-legal moves here during minimax simulation to keep it fast;
    // the existing GetLegalMoves already filters via SimulateMove internally,
    // which would cause deeply nested simulations and slow everything down.
    // Instead we use GetValidMoves (pseudo-legal) for tree nodes and rely on
    // the king-still-in-check check in GetLegalMoves only for the top-level move.
    //
    // NOTE: For correctness at depth we DO use GetLegalMoves so that the bot
    // never picks an illegal move itself. The inner minimax uses pseudo-legal
    // moves which is the standard practice for alpha-beta engines.
    private List<(ChessPiece piece, Vector2Int to)> GatherAllMoves(BoardManager board, PieceColor color)
    {
        var result = new List<(ChessPiece, Vector2Int)>();
        var pieces = board.GetPieces(color);

        foreach (var piece in pieces)
        {
            // Use GetLegalMoves which filters illegal moves via simulation.
            // This is slightly slower but guarantees correctness.
            var legalMoves = board.GetLegalMoves(piece);
            if (legalMoves == null) continue;
            foreach (var to in legalMoves)
                result.Add((piece, to));
        }
        return result;
    }

    // ──────────────────────────────────────────────
    // Utilities
    // ──────────────────────────────────────────────
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}