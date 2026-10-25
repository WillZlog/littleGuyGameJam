using UnityEngine;
using UnityEngine.UI;

public enum GameSound
{
    Step, Eat, Golden, HunterEat, Hurt, Windup, Stab, Throw, Dash, Stun,
    NearMiss, Exhausted, Recovered, Hunger, Growth, Combo, Start, GameOver, UiClick
}

public class GameAudio : MonoBehaviour
{
    private static GameAudio instance;
    private GameFeelSettings settings;
    private AudioClip[] clips;
    private AudioSource[] voices;
    private float[] nextAllowed;
    private float[] priorities;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    public static void Initialize(GameFeelSettings settings)
    {
        if (instance == null)
        {
            instance = new GameObject("Sound Effects").AddComponent<GameAudio>();
            DontDestroyOnLoad(instance.gameObject);
            int count = System.Enum.GetValues(typeof(GameSound)).Length;
            instance.clips = new AudioClip[count];
            instance.nextAllowed = new float[count];
            instance.voices = new AudioSource[12];
            instance.priorities = new float[12];
            for (int i = 0; i < count; i++)
                instance.clips[i] = Resources.Load<AudioClip>("Audio/" + (GameSound)i);
            for (int i = 0; i < instance.voices.Length; i++)
            {
                var voice = instance.gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                instance.voices[i] = voice;
            }
        }
        instance.settings = settings;
        foreach (var voice in instance.voices) voice.Stop();
        if (settings != null && settings.soundEffects != null)
            foreach (var effect in settings.soundEffects)
                if (effect.clip != null && (int)effect.sound >= 0 && (int)effect.sound < instance.clips.Length)
                    instance.clips[(int)effect.sound] = effect.clip;
        foreach (var clip in instance.clips)
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        System.Array.Clear(instance.nextAllowed, 0, instance.nextAllowed.Length);
        // Existing custom buttons keep their own visuals and actions.
        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            button.onClick.RemoveListener(Click);
            button.onClick.AddListener(Click);
        }
    }

    private static void Click() => Play(GameSound.UiClick, volume: 0.45f);

    public static void Play(GameSound sound, Vector2? position = null, float volume = 1f, float pitch = 1f)
    {
        if (instance == null) return;
        int index = (int)sound;
        if (index < 0 || index >= instance.clips.Length || instance.clips[index] == null
            || Time.unscaledTime < instance.nextAllowed[index]) return;
        float pan = 0f;
        if (position.HasValue && PlayerAnimationController.Instance != null)
        {
            var player = PlayerAnimationController.Instance;
            Vector2 offset = position.Value - (Vector2)player.transform.position;
            float range = 10f * Mathf.Sqrt(player.WorldScale);
            volume *= Mathf.Clamp01(1f - offset.magnitude / range);
            pan = Mathf.Clamp(offset.x / range, -0.65f, 0.65f);
        }
        if (volume <= 0.01f) return;
        float priority = sound == GameSound.Step ? 0f
            : sound == GameSound.GameOver || sound == GameSound.Hurt || sound == GameSound.Golden ? 2f : 1f;
        int selected = -1;
        for (int i = 0; i < instance.voices.Length; i++)
            if (!instance.voices[i].isPlaying) { selected = i; break; }
        if (selected < 0)
            for (int i = 0; i < instance.voices.Length; i++)
                if (instance.priorities[i] < priority) { selected = i; break; }
        if (selected < 0) return;
        instance.nextAllowed[index] = Time.unscaledTime + (sound == GameSound.Step ? 0.11f
            : sound == GameSound.Windup ? 0.18f : 0.06f);
        var source = instance.voices[selected];
        source.Stop();
        source.clip = instance.clips[index];
        source.volume = Mathf.Clamp01(volume * (instance.settings != null ? instance.settings.soundEffectsVolume : 0.65f));
        source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        source.panStereo = pan;
        instance.priorities[selected] = priority;
        source.Play();
    }
}
