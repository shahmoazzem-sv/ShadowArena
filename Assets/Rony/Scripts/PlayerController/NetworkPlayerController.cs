using UnityEngine;

public class NetworkPlayerController : MonoBehaviour, IPlayerController
{
    public PieceColor ControlledColor;

    public void Initialize(PieceColor color) => ControlledColor = color;

    public void OnTurnStarted()
    {
        // For a remote opponent, disable local input and wait for incoming move packets.
        // For a local "network host" player, you might open a UI to confirm a move then send to peers.
        if (ControlledColor != GameManager.Instance.GetCurrentTurnColor()) return;

        // TODO: hook into networking system. When a remote move arrives, call:
        // GameManager.Instance.TryMakeMove(piece, to);
    }

    public void OnTurnEnded()
    {
        // nothing by default
    }
}