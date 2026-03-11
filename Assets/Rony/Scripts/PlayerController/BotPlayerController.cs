using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotPlayerController : MonoBehaviour, IPlayerController
{
    public PieceColor ControlledColor;
    public float ThinkDelay = 0.7f;    // seconds

    bool thinking = false;

    public void Initialize(PieceColor color)
    {
        ControlledColor = color;
    }

    public void OnTurnStarted()
    {
        // Only run if it's this bot's color
        if (GameManager.Instance.GetCurrentTurnColor() != ControlledColor) return;
        if (thinking) return;
        StartCoroutine(ThinkAndPlay());
    }

    public void OnTurnEnded()
    {
        // cancel any thinking in progress
        StopAllCoroutines();
        thinking = false;
    }

    IEnumerator ThinkAndPlay()
    {
        thinking = true;
        yield return new WaitForSeconds(ThinkDelay);

        // Gather all legal moves
        var moves = new List<(ChessPiece piece, Vector2Int to)>();

        foreach (var piece in BoardManager.Instance.GetPieces(ControlledColor))
        {
            var legal = BoardManager.Instance.GetLegalMoves(piece);
            if (legal == null || legal.Count == 0) continue;
            foreach (var to in legal) moves.Add((piece, to));
        }

        if (moves.Count == 0)
        {
            thinking = false;
            yield break;
        }

        var choice = moves[Random.Range(0, moves.Count)];

        // Use GameManager facade to perform the move
        bool ok = GameManager.Instance.TryMakeMove(choice.piece, choice.to);
        if (!ok)
            Debug.LogWarning("Bot: TryMakeMove failed for selected move.");

        thinking = false;
    }
}