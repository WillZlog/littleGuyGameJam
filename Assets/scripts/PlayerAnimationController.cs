using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAnimationController : MonoBehaviour
{
    public static PlayerAnimationController Instance { get; private set; }

    public Slider staminaSlider;

    [SerializeField] private GameObject gameOverPanel;

    [Min(0f)] public float moveSpeed = 5f;
    [Min(1f)] public float sprintModifier = 1.5f;
    [SerializeField, Min(0.01f)] private float acceleration = 35f;
    [SerializeField, Min(0.01f)] private float deceleration = 45f;

    public float scale = 1f;
    public bool facingLeft;

    [Min(1f)] public float maxSprint = 100f;
    public float currentSprint;
    [Min(0f)] public float sprintDrainPerSecond = 50f;
    [Min(0f)] public float sprintRegenDelay = 4f;
    [Min(0f)] public float sprintGainPerSecond = 30f;
    [SerializeField, Range(0.01f, 1f)] private float exhaustionRecoveryFraction = 0.35f;

    public bool isSprinting;
    public bool isExhausted;

    [SerializeField, Min(0f)] private float timeWindow = 3f;
    [SerializeField, Min(0f)] private float shrinkSpeed = 0.1f;
    [SerializeField, Min(0f)] private float growthAmount = 0.2f;
    [SerializeField, Min(0.11f)] private float maxScale = 100f;
    [SerializeField, Min(0.01f)] private float minimumScale = 0.1f;

    [Tooltip("Ascending size thresholds. Reached stages stay unlocked even when hungry.")]
    [SerializeField] private float[] stageThresholds = { 2f, 4f, 8f, 16f, 32f };
    [SerializeField, Min(1.01f)] private float stageZoomMultiplier = 1.65f;
    [SerializeField, Min(0.01f)] private float cameraSmoothTime = 0.6f;
    [SerializeField] private bool followPlayer = true;

    public bool IsGameOver { get; private set; }
    public int HumansEaten { get; private set; }
    public int GrowthStage { get; private set; }
    public float SurvivalTime { get; private set; }
    public float PeakSize { get; private set; }
    public float HungerTimeRemaining => countdownTimer;
    public float StaminaNormalized => maxSprint > 0f ? currentSprint / maxSprint : 0f;
    public float WorldScale => ScaleForStage(GrowthStage);
    public float NextStageSize => GrowthStage < stageThresholds.Length ? stageThresholds[GrowthStage] : 0f;
    public float ScaleForStage(int stage) => Mathf.Pow(stageZoomMultiplier, Mathf.Max(0, stage));

    private Rigidbody2D body;
    private Animator animator;
    private Camera gameCamera;
    private float initialCameraSize;
    private float zoomVelocity;
    private Vector3 followVelocity;
    private Vector2 movementInput;
    private float regenDelay;
    private float countdownTimer;
    private bool hasSpeedParameter;
    private static readonly int Speed = Animator.StringToHash("Speed");

    private void OnValidate()
    {
        minimumScale = Mathf.Max(0.01f, minimumScale);
        maxScale = Mathf.Max(minimumScale + 0.01f, maxScale);
        maxSprint = Mathf.Max(1f, maxSprint);
        stageZoomMultiplier = Mathf.Max(1.01f, stageZoomMultiplier);
        cameraSmoothTime = Mathf.Max(0.01f, cameraSmoothTime);

        if (stageThresholds == null)
        {
            stageThresholds = new float[0];
        }

        float previous = minimumScale;
        for (int i = 0; i < stageThresholds.Length; i++)
        {
            stageThresholds[i] = Mathf.Max(previous + 0.01f, stageThresholds[i]);
            previous = stageThresholds[i];
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasSpeedParameter = true;
                }
            }
        }
    }

    private void Start()
    {
        gameCamera = Camera.main;
        if (gameCamera != null)
        {
            initialCameraSize = gameCamera.orthographicSize;
        }

        currentSprint = Mathf.Max(1f, maxSprint);
        countdownTimer = timeWindow;
        scale = Mathf.Clamp(scale, minimumScale + 0.01f, maxScale);
        PeakSize = scale;
        UpdateGrowthStage();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        UpdatePresentation();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        if ((keyboard != null && keyboard.rKey.wasPressedThisFrame) ||
            (IsGameOver && gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
        {
            RestartGame();
            return;
        }

        if (IsGameOver || Time.timeScale == 0f)
        {
            return;
        }

        Vector2 input = Vector2.zero;
        if (keyboard != null)
        {
            input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
        }
        if (gamepad != null && gamepad.leftStick.ReadValue().sqrMagnitude > input.sqrMagnitude)
        {
            input = gamepad.leftStick.ReadValue();
        }

        movementInput = Vector2.ClampMagnitude(input, 1f);
        bool sprintHeld = (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            || (gamepad != null && gamepad.rightShoulder.isPressed);
        UpdateStamina(sprintHeld && movementInput.sqrMagnitude > 0.01f, Time.deltaTime);
        SurvivalTime += Time.deltaTime;

        float hungryDelta = Mathf.Max(0f, Time.deltaTime - countdownTimer);
        countdownTimer = Mathf.Max(0f, countdownTimer - Time.deltaTime);
        scale = Mathf.Max(minimumScale, scale - shrinkSpeed * hungryDelta);

        if (scale <= minimumScale)
        {
            TriggerGameOver();
        }

        if (movementInput.x < -0.01f)
        {
            facingLeft = true;
        }
        else if (movementInput.x > 0.01f)
        {
            facingLeft = false;
        }

        UpdatePresentation();
    }

    private void UpdateStamina(bool wantsSprint, float delta)
    {
        isSprinting = wantsSprint && !isExhausted && currentSprint > 0f;

        if (isSprinting)
        {
            currentSprint = Mathf.Max(0f, currentSprint - sprintDrainPerSecond * delta);
            regenDelay = sprintRegenDelay;

            if (currentSprint <= 0f)
            {
                isExhausted = true;
                isSprinting = false;
            }
        }
        else
        {
            float recoveryDelta = Mathf.Max(0f, delta - regenDelay);
            regenDelay = Mathf.Max(0f, regenDelay - delta);
            currentSprint = Mathf.Min(maxSprint, currentSprint + sprintGainPerSecond * recoveryDelta);

            if (currentSprint >= maxSprint * exhaustionRecoveryFraction)
            {
                isExhausted = false;
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsGameOver)
        {
            return;
        }

        Vector2 target = movementInput * moveSpeed * Mathf.Sqrt(WorldScale) * (isSprinting ? sprintModifier : 1f);
        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target,
            (movementInput.sqrMagnitude > 0f ? acceleration : deceleration) * Mathf.Sqrt(WorldScale) * Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        if (gameCamera == null || !gameCamera.orthographic || IsGameOver)
        {
            return;
        }

        gameCamera.orthographicSize = Mathf.SmoothDamp(gameCamera.orthographicSize,
            initialCameraSize * WorldScale, ref zoomVelocity, cameraSmoothTime);

        if (!followPlayer)
        {
            return;
        }

        Vector3 target = new Vector3(transform.position.x, transform.position.y, gameCamera.transform.position.z);
        gameCamera.transform.position = Vector3.SmoothDamp(gameCamera.transform.position, target,
            ref followVelocity, cameraSmoothTime);
    }

    private void UpdatePresentation()
    {
        if (animator != null && hasSpeedParameter)
        {
            animator.SetFloat(Speed, IsGameOver ? 0f : body.linearVelocity.magnitude / Mathf.Max(0.01f, moveSpeed));
        }

        transform.localScale = new Vector3(facingLeft ? -scale : scale, scale, transform.localScale.z);
        if (staminaSlider == null)
        {
            return;
        }

        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = maxSprint;
        staminaSlider.value = currentSprint;
    }

    public void ResetTimer() => ConsumeFood(1f);

    public void ConsumeFood(float nutrition)
    {
        if (IsGameOver || Time.timeScale == 0f || nutrition <= 0f)
        {
            return;
        }
        animator.SetTrigger("Eat");
        countdownTimer = timeWindow;
        scale = Mathf.Min(maxScale, scale + growthAmount * nutrition);
        HumansEaten++;
        PeakSize = Mathf.Max(PeakSize, scale);
        UpdateGrowthStage();
        UpdatePresentation();
    }

    private void UpdateGrowthStage()
    {
        while (GrowthStage < stageThresholds.Length && PeakSize >= stageThresholds[GrowthStage])
        {
            GrowthStage++;
        }
    }

    private void TriggerGameOver()
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        isSprinting = false;
        movementInput = Vector2.zero;
        body.linearVelocity = Vector2.zero;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        Instance = null;

        if (IsGameOver)
        {
            Time.timeScale = 1f;
        }
    }
}
