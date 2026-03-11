using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    IPlayerController whiteController;
    IPlayerController blackController;
    List<IPlayerController> allControllers = new();

    void Start()
    {
        SetupPlayers();

        // Ensure all controllers are "ended" (disable input etc) before starting first turn
        foreach (var c in allControllers) c.OnTurnEnded();

        // Subscribe to future turns
        if (GameManager.Instance != null)
            GameManager.Instance.OnTurnStarted += OnTurnStarted;

        // Fire initial turn for whatever GameManager.CurrentState already is
        if (GameManager.Instance != null)
        {
            PieceColor first = GameManager.Instance.GetCurrentTurnColor();
            OnTurnStarted(first);
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    void SetupPlayers()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("PlayerManager: GameManager.Instance is null.");
            return;
        }

        // create white controller
        whiteController = CreateControllerFor(gm.WhitePlayer, PieceColor.White);
        if (whiteController != null) allControllers.Add(whiteController);

        // create black controller
        blackController = CreateControllerFor(gm.BlackPlayer, PieceColor.Black);
        if (blackController != null) allControllers.Add(blackController);
    }

    IPlayerController CreateControllerFor(PlayerType type, PieceColor color)
    {
        GameObject go = new GameObject($"{type}Controller_{color}");
        go.transform.SetParent(transform, false);

        IPlayerController controller = null;
        switch (type)
        {
            case PlayerType.Human:
                controller = go.AddComponent<HumanPlayerController>() as IPlayerController;
                break;
            case PlayerType.Bot:
                controller = go.AddComponent<BotPlayerController>() as IPlayerController;
                break;
            case PlayerType.Network:
                controller = go.AddComponent<NetworkPlayerController>() as IPlayerController;
                break;
        }
        if (controller != null) controller.Initialize(color);
        return controller;
    }

    // Called when GameManager fires OnTurnStarted(color)
    void OnTurnStarted(PieceColor color)
    {
        // End everything first (disables input, stops bots thinking)
        foreach (var c in allControllers) c.OnTurnEnded();

        // Start the right controller
        if (color == PieceColor.White)
        {
            whiteController?.OnTurnStarted();
        }
        else
        {
            blackController?.OnTurnStarted();
        }
    }
}