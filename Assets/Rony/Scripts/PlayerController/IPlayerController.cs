public interface IPlayerController
{
    // called by PlayerManager when the player should start their turn
    void OnTurnStarted();

    // called to cancel any in-progress thinking or input
    void OnTurnEnded();

    // configure the controller (optional)
    void Initialize(PieceColor color);
}