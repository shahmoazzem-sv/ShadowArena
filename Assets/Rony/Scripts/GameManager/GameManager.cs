using UnityEngine;


public enum GameState
{
    Initializing,
    WhiteTurn,
    BlackTurn,
    GameOver
}
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState CurrentState { get; private set; }

    // Which king (if any) is currently checked. null = no king in check.
    public PieceColor? CheckedKing { get; private set; } = null;

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
        // Set a safe default early so other Start() methods can rely on it if needed.
        CurrentState = GameState.Initializing;
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
    }

    private void HandleKingInCheck(PieceColor kingColor)
    {
        Debug.Log($"⚠ {kingColor} King is under CHECK!");
        // keep track of it:
        CheckedKing = kingColor;

        // You may want to show UI, sound, etc.
    }

    // Clear or set the checked king programmatically (used by BoardManager.UpdateCheckStatus)
    public void SetCheckedKing(PieceColor? color)
    {
        CheckedKing = color;
        if (color == null)
        {
            Debug.Log("No king is in check.");
        }
        else
        {
            Debug.Log($"{color} king is in check (SetCheckedKing).");
        }
    }

    // A helper method to easily swap turns after a valid move
    public void EndTurn()
    {
        if (CurrentState == GameState.WhiteTurn)
        {
            ChangeState(GameState.BlackTurn);
        }
        else if (CurrentState == GameState.BlackTurn)
        {
            ChangeState(GameState.WhiteTurn);
        }
        else
        {
            // If not in a player turn state, ignore (or set a default)
            Debug.LogWarning("EndTurn called while not in a player-turn state.");
        }
    }


}
