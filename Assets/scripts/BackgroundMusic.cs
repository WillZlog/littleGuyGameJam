using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    private static BackgroundMusic instance;
    private AudioSource source;
    private float targetVolume;
    private bool waitingForPower;
    private bool spinningUp;
    private float spinupElapsed;
    private float spinupDuration;
    private bool ducked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    public static void EnsurePlaying(GameFeelSettings settings)
    {
        if (settings == null || settings.backgroundMusic == null)
        {
            return;
        }

        if (instance == null)
        {
            instance = new GameObject("Background Music").AddComponent<BackgroundMusic>();
            DontDestroyOnLoad(instance.gameObject);
            instance.source = instance.GetComponent<AudioSource>();
            instance.source.playOnAwake = false;
            instance.source.loop = true;
            instance.source.spatialBlend = 0f;
            instance.source.volume = 0f;
        }

        instance.targetVolume = settings.musicVolume;
        instance.ducked = false;
        if (instance.source.clip == settings.backgroundMusic && instance.source.isPlaying)
        {
            return;
        }

        instance.source.clip = settings.backgroundMusic;
        if (MainMenu.NeedsBoot)
        {
            instance.waitingForPower = true;
            instance.source.pitch = 0.12f;
            instance.source.volume = 0f;
            return;
        }
        instance.source.pitch = 1f;
        instance.source.Play();
    }

    public static void BeginPowerOn(float duration)
    {
        if (instance == null || instance.source.clip == null || !instance.waitingForPower)
        {
            return;
        }

        instance.waitingForPower = false;
        instance.spinningUp = true;
        instance.spinupElapsed = 0f;
        instance.spinupDuration = Mathf.Max(0.1f, duration);
        instance.source.pitch = 0.25f;
        instance.source.volume = 0f;
        instance.source.Play();
    }

    public static void FinishPowerOn()
    {
        if (instance == null || instance.waitingForPower)
        {
            return;
        }

        instance.spinningUp = false;
        instance.source.pitch = 1f;
        instance.source.volume = instance.targetVolume;
    }

    private void Update()
    {
        if (waitingForPower)
        {
            return;
        }

        if (spinningUp)
        {
            spinupElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(spinupElapsed / spinupDuration));
            source.pitch = Mathf.Lerp(0.25f, 1f, progress);
            source.volume = targetVolume * progress;
            if (spinupElapsed >= spinupDuration)
            {
                FinishPowerOn();
            }

            return;
        }
        source.volume = Mathf.MoveTowards(source.volume, targetVolume * (ducked ? 0.45f : 1f), Time.unscaledDeltaTime * 0.35f);
    }

    public static void SetDucked(bool value)
    {
        if (instance != null) instance.ducked = value;
    }
}
