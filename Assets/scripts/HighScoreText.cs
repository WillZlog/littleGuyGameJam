using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class HighScoreText : MonoBehaviour
{
    private void OnEnable()
    {
        int best = PlayerPrefs.GetInt("LittleGuy.BestScore", 0);
        GetComponent<TMP_Text>().text = $"High Score: {best}";
    }
}