using UnityEngine;
using UnityEngine.UI;

public class sprintAmount : MonoBehaviour
{
    private Slider slider;
    private void Awake() => slider = GetComponent<Slider>();
    private void Update()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (player == null || slider == null || player.staminaSlider == slider) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = player.StaminaNormalized;
    }
}
