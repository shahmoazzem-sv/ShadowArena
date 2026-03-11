using UnityEngine;

public class HumanPlayerController : MonoBehaviour, IPlayerController
{
    public PieceColor ControlledColor;

    public void Initialize(PieceColor color) => ControlledColor = color;

    public void OnTurnStarted()
    {
        // Enable input when it's this player's turn
        if (InputManager.Instance != null) InputManager.Instance.gameObject.SetActive(true);
    }

    public void OnTurnEnded()
    {
        // Disable input so clicks won't interfere while bots/network are moving
        if (InputManager.Instance != null) InputManager.Instance.gameObject.SetActive(false);
    }
}