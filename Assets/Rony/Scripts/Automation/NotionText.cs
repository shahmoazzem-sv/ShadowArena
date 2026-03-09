using TMPro;
using UnityEngine;

public class NotionText : MonoBehaviour
{
    [SerializeField] TMP_Text textMeshPro;

    void Start()
    {
        textMeshPro.text = "HI";
    }
}
