using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class ScoreComboText : MonoBehaviour
{
    private TMP_Text label;
    private int displayedScore = -1;
    private int displayedCombo = -1;

    private void Awake() => label = GetComponent<TMP_Text>();

    private void LateUpdate()
    {
        var player = PlayerAnimationController.Instance;
        bool visible = player != null && !MainMenu.IsOpen && !player.IsGameOver;
        label.enabled = visible;
        if (!visible || (displayedScore == player.Score && displayedCombo == player.Combo)) return;
        displayedScore = player.Score;
        displayedCombo = player.Combo;
        label.text = "combo: " + displayedCombo + "\nscore: " + displayedScore;
    }
}
