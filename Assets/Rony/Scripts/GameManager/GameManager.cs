using UnityEngine;
using System.Collections.Generic;
using System;


public enum GameState
{
    Initializing,
    WhiteTurn,
    BlackTurn,
    GameOver
}

public enum PlayerType { Human, Bot, Network }
public enum GameMode { HumanVsHuman, HumanVsBot, BotVsBot, Networked }
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState CurrentState { get; private set; }

    // Which king (if any) is currently checked. null = no king in check.
    public PieceColor? CheckedKing { get; private set; } = null;

    [Header("Game History")]
    public int FullMoveNumber { get; private set; } = 1;
    public int HalfMoveClock { get; private set; } = 0;
    public string CurrentFEN { get; private set; }

    // Stores the sequence of SAN strings (e.g., ["e4", "e5", "Nf3"])
    public List<string> PgnMoves { get; private set; } = new List<string>();


    // Action passes: (FullMoveNumber, SAN string, isWhiteTurn)
    public event Action<int, string, bool> OnMoveRecorded;
    public event Action<PieceColor> OnKingChecked;    // invoked when a king is found to be in check
    public event Action OnKingCleared;                // invoked when no king is in check anymore
    public event Action<PieceColor> OnGameOver;       // invoked when game state moves to GameOver (passes checked king color)
    public event Action<PieceColor> OnInvalidMoveInCheck;


    [Header("Player Setup")]
    public GameMode CurrentGameMode = GameMode.HumanVsBot;
    public PlayerType WhitePlayer = PlayerType.Human;
    public PlayerType BlackPlayer = PlayerType.Bot;

    // Static variables to pass setup from Main Menu to Game scene
    public static GameMode? PendingGameMode = null;
    public static PlayerType? PendingWhitePlayer = null;
    public static PlayerType? PendingBlackPlayer = null;

    // fired whenever a new turn begins (argument = color to move)
    public event Action<PieceColor> OnTurnStarted;

    private void Awake()
    {
        // Standard Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Apply pending setup from Main Menu if it exists
        if (PendingGameMode.HasValue) CurrentGameMode = PendingGameMode.Value;
        if (PendingWhitePlayer.HasValue) WhitePlayer = PendingWhitePlayer.Value;
        if (PendingBlackPlayer.HasValue) BlackPlayer = PendingBlackPlayer.Value;

        // Reset so they don't affect future game plays directly started from editor
        PendingGameMode = null;
        PendingWhitePlayer = null;
        PendingBlackPlayer = null;

        // Set a safe default early so other Start() methods can rely on it if needed.
        CurrentState = GameState.Initializing;
    }


    // Call this to initialize the FEN at the very start of the game
    public void InitializeHistory(string startingFEN)
    {
        CurrentFEN = startingFEN;
        FullMoveNumber = 1;
        HalfMoveClock = 0;
        PgnMoves.Clear();
    }

    // Called by BoardManager after a move is finalized
    public void RecordMoveInfo(string san, string fen, bool isPawnMoveOrCapture)
    {
        PgnMoves.Add(san);
        CurrentFEN = fen;

        // FEN Rules: HalfMoveClock resets to 0 on a pawn move or capture. Otherwise increments.
        if (isPawnMoveOrCapture) HalfMoveClock = 0;
        else HalfMoveClock++;

        // Capture state before incrementing FullMoveNumber for UI purposes
        bool wasWhiteMove = (CurrentState == GameState.WhiteTurn);

        // Notify History Board BEFORE we increment the FullMoveNumber if it was black's turn
        OnMoveRecorded?.Invoke(FullMoveNumber, san, wasWhiteMove);

        // FEN Rules: FullMove increments ONLY after Black completes their turn
        if (CurrentState == GameState.BlackTurn) FullMoveNumber++;

        // Optional: Trigger a UI event here
        Debug.Log($"<color=orange>Move {FullMoveNumber}: {san} | FEN: {CurrentFEN}</color>");


    }

    private void OnEnable()
    {
        // Subscribe to board events when board exists
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnKingInCheck += HandleKingInCheck;
    }

    private void OnDisable()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnKingInCheck -= HandleKingInCheck;
    }

    private void Start()
    {
        // ChangeState(GameState.Initializing);
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"<color=cyan>Game State Changed to: {CurrentState}</color>");

        // Here you can trigger events based on state changes later
        // e.g., if (newState == GameState.WhiteTurn) UIManager.UpdateTurnText("White's Turn");
        // If game becomes GameOver, fire OnGameOver with the currently checked king (if any).
        if (newState == GameState.GameOver)
        {
            if (CheckedKing.HasValue)
                OnGameOver?.Invoke(CheckedKing.Value);
            else
                OnGameOver?.Invoke(PieceColor.White); // fallback if you want a default (optional)
        }
    }

    private void HandleKingInCheck(PieceColor kingColor)
    {
        Debug.Log($"⚠ {kingColor} King is under CHECK!");
        // keep track of it:
        CheckedKing = kingColor;

        // You may want to show UI, sound, etc.
        // UI / sound hooks:
        OnKingChecked?.Invoke(kingColor);
    }

    public void TriggerInvalidMoveInCheck(PieceColor color)
    {
        OnInvalidMoveInCheck?.Invoke(color);
    }

    // Clear or set the checked king programmatically (used by BoardManager.UpdateCheckStatus)
    public void SetCheckedKing(PieceColor? color)
    {
        CheckedKing = color;
        if (color == null)
        {
            Debug.Log("No king is in check.");
            OnKingCleared?.Invoke();
        }
        else
        {
            Debug.Log($"{color} king is in check (SetCheckedKing).");
            OnKingChecked?.Invoke(color.Value);

        }
    }

    // A helper method to easily swap turns after a valid move
    public void EndTurn()
    {
        if (CurrentState == GameState.WhiteTurn)
        {
            ChangeState(GameState.BlackTurn);
            OnTurnStarted?.Invoke(PieceColor.Black);

        }
        else if (CurrentState == GameState.BlackTurn)
        {
            ChangeState(GameState.WhiteTurn);
            OnTurnStarted?.Invoke(PieceColor.White);
        }
        else
        {
            // If not in a player turn state, ignore (or set a default)
            Debug.LogWarning("EndTurn called while not in a player-turn state.");
        }
    }

    // UI HELPER: Get the exact string for the UI (e.g. "Move 5")
    public string GetUIMoveNumber() => $"Move {FullMoveNumber}";

    // UI HELPER: Get the last move PGN text
    public string GetUILastMove() => PgnMoves.Count > 0 ? PgnMoves[PgnMoves.Count - 1] : "None";

    public bool IsWhiteTurn() => CurrentState == GameState.WhiteTurn ? true : false;

    //AI helper 
    public bool IsBotTurn()
    {
        if (CurrentState == GameState.WhiteTurn) return WhitePlayer == PlayerType.Bot;
        if (CurrentState == GameState.BlackTurn) return BlackPlayer == PlayerType.Bot;
        return false;
    }
    public PieceColor GetCurrentTurnColor()
    {
        if (CurrentState == GameState.WhiteTurn) return PieceColor.White;
        if (CurrentState == GameState.BlackTurn) return PieceColor.Black;
        return PieceColor.White; // fallback
    }

    // Try to perform the move; returns true on success.
    public bool TryMakeMove(ChessPiece piece, Vector2Int to)
    {
        // Safety checks
        if (piece == null) return false;
        PieceColor turnColor = GetCurrentTurnColor();
        if (piece.pieceColor != turnColor)
        {
            Debug.LogWarning("TryMakeMove: attempted to move when it's not the piece's turn.");
            return false;
        }

        // Ask BoardManager for legal moves (should already filter by check/pins)
        var legal = BoardManager.Instance.GetLegalMoves(piece);
        if (legal == null || legal.Count == 0) return false;
        if (!legal.Contains(to)) return false;

        // Perform the move via BoardManager (BoardManager should perform model update & animations)
        // BoardManager handles all logging, history updates, and ending turns now.
        bool moved = BoardManager.Instance.TryMovePiece(piece, to);
        if (!moved) return false;

        return true;
    }


}
