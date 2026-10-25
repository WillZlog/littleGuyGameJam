using UnityEngine;

[CreateAssetMenu(menuName = "Little Guy/Game Feel")]
public class GameFeelSettings : ScriptableObject
{
    public AudioClip pigGrunt;
    [Range(0f, 1f)] public float pigVolume = 0.65f;
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.25f;
    public AudioClip tvSwitchClick;
    [Range(0f, 2f)] public float cameraShake = 1f;
}
