using UnityEngine;
using UnityEngine.UI;
using System;

public class PromotionUI : MonoBehaviour
{
    [SerializeField] private Button queenButton;
    [SerializeField] private Button rookButton;
    [SerializeField] private Button bishopButton;
    [SerializeField] private Button knightButton;

    // optional: set icons on buttons (sprites)
    [SerializeField] private UnityEngine.UI.Image queenImage;
    [SerializeField] private UnityEngine.UI.Image rookImage;
    [SerializeField] private UnityEngine.UI.Image bishopImage;
    [SerializeField] private UnityEngine.UI.Image knightImage;

    private Action<PieceType> callback;

    private void Awake()
    {
        gameObject.SetActive(false);

        queenButton.onClick.AddListener(() => OnChoice(PieceType.Queen));
        rookButton.onClick.AddListener(() => OnChoice(PieceType.Rook));
        bishopButton.onClick.AddListener(() => OnChoice(PieceType.Bishop));
        knightButton.onClick.AddListener(() => OnChoice(PieceType.Knight));
    }

    public void Show(PieceColor color, Action<PieceType> onChoice)
    {
        callback = onChoice;

        // optionally set icons based on color using your PieceData if available
        // e.g. queenImage.sprite = pieceData.GetSprite(PieceType.Queen, color);
        gameObject.SetActive(true);
    }

    private void OnChoice(PieceType chosen)
    {
        gameObject.SetActive(false);
        callback?.Invoke(chosen);
    }
}