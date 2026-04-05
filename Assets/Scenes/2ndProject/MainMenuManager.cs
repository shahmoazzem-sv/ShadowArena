using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the Main-Menu scene including the Play Setup panel.
///
/// ── Inspector wiring guide ────────────────────────────────────────────────
///  Main Menu buttons
///    playButton            → opens PlaySetupPanel
///    quitButton            → quits application
///
///  Play Setup Panel (initially deactivated)
///    playSetupPanel        → root panel GameObject
///    closeSetupButton      → deactivates playSetupPanel
///
///    humanVsHumanButton    → selects HvH mode, hides botSetupPanel, goes to game
///    humanVsBotButton      → selects HvB mode, shows botSetupPanel
///    (botVsBotButton – reserved, wired when needed)
///
///  Bot Setup Panel (child of playSetupPanel, initially deactivated)
///    botSetupPanel
///
///    Difficulty selectors (each has an "active indicator" child you toggle):
///      diffEasyButton / diffEasyIndicator
///      diffMediumButton / diffMediumIndicator
///      diffHardButton  / diffHardIndicator
///
///    Player colour selectors (bot gets the opposite colour):
///      botWhiteButton / botWhiteIndicator   ← "Play as White"
///      botBlackButton / botBlackIndicator   ← "Play as Black"
///
///    finalPlayButton  → applies all settings → loads game scene
/// ─────────────────────────────────────────────────────────────────────────
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ── Main menu ──────────────────────────────────────────────────────────
    [Header("Main Menu Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    // ── Play Setup Panel ───────────────────────────────────────────────────
    [Header("Play Setup Panel")]
    [SerializeField] private GameObject playSetupPanel;
    [SerializeField] private Button    closeSetupButton;

    [Header("Game Mode Buttons")]
    [SerializeField] private Button humanVsHumanButton;
    [SerializeField] private Button humanVsBotButton;
    // [SerializeField] private Button botVsBotButton;  // un-comment when ready

    // ── Bot Setup Sub-panel ────────────────────────────────────────────────
    [Header("Bot Setup Panel")]
    [SerializeField] private GameObject botSetupPanel;

    [Header("Difficulty Buttons")]
    [SerializeField] private Button     diffEasyButton;
    [SerializeField] private GameObject diffEasyIndicator;
    [SerializeField] private Button     diffMediumButton;
    [SerializeField] private GameObject diffMediumIndicator;
    [SerializeField] private Button     diffHardButton;
    [SerializeField] private GameObject diffHardIndicator;

    [Header("Player Color Buttons  (bot gets the opposite)")]
    [SerializeField] private Button     botWhiteButton;       // "Play as White"
    [SerializeField] private GameObject botWhiteIndicator;
    [SerializeField] private Button     botBlackButton;       // "Play as Black"
    [SerializeField] private GameObject botBlackIndicator;

    [Header("Final Play")]
    [SerializeField] private Button finalPlayButton;

    [Header("Scene")]
    [SerializeField] private int gameSceneIndex = 1;

    // ── Internal state ─────────────────────────────────────────────────────
    private BotPlayerController.Difficulty selectedDifficulty  = BotPlayerController.Difficulty.Easy;
    // This is the PLAYER colour; the bot automatically gets the opposite.
    private PieceColor                     selectedPlayerColor = PieceColor.White;

    // ──────────────────────────────────────────────────────────────────────
    private void Start()
    {
        // Main menu
        AddListener(playButton,  OpenPlaySetup);
        AddListener(settingsButton, OpenSettings);
        AddListener(quitButton,  QuitGame);

        // Setup panel
        AddListener(closeSetupButton,   ClosePlaySetup);
        AddListener(humanVsHumanButton, OnHumanVsHuman);
        AddListener(humanVsBotButton,   OnHumanVsBot);

        // Difficulty
        AddListener(diffEasyButton,   () => SelectDifficulty(BotPlayerController.Difficulty.Easy));
        AddListener(diffMediumButton, () => SelectDifficulty(BotPlayerController.Difficulty.Medium));
        AddListener(diffHardButton,   () => SelectDifficulty(BotPlayerController.Difficulty.Hard));

        // Player colour (bot gets the opposite)
        AddListener(botWhiteButton, () => SelectPlayerColor(PieceColor.White));
        AddListener(botBlackButton, () => SelectPlayerColor(PieceColor.Black));

        // Final play
        AddListener(finalPlayButton, LaunchHumanVsBot);

        // Initial panel states
        if (playSetupPanel != null) playSetupPanel.SetActive(false);
        if (botSetupPanel  != null) botSetupPanel.SetActive(false);

        // Apply defaults so indicators are correct on first open
        RefreshDifficultyIndicators();
        RefreshColorIndicators();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Main menu actions
    // ══════════════════════════════════════════════════════════════════════
    public void OpenPlaySetup()
    {
        if (playSetupPanel != null) playSetupPanel.SetActive(true);
        if (botSetupPanel  != null) botSetupPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        if (SettingsPanel.Instance != null)
        {
            SettingsPanel.Instance.Open();
        }
    }

    public void ClosePlaySetup()
    {
        if (playSetupPanel != null) playSetupPanel.SetActive(false);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
    // Game-mode selection
    // ══════════════════════════════════════════════════════════════════════
    public void OnHumanVsHuman()
    {
        // No bot config needed – launch immediately
        if (botSetupPanel != null) botSetupPanel.SetActive(false);
        LaunchHumanVsHuman();
    }

    public void OnHumanVsBot()
    {
        // Show bot configuration sub-panel
        if (botSetupPanel != null) botSetupPanel.SetActive(true);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Difficulty selection
    // ══════════════════════════════════════════════════════════════════════
    public void SelectDifficulty(BotPlayerController.Difficulty diff)
    {
        selectedDifficulty = diff;
        RefreshDifficultyIndicators();
    }

    private void RefreshDifficultyIndicators()
    {
        SetActive(diffEasyIndicator,   selectedDifficulty == BotPlayerController.Difficulty.Easy);
        SetActive(diffMediumIndicator, selectedDifficulty == BotPlayerController.Difficulty.Medium);
        SetActive(diffHardIndicator,   selectedDifficulty == BotPlayerController.Difficulty.Hard);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Player colour selection  (bot gets the opposite automatically)
    // ══════════════════════════════════════════════════════════════════════
    public void SelectPlayerColor(PieceColor color)
    {
        selectedPlayerColor = color;
        RefreshColorIndicators();
    }

    private void RefreshColorIndicators()
    {
        // Indicator lights up when that colour belongs to the PLAYER.
        SetActive(botWhiteIndicator, selectedPlayerColor == PieceColor.White);
        SetActive(botBlackIndicator, selectedPlayerColor == PieceColor.Black);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scene launching
    // ══════════════════════════════════════════════════════════════════════
    public void LaunchHumanVsHuman()
    {
        GameManager.PendingGameMode    = GameMode.HumanVsHuman;
        GameManager.PendingWhitePlayer = PlayerType.Human;
        GameManager.PendingBlackPlayer = PlayerType.Human;
        GameManager.PendingBotDifficulty = null; // no bot
        SceneManager.LoadScene(gameSceneIndex);
    }

    public void LaunchHumanVsBot()
    {
        GameManager.PendingGameMode = GameMode.HumanVsBot;

        // selectedPlayerColor = which colour the HUMAN plays.
        // The bot always gets the opposite colour.
        if (selectedPlayerColor == PieceColor.White)
        {
            GameManager.PendingWhitePlayer = PlayerType.Human;
            GameManager.PendingBlackPlayer = PlayerType.Bot;
        }
        else  // player plays Black → bot plays White
        {
            GameManager.PendingWhitePlayer = PlayerType.Bot;
            GameManager.PendingBlackPlayer = PlayerType.Human;
        }

        GameManager.PendingBotDifficulty = selectedDifficulty;
        GameManager.PendingPlayerColor   = selectedPlayerColor; // board perspective
        SceneManager.LoadScene(gameSceneIndex);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Legacy wrappers (keep so old inspector buttons don't break)
    // ══════════════════════════════════════════════════════════════════════
    public void PlayHumanVsHuman() => LaunchHumanVsHuman();
    public void PlayHumanVsBot()   => LaunchHumanVsBot();
    public void PlayGame()         => OpenPlaySetup();

    // ══════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════
    private static void AddListener(Button btn, System.Action action)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => action());
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
