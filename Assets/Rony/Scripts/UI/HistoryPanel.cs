using TMPro;
using UnityEngine;

public class HistoryPanel : MonoBehaviour
{
    [SerializeField] TMP_Text number;
    [SerializeField] TMP_Text whiteMove;
    [SerializeField] TMP_Text blackMove;

    public void SetMoveNumber(int number)
    {
        this.number.text = $"{number}.";
    }

    public void SetWhiteMove(string text)
    {
        whiteMove.text = text;
    }

    public void SetBlackMove(string text)
    {
        blackMove.text = text;
    }

}
