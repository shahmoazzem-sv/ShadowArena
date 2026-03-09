using UnityEngine;

public class HistoryBoard : MonoBehaviour
{
    [SerializeField] HistoryPanel historyPanelPrefab;
    [SerializeField] Transform historyPanelParent;

    private HistoryPanel currentActivePanel;
    public static HistoryBoard Instance;

    void Awake()
    {
        if (Instance == null && Instance != this)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        // Subscribe to the new event
        GameManager.Instance.OnMoveRecorded += HandleMoveRecorded;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnMoveRecorded -= HandleMoveRecorded;
    }


    void Start()
    {
        currentActivePanel = Instantiate(historyPanelPrefab, historyPanelParent);
    }



    void HandleMoveRecorded(int fullMoveNumber, string san, bool isWhite)
    {
        if (isWhite)
        {
            // Create a new row for every White move
            currentActivePanel = Instantiate(historyPanelPrefab, historyPanelParent);
            currentActivePanel.SetMoveNumber(fullMoveNumber);
            currentActivePanel.SetWhiteMove(san);
            currentActivePanel.SetBlackMove(""); // Clear black text for now
        }
        else
        {
            // If black moves, update the current existing row
            if (currentActivePanel != null)
            {
                currentActivePanel.SetBlackMove(san);
            }
        }
    }




}
