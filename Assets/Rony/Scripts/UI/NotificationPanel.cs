using TMPro;
using UnityEngine;
using DG.Tweening;
using System;

public class NotificationPanel : MonoBehaviour
{
    [SerializeField] TMP_Text messageText;

    [Header("Timings")]
    [SerializeField] float popUpDuration = 0.6f;
    [SerializeField] float popDownDuration = 0.35f;
    [SerializeField] float autoHideDelay = 2.5f;  // how long a normal "in check" message stays before hiding
    [SerializeField] float checkmateStay = 5f;      // how long checkmate message stays

    Vector3 originalScale;
    Tween currentTween;
    bool initialized = false;

    void Awake()
    {
        if (messageText == null) Debug.LogWarning("NotificationPanel: messageText not assigned in inspector.");
        // cache original scale so we can return to it
        originalScale = transform.localScale;
        initialized = true;
    }

    void Start()
    {
        // start hidden
        transform.localScale = Vector3.zero;
        messageText.text = "";
    }

    void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnKingChecked += OnKingChecked;
            GameManager.Instance.OnKingCleared += OnKingCleared;
            GameManager.Instance.OnGameOver += OnGameOver;
        }
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnKingChecked -= OnKingChecked;
            GameManager.Instance.OnKingCleared -= OnKingCleared;
            GameManager.Instance.OnGameOver -= OnGameOver;
        }
    }

    // Called by GameManager when a king is in check
    void OnKingChecked(PieceColor color)
    {
        string c = color == PieceColor.White ? "White" : "Black";
        ShowMessage($"{c} king is in check");
        // auto-hide after short delay (unless cleared earlier)
        DOVirtual.DelayedCall(autoHideDelay, () =>
        {
            // if the king is no longer checked (or it still is — but we respect the auto-hide behavior),
            // we pop down. If you prefer to never auto-hide while check persists, change condition below.
            PopDown();
        });
    }

    // Called by GameManager when no king is in check
    void OnKingCleared()
    {
        PopDown();
    }

    // Called when the game moves to GameOver (checkmate)
    void OnGameOver(PieceColor checkedKing)
    {
        string c = checkedKing == PieceColor.White ? "White" : "Black";
        ShowMessage($"{c} is checkmated! Game Over");
        // keep message longer
        DOVirtual.DelayedCall(checkmateStay, () =>
        {
            PopDown();
        });
    }

    // Public API — optional if you want to call manually elsewhere
    public void ShowMessage(string message)
    {
        messageText.text = message;
        PopUp();
    }

    // Pop up with a bounce (from 0 to originalScale)
    public void PopUp()
    {
        if (!initialized) Awake();

        // stop existing tweens and start from 0 (makes pop look consistent)
        transform.DOKill(true);
        transform.localScale = Vector3.zero;

        currentTween = transform
            .DOScale(originalScale, popUpDuration)
            .SetEase(Ease.OutBack);
    }

    // Pop down to zero with a small bounce-in
    public void PopDown()
    {
        transform.DOKill(true);
        currentTween = transform
            .DOScale(Vector3.zero, popDownDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                // clear text when hidden
                messageText.text = "";
            });
    }
}