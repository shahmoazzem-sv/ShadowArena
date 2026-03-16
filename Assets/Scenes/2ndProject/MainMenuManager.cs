using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{

    [SerializeField] private Button playButton;
    [SerializeField] private Button PlayWithBotButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    private void Start()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(PlayHumanVsHuman);
        }
            
        if (PlayWithBotButton != null)
        {
            PlayWithBotButton.onClick.RemoveAllListeners();
            PlayWithBotButton.onClick.AddListener(PlayHumanVsBot);
        }
            
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    // Usually maps to Human vs Human
    public void PlayHumanVsHuman()
    {
        GameManager.PendingGameMode = GameMode.HumanVsHuman;
        GameManager.PendingWhitePlayer = PlayerType.Human;
        GameManager.PendingBlackPlayer = PlayerType.Human;
        SceneManager.LoadScene(1);
    }

    // Maps to Human vs Bot
    public void PlayHumanVsBot()
    {
        GameManager.PendingGameMode = GameMode.HumanVsBot;
        GameManager.PendingWhitePlayer = PlayerType.Human;
        GameManager.PendingBlackPlayer = PlayerType.Bot; // Assuming Bot plays black
        SceneManager.LoadScene(1);
    }

    // Kept for UI buttons already linked in Inspector to PlayGame
    public void PlayGame()
    {
        PlayHumanVsHuman();
    }
}
