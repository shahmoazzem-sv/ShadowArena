using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SettingsPanel : MonoBehaviour
{
    public static SettingsPanel Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.4f;
    [SerializeField] private Ease easeType = Ease.OutBack;

    private void Awake()
    {
        // Singleton setup for all scenes
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Hide by default on start
            HideInstant();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Add listeners
        musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        closeButton.onClick.AddListener(Close);
    }

    private void Start()
    {
        // Synchronize sliders with initial AudioManager values
        if (AudioManager.Instance != null)
        {
            musicSlider.value = AudioManager.Instance.GetMusicVolume();
            sfxSlider.value = AudioManager.Instance.GetSFXVolume();
        }
    }

    // ──────────────────────────────────────────────
    // Public Show/Hide Methods
    // ──────────────────────────────────────────────

    public void Open()
    {
        // Stop any current tweens
        panelRoot.DOKill();
        canvasGroup.DOKill();

        gameObject.SetActive(true);

        // Reset state for animation
        panelRoot.localScale = Vector3.zero;
        canvasGroup.alpha = 0;

        // Perform Scale Ease In
        panelRoot.DOScale(1f, animationDuration).SetEase(easeType).SetUpdate(true);
        canvasGroup.DOFade(1f, animationDuration).SetUpdate(true);
    }

    public void Close()
    {
        // Scale Ease Out
        panelRoot.DOScale(0f, animationDuration).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() => gameObject.SetActive(false));
            
        canvasGroup.DOFade(0f, animationDuration).SetUpdate(true);
    }

    private void HideInstant()
    {
        panelRoot.localScale = Vector3.zero;
        canvasGroup.alpha = 0;
        gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────────
    // Slider Callbacks
    // ──────────────────────────────────────────────

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }

    // External Trigger (Helper for UI Buttons in other panels)
    public void TogglePanel()
    {
        if (gameObject.activeSelf) Close();
        else Open();
    }
}
