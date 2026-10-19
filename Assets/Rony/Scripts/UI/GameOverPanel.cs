using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

/// <summary>
/// Attach to the GameOver Panel GameObject (initially deactivated).
///
/// Inspector wiring:
///   panelRoot        – RectTransform that will be scaled for the animation
///   canvasGroup      – CanvasGroup component on the same object (for fade)
///   resultText       – TextMeshProUGUI showing "White Wins!" / "Black Wins!" etc.
///   playAgainButton  – restarts the game with the exact same setup
///   mainMenuButton   – returns to main menu
///   quitButton       – exits the application
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.45f;
    [SerializeField] private Ease openEase = Ease.OutBack;

    [SerializeField] Button sceneHomeButton;
    [SerializeField] Button sceneQuitButton;


    // ──────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────

    private void Awake()
    {
        // Hide visually but keep active so Start() can run and subscribe
        if (panelRoot != null) panelRoot.localScale = Vector3.zero;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void Start()
    {
        // Wire buttons
        if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgain);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

        // Subscribe to game-over event
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver += Show;

        // Now safe to disable
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= Show;
    }

    // ──────────────────────────────────────────────
    // Show / Hide
    // ──────────────────────────────────────────────

    /// <summary>Called automatically via GameManager.OnGameOver event.</summary>
    public void Show(PieceColor losingKingColor)
    {
        // Set result text
        if (resultText != null)
        {
            PieceColor winner = losingKingColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            resultText.text = $"{winner} Wins!";
        }

        // Freeze the game so no more moves happen
        Time.timeScale = 0f;

        // Animate in (SetUpdate(true) so tweens work while timeScale == 0)
        gameObject.SetActive(true);
        panelRoot.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;

        panelRoot.DOScale(1f, animDuration).SetEase(openEase).SetUpdate(true);
        canvasGroup.DOFade(1f, animDuration).SetUpdate(true);

        sceneHomeButton.gameObject.SetActive(false);
        sceneQuitButton.gameObject.SetActive(false);
    }

    private void HideInstant()
    {
        if (panelRoot != null) panelRoot.localScale = Vector3.zero;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────────
    // Button Handlers
    // ──────────────────────────────────────────────

    private void OnPlayAgain()
    {
        sceneHomeButton.gameObject.SetActive(true);
        sceneQuitButton.gameObject.SetActive(true);
        Time.timeScale = 1f; // restore before loading scene
        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
        AudioManager.Instance.PlayRandomMusic();
    }

    private void OnMainMenu()
    {
        AudioManager.Instance.PlayRandomMusic();
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.GoToMainMenu();
    }

    private void OnQuit()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.QuitGame();
    }
}
