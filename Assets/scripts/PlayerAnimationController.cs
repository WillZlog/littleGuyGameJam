using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAnimationController : MonoBehaviour
{
    public static PlayerAnimationController Instance { get; private set; }

    public Slider staminaSlider;

    [SerializeField] private GameObject startCanvas;

    [FormerlySerializedAs("gameOverPanel")]
    [SerializeField] private GameObject gameOverCanvas;

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
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public bool IsTired => isExhausted || StaminaNormalized < exhaustionRecoveryFraction;
    private float comboRemaining;
    private const float ComboWindow = 3.5f;

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
    private bool hasTiredParameter;
    private bool hasEatParameter;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Tired = Animator.StringToHash("IsTired");
    private static readonly int Eat = Animator.StringToHash("Eat");
    private float damageImmunity;
    private float knockbackRemaining;
    private SpriteRenderer[] playerSprites;
    private Color[] playerColors;
    private AudioSource pigAudio;
    private GameFeelSettings feedbackSettings;
    private float nextGruntTime;
    private Vector3 cameraRestPosition;
    private float shakeRemaining;
    private float shakeDuration;
    private float shakeStrength;
    private float displayScale;
    private float scaleVelocity;

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
        playerSprites = GetComponentsInChildren<SpriteRenderer>();
        playerColors = new Color[playerSprites.Length];
        for (int i = 0; i < playerSprites.Length; i++)
        {
            playerColors[i] = playerSprites[i].color;
        }

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        feedbackSettings = Resources.Load<GameFeelSettings>("GameFeel");
        BackgroundMusic.EnsurePlaying(feedbackSettings);
        pigAudio = GetComponent<AudioSource>();
        if (pigAudio == null)
        {
            pigAudio = gameObject.AddComponent<AudioSource>();
        }

        pigAudio.playOnAwake = false;
        pigAudio.spatialBlend = 0f;
        pigAudio.volume = feedbackSettings != null ? feedbackSettings.pigVolume : 0.65f;
        pigAudio.clip = feedbackSettings != null ? feedbackSettings.pigGrunt : null;

        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == Tired && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasTiredParameter = true;
                }

                if (parameter.nameHash == Eat && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    hasEatParameter = true;
                }

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
            cameraRestPosition = gameCamera.transform.position;
        }

        currentSprint = Mathf.Max(1f, maxSprint);
        countdownTimer = timeWindow / (1.5f + GrowthStage * 0.25f);
        scale = Mathf.Clamp(scale, minimumScale + 0.01f, maxScale);
        displayScale = scale;
        PeakSize = scale;
        BestScore = PlayerPrefs.GetInt("LittleGuy.BestScore", 0);
        UpdateGrowthStage();

        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(false);
        }

        UpdatePresentation();
        MainMenu.ShowIfNeeded(startCanvas, staminaSlider);
    }

    private void Update()
    {
        if (MainMenu.IsOpen)
        {
            return;
        }

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
        damageImmunity = Mathf.Max(0f, damageImmunity - Time.deltaTime);
        comboRemaining = Mathf.Max(0f, comboRemaining - Time.deltaTime);
        if (comboRemaining <= 0f)
        {
            Combo = 0;
        }

        float hungryDelta = Mathf.Max(0f, Time.deltaTime - countdownTimer);
        countdownTimer = Mathf.Max(0f, countdownTimer - Time.deltaTime);
        scale = Mathf.Max(minimumScale, scale - shrinkSpeed * 3f * Mathf.Max(1f, scale) * hungryDelta);

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

        if (knockbackRemaining > 0f)
        {
            knockbackRemaining = Mathf.Max(0f, knockbackRemaining - Time.fixedDeltaTime);
            return;
        }

        Vector2 target = movementInput * moveSpeed * Mathf.Sqrt(WorldScale)
            * (isSprinting ? sprintModifier : 1f) * (IsTired ? 0.8f : 1f);
        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target,
            (movementInput.sqrMagnitude > 0f ? acceleration : deceleration) * Mathf.Sqrt(WorldScale) * Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        displayScale = Mathf.SmoothDamp(displayScale, scale, ref scaleVelocity, 0.09f,
            Mathf.Infinity, Time.unscaledDeltaTime);
        transform.localScale = new Vector3(facingLeft ? -displayScale : displayScale, displayScale, transform.localScale.z);

        if (gameCamera == null || !gameCamera.orthographic)
        {
            return;
        }

        if (!IsGameOver && Time.timeScale > 0f)
        {
            gameCamera.orthographicSize = Mathf.SmoothDamp(gameCamera.orthographicSize,
                initialCameraSize * WorldScale, ref zoomVelocity, cameraSmoothTime);
            if (followPlayer)
            {
                Vector2 lookAhead = Vector2.ClampMagnitude(body.linearVelocity * 0.08f, 0.4f * WorldScale);
                Vector3 target = new Vector3(transform.position.x + lookAhead.x, transform.position.y + lookAhead.y, cameraRestPosition.z);
                cameraRestPosition = Vector3.SmoothDamp(cameraRestPosition, target,
                    ref followVelocity, Mathf.Max(0.05f, cameraSmoothTime * 0.5f));
            }
        }
        shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.unscaledDeltaTime);
        float fade = shakeDuration > 0f ? shakeRemaining / shakeDuration : 0f;
        float noiseTime = Time.unscaledTime * 28f;
        Vector3 offset = new Vector3(Mathf.PerlinNoise(noiseTime, 0.2f) * 2f - 1f,
            Mathf.PerlinNoise(0.8f, noiseTime) * 2f - 1f, 0f) * shakeStrength * fade * fade;
        gameCamera.transform.position = cameraRestPosition + offset;
    }

    public void ShakeCamera(float strength, float duration)
    {
        float amount = strength * Mathf.Sqrt(WorldScale) * (feedbackSettings != null ? feedbackSettings.cameraShake : 1f);
        shakeStrength = Mathf.Max(shakeRemaining > 0f ? shakeStrength : 0f, amount);
        shakeDuration = Mathf.Max(shakeRemaining, duration);
        shakeRemaining = shakeDuration;
    }

    private void PlayGrunt(bool hurt = false)
    {
        if (pigAudio == null || pigAudio.clip == null || (!hurt && Time.unscaledTime < nextGruntTime))
        {
            return;
        }

        nextGruntTime = Time.unscaledTime + 0.45f;
        pigAudio.pitch = hurt ? 0.8f : Random.Range(0.95f, 1.08f);
        pigAudio.Play();
    }

    private void UpdatePresentation()
    {
        for (int i = 0; i < playerSprites.Length; i++)
        {
            playerSprites[i].color = damageImmunity > 0f
                ? Color.Lerp(playerColors[i], new Color(1f, 0.35f, 0.35f, playerColors[i].a),
                    0.5f + 0.5f * Mathf.Sin(Time.time * 22f)) : playerColors[i];
        }

        if (animator != null && hasTiredParameter)
        {
            animator.SetBool(Tired, IsTired);
        }

        if (animator != null && hasSpeedParameter)
        {
            animator.SetFloat(Speed, IsGameOver ? 0f : body.linearVelocity.magnitude / Mathf.Max(0.01f, moveSpeed),
                0.08f, Time.deltaTime);
        }

        if (staminaSlider == null)
        {
            return;
        }

        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = maxSprint;
        staminaSlider.value = Mathf.MoveTowards(staminaSlider.value, currentSprint, maxSprint * 6f * Time.deltaTime);
    }

    public void ResetTimer() => ConsumeFood(1f);

    public bool TakeHit(Vector2 awayFromEnemy)
    {
        if (IsGameOver || Time.timeScale == 0f || damageImmunity > 0f)
        {
            return false;
        }

        PlayGrunt(true);
        ShakeCamera(0.22f, 0.32f);
        damageImmunity = 1.25f;
        knockbackRemaining = 0.18f;
        scale = Mathf.Max(minimumScale, scale - Mathf.Max(0.18f, scale * 0.2f));
        currentSprint = Mathf.Max(0f, currentSprint - maxSprint * 0.25f);
        regenDelay = Mathf.Max(regenDelay, sprintRegenDelay);
        if (currentSprint <= 0f)
        {
            isExhausted = true;
        }

        Combo = 0;
        comboRemaining = 0f;
        countdownTimer = 0f;
        body.linearVelocity = awayFromEnemy.normalized * 8f * Mathf.Sqrt(WorldScale);
        if (scale <= minimumScale)
        {
            TriggerGameOver();
        }

        UpdatePresentation();
        return true;
    }

    public void ConsumeFood(float nutrition)
    {
        if (IsGameOver || Time.timeScale == 0f || nutrition <= 0f)
        {
            return;
        }

        if (animator != null && hasEatParameter)
        {
            animator.SetTrigger(Eat);
        }

        PlayGrunt();
        ShakeCamera(0.035f, 0.12f);
        Combo++;
        BestCombo = Mathf.Max(BestCombo, Combo);
        comboRemaining = ComboWindow;
        Score += Mathf.RoundToInt(100f * nutrition) * Mathf.Min(5, 1 + (Combo - 1) / 3);
        BestScore = Mathf.Max(BestScore, Score);
        countdownTimer = timeWindow / (1.5f + GrowthStage * 0.25f);
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
        SaveBestScore();
        isSprinting = false;
        movementInput = Vector2.zero;
        body.linearVelocity = Vector2.zero;

        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
        }

        Time.timeScale = 0f;
    }

    public void StartGame() => MainMenu.BeginGameplay();

    public void RestartGame()
    {
        SaveBestScore();
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(false);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    private void SaveBestScore()
    {
        PlayerPrefs.SetInt("LittleGuy.BestScore", BestScore);
        PlayerPrefs.Save();
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
