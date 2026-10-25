using TMPro;
using UnityEngine;

public class GameOverStats : MonoBehaviour
{
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private TMP_Text humansEatenText;
    [SerializeField] private TMP_Text bestComboText;

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (player == null) return;
        if (summaryText != null)
            summaryText.text = "Score: " + player.Score + "\nHigh Score: " + player.BestScore
                + "\nHumans Eaten: " + player.HumansEaten + "\nBest Combo: " + player.BestCombo;
        if (scoreText != null) scoreText.text = player.Score.ToString();
        if (highScoreText != null) highScoreText.text = player.BestScore.ToString();
        if (humansEatenText != null) humansEatenText.text = player.HumansEaten.ToString();
        if (bestComboText != null) bestComboText.text = player.BestCombo.ToString();
    }
}
