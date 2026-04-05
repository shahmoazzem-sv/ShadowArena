using UnityEngine;
using UnityEngine.UI;

public class SettingButton : MonoBehaviour
{
    [SerializeField] GameObject settingPanel;
    Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
    }

    void Start()
    {
        btn.onClick.AddListener(OnSettingButtonClicked);
    }

    public void OnSettingButtonClicked()
    {
        if (settingPanel != null)
        {
            settingPanel.GetComponent<SettingsPanel>().Open();

        }
        else
            Debug.Log("Setting Panel Not found");
    }
}
