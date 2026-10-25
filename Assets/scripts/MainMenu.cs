using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private static MainMenu instance;
    private static bool hasStarted;
    private const float BootDuration = 5f;
    private GameObject startCanvas;
    private GameObject hiddenHud;
    private bool hudWasActive;
    private AudioSource switchAudio;
    private float elapsed;
    private bool ready;
    private bool powerSwitched;
    private bool bootClockStarted;
    public static bool IsOpen => instance != null;
    public static bool NeedsBoot => !hasStarted;
    private static readonly int BootAmount = Shader.PropertyToID("_TVBootAmount");
    private static readonly int BootReveal = Shader.PropertyToID("_TVBootReveal");
    private static readonly int BootLine = Shader.PropertyToID("_TVBootLine");
    private static readonly int BootFlicker = Shader.PropertyToID("_TVBootFlicker");
    private static readonly int BootTime = Shader.PropertyToID("_TVBootTime");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        instance = null;
        hasStarted = false;
        ResetScreen();
    }

    public static void ShowIfNeeded(GameObject canvas, Slider stamina)
    {
        if (canvas != null)
        {
            canvas.SetActive(false);
        }

        if (hasStarted || instance != null)
        {
            return;
        }

        var menu = new GameObject("TV Startup").AddComponent<MainMenu>();
        menu.startCanvas = canvas;
        if (stamina != null)
        {
            menu.hiddenHud = stamina.gameObject;
            menu.hudWasActive = stamina.gameObject.activeSelf;
            stamina.gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        instance = this;
        Time.timeScale = 0f;
        var settings = Resources.Load<GameFeelSettings>("GameFeel");
        switchAudio = gameObject.AddComponent<AudioSource>();
        switchAudio.playOnAwake = false;
        switchAudio.spatialBlend = 0f;
        switchAudio.volume = 0.7f;
        switchAudio.clip = settings != null ? settings.tvSwitchClick : null;
        Shader.SetGlobalFloat(BootAmount, 1f);
        Shader.SetGlobalFloat(BootReveal, 0f);
        Shader.SetGlobalFloat(BootLine, 0f);
        Shader.SetGlobalFloat(BootFlicker, 0f);
    }

    private void Update()
    {
        if (!bootClockStarted)
        {
            bootClockStarted = true;
            return;
        }

        if (!ready)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / BootDuration);
            if (!powerSwitched && t >= 0.08f)
            {
                powerSwitched = true;
                if (switchAudio.clip != null)
                {
                    switchAudio.Play();
                }

                BackgroundMusic.BeginPowerOn(Mathf.Max(0.1f, BootDuration - elapsed));
            }
            Shader.SetGlobalFloat(BootTime, Time.unscaledTime);
            Shader.SetGlobalFloat(BootAmount, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, t)));
            Shader.SetGlobalFloat(BootLine, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.085f, 0.14f, t))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.84f, t))));
            Shader.SetGlobalFloat(BootReveal, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.86f, t)));
            float flicker = (t > 0.72f && t < 0.735f) || (t > 0.84f && t < 0.855f) ? 0.7f : 0f;
            Shader.SetGlobalFloat(BootFlicker, flicker);
            if (t >= 1f)
            {
                ready = true;
                BackgroundMusic.FinishPowerOn();
                ResetScreen();
                if (startCanvas != null)
                {
                    startCanvas.SetActive(true);
                }
            }

            return;
        }

        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            || (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame))) BeginGameplay();
    }

    public static void BeginGameplay()
    {
        if (instance == null || !instance.ready)
        {
            return;
        }

        hasStarted = true;
        if (instance.startCanvas != null)
        {
            instance.startCanvas.SetActive(false);
        }
        

        instance.RestoreHud();
        Time.timeScale = 1f;
        Destroy(instance.gameObject);
    }

    private void RestoreHud()
    {
        if (hiddenHud != null)
        {
            hiddenHud.SetActive(hudWasActive);
        }
    }

    private static void ResetScreen()
    {
        Shader.SetGlobalFloat(BootAmount, 0f);
        Shader.SetGlobalFloat(BootReveal, 1f);
        Shader.SetGlobalFloat(BootLine, 0f);
        Shader.SetGlobalFloat(BootFlicker, 0f);
    }

    private void OnDestroy()
    {
        ResetScreen();
        RestoreHud();
        if (instance == this)
        {
            instance = null;
            if (!hasStarted)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
