using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ChargingHunter : MonoBehaviour
{
    public enum AttackStyle { Dash, Shoot, Stab }
    [SerializeField] private AttackStyle attackStyle = AttackStyle.Stab;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 14f;
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private bool artworkFacesLeft;
    [SerializeField, Min(0.1f)] private float characterHeight = 1.4f;
    [SerializeField, Min(0.1f)] private float pursuitSpeed = 2.3f;
    [SerializeField, Min(0.1f)] private float dashSpeed = 11f;
    [SerializeField, Min(0.1f)] private float warningDuration = 0.6f;
    [SerializeField, Min(0.1f)] private float recoveryDuration = 1.4f;
    [SerializeField, Min(0.1f)] private float stabReach = 1.05f;
    [SerializeField, Min(1f)] private float ediblePlayerSize = 1.8f;
    [SerializeField, Min(0.1f)] private float hunterNutrition = 2f;
    private enum AttackState { Pursuing, Warning, Charging, Recovering, Stabbing, Stunned }
    private static readonly List<ChargingHunter> activeEnemies = new List<ChargingHunter>();
    private AttackState state;
    private float stateTime;
    private Vector2 attackDirection;
    private Vector2 chaseVelocity;
    private Rigidbody2D body;
    private CapsuleCollider2D bodyCollider;
    private bool initialized;
    private float baseScale = 1f;
    private float size = 1f;
    private Color baseColor;
    private Vector3 artOrigin;
    private float frameWidth;
    private float attackPoseRemaining;
    private Vector2 previousAnimationPosition;
    private float walkFrame;
    private float measuredMoveSpeed;
    private bool missedStab;
    private bool consumed;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    private const float ChargeDuration = 0.65f;
    public AttackStyle Style => attackStyle;
    public bool IsStunned => state == AttackState.Stunned;

    public bool CanBeEatenBy(PlayerAnimationController player) => !consumed && initialized && player != null
        && !player.IsGameOver && player.scale >= ediblePlayerSize * size
        && (IsStunned || (attackStyle == AttackStyle.Stab && state == AttackState.Recovering && missedStab));

    public void Stun(float duration = 1.6f)
    {
        if (consumed || !initialized) return;
        state = AttackState.Stunned;
        stateTime = Mathf.Max(0.1f, duration);
        attackPoseRemaining = 0f;
        chaseVelocity = Vector2.zero;
        body.linearVelocity = Vector2.zero;
    }

    public bool TryBeEaten(PlayerAnimationController player)
    {
        if (Time.timeScale == 0f || !CanBeEatenBy(player)) return false;
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null || bodyCollider.Distance(playerCollider).distance > 0.07f * size) return false;
        consumed = true;
        bodyCollider.enabled = false;
        player.ConsumeFood(hunterNutrition * size);
        Destroy(gameObject);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEnemies() => activeEnemies.Clear();

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.useFullKinematicContacts = true;
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        bodyCollider = GetComponent<CapsuleCollider2D>();
        if (bodyCollider == null) bodyCollider = gameObject.AddComponent<CapsuleCollider2D>();
        bodyCollider.isTrigger = false;
        activeEnemies.Add(this);
    }

    public static ChargingHunter Create(Sprite fallbackSprite, PlayerAnimationController player, ChargingHunter template = null)
    {
        ChargingHunter prefab = template != null ? template : Resources.Load<ChargingHunter>("HumanKnife");
        if (prefab == null) prefab = Resources.Load<ChargingHunter>("Hunter");
        ChargingHunter enemy = prefab != null ? Instantiate(prefab) : new GameObject("Hunter").AddComponent<ChargingHunter>();
        enemy.Initialize(fallbackSprite, player);
        return enemy;
    }

    private void Initialize(Sprite fallbackSprite, PlayerAnimationController player)
    {
        name = attackStyle == AttackStyle.Shoot ? "humanStar" : attackStyle == AttackStyle.Stab ? "humanKnife" : "Hunter";
        if (visual == null)
        {
            GameObject art = new GameObject("Enemy Art");
            art.transform.SetParent(transform, false);
            visual = art.AddComponent<SpriteRenderer>();
        }
        if (idleFrames != null && idleFrames.Length > 0) visual.sprite = idleFrames[0];
        if (visual.sprite == null) visual.sprite = fallbackSprite;
        if (visual.sprite == null) { Destroy(gameObject); return; }
        visual.sortingOrder = 12;
        baseColor = visual.color;
        artOrigin = visual.transform.localPosition;
        bool animated = walkFrames != null && walkFrames.Length > 0;
        frameWidth = animated ? visual.sprite.texture.width / 6f : 0f;
        float nativeHeight = animated ? visual.sprite.texture.height / visual.sprite.pixelsPerUnit : visual.bounds.size.y;
        baseScale = characterHeight / Mathf.Max(0.1f, nativeHeight);
        transform.localScale = Vector3.one * baseScale;
        bodyCollider.size = new Vector2(0.48f, 0.85f) / baseScale;
        bodyCollider.offset = new Vector2(0f, -0.05f) / baseScale;
        stateTime = Random.Range(0.8f, 1.6f);
        initialized = true;
        SetSpawnPosition((Vector2)player.transform.position + Random.insideUnitCircle.normalized * 6f);
        ApplyFrame(visual.sprite);
    }

    public void SetSpawnPosition(Vector2 position)
    {
        body.position = ArenaBounds.Clamp(position, Vector2.one * 0.8f * size);
        body.linearVelocity = Vector2.zero;
        previousAnimationPosition = body.position;
        measuredMoveSpeed = 0f;
        walkFrame = 0f;
    }

    private void FixedUpdate()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (player == null || player.IsGameOver || !initialized || consumed) return;
        float movementScale = Mathf.Sqrt(player.WorldScale);
        size = Mathf.Min(2f, movementScale);
        Vector2 position = body.position;
        float distanceMoved = Vector2.Distance(position, previousAnimationPosition);
        previousAnimationPosition = position;
        measuredMoveSpeed = distanceMoved / Mathf.Max(0.001f, Time.fixedDeltaTime);
        if (walkFrames != null && walkFrames.Length > 0)
            walkFrame = (walkFrame + distanceMoved * walkFramesPerSecond
                / Mathf.Max(0.1f, pursuitSpeed * movementScale)) % walkFrames.Length;
        Vector2 toPlayer = (Vector2)player.transform.position - position;
        float distance = toPlayer.magnitude;
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (CanBeEatenBy(player) && playerCollider != null
            && bodyCollider.Distance(playerCollider).distance <= 0.07f * size && TryBeEaten(player)) return;
        stateTime -= Time.fixedDeltaTime;
        attackPoseRemaining = Mathf.Max(0f, attackPoseRemaining - Time.fixedDeltaTime);
        if (state == AttackState.Pursuing)
        {
            float range = attackStyle == AttackStyle.Stab ? 1.3f * size : 7.5f * movementScale;
            if (stateTime <= 0f && distance < range)
            {
                attackDirection = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.right;
                chaseVelocity = Vector2.zero;
                state = AttackState.Warning;
                stateTime = Mathf.Max(0.35f, warningDuration - player.GrowthStage * 0.035f);
            }
            else
            {
                Vector2 steering = toPlayer.normalized;
                if (attackStyle == AttackStyle.Shoot)
                {
                    if (distance < 3.2f * movementScale) steering = -steering;
                    else if (distance < 4.8f * movementScale) steering = Vector2.zero;
                }
                steering = AvoidHunters(steering, position, movementScale);
                chaseVelocity = Vector2.MoveTowards(chaseVelocity,
                    Vector2.ClampMagnitude(steering, 1f) * pursuitSpeed * movementScale, 12f * movementScale * Time.fixedDeltaTime);
                MoveSolid(position + chaseVelocity * Time.fixedDeltaTime);
            }
        }
        else if (state == AttackState.Warning && stateTime <= 0f)
        {
            attackPoseRemaining = 0.22f;
            if (attackStyle == AttackStyle.Shoot)
            {
                HunterProjectile.Create(position + attackDirection * 0.45f * size, attackDirection,
                    6.5f * movementScale, size, projectileSprite, transform);
                Recover();
            }
            else if (attackStyle == AttackStyle.Stab)
            {
                missedStab = !StrikePlayer(player, stabReach * size, 0.18f * size);
                state = AttackState.Stabbing;
                stateTime = 0.18f;
            }
            else { state = AttackState.Charging; stateTime = ChargeDuration; }
        }
        else if (state == AttackState.Stabbing)
        {
            MoveSolid(position + attackDirection * 3.2f * movementScale * Time.fixedDeltaTime);
            if (stateTime <= 0f) Recover();
        }
        else if (state == AttackState.Charging)
        {
            Vector2 travel = attackDirection * dashSpeed * movementScale * Time.fixedDeltaTime;
            bool hit = StrikePlayer(player, travel.magnitude, 0.32f * size);
            MoveSolid(position + travel);
            if (hit || stateTime <= 0f) Recover();
        }
        else if ((state == AttackState.Recovering || state == AttackState.Stunned) && stateTime <= 0f)
        { state = AttackState.Pursuing; stateTime = Random.Range(0.25f, 0.6f); }
    }

    private bool StrikePlayer(PlayerAnimationController player, float reach, float radius)
    {
        int count = Physics2D.CircleCast(body.position, radius, attackDirection, ContactFilter2D.noFilter, hits, reach);
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = hits[i].collider;
            if (collider.transform.IsChildOf(transform)) continue;
            if (collider.GetComponentInParent<PlayerAnimationController>() == player) return player.TakeHit(attackDirection);
            if (!collider.isTrigger) return false;
        }
        return false;
    }

    private Vector2 AvoidHunters(Vector2 desired, Vector2 position, float movementScale)
    {
        Vector2 steering = desired;
        foreach (ChargingHunter enemy in activeEnemies)
        {
            if (enemy == null || enemy == this || !enemy.initialized || enemy.consumed
                || !enemy.isActiveAndEnabled) continue;
            Vector2 toward = enemy.body.position - position;
            float distance = toward.magnitude;
            float spacing = 0.85f * (size + enemy.size);
            Vector2 away = distance > 0.001f ? -toward / distance
                : (activeEnemies.IndexOf(this) < activeEnemies.IndexOf(enemy) ? Vector2.left : Vector2.right);
            if (distance < spacing)
                steering += away * (1f - distance / spacing) * 3f;

            // Start passing before touching. Both hunters keep the same handedness,
            // so head-on approaches choose opposite sides instead of cancelling out.
            float ahead = Vector2.Dot(toward, desired);
            float sideways = Mathf.Abs(toward.x * desired.y - toward.y * desired.x);
            float lookAhead = spacing + pursuitSpeed * movementScale * 0.35f;
            if (desired.sqrMagnitude > 0.01f && ahead > 0f && ahead < lookAhead && sideways < spacing)
            {
                Vector2 passing = new Vector2(away.y, -away.x);
                float urgency = (1f - ahead / lookAhead) * (1f - sideways / spacing);
                steering += passing * (1f + urgency * 2f);
            }
        }
        return Vector2.ClampMagnitude(steering, 1f);
    }

    private void MoveSolid(Vector2 desired)
    {
        Vector2 next = ArenaBounds.Clamp(desired, visual.bounds.extents, visual.bounds.center - transform.position);
        Vector2 travel = next - body.position;
        float distance = travel.magnitude;
        if (distance < 0.0001f) return;
        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = false;
        int count = body.Cast(travel / distance, filter, hits, distance + 0.02f);
        for (int i = 0; i < count; i++)
            if (!hits[i].collider.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - 0.015f));
        body.MovePosition(body.position + travel.normalized * distance);
    }

    private void Recover() { state = AttackState.Recovering; stateTime = recoveryDuration; }

    private void LateUpdate()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (player == null || !initialized || player.IsGameOver) return;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * baseScale * size, 1f - Mathf.Exp(-12f * Time.deltaTime));
        Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
        float facing = state == AttackState.Warning || state == AttackState.Stabbing || state == AttackState.Charging ? attackDirection.x : toPlayer.x;
        visual.flipX = artworkFacesLeft ? facing > 0f : facing < 0f;
        bool hasAttackArt = attackPoseRemaining > 0f && attackFrames != null && attackFrames.Length > 0;
        bool walking = measuredMoveSpeed > 0.05f && walkFrames != null && walkFrames.Length > 0;
        Sprite[] frames = hasAttackArt ? attackFrames : walking ? walkFrames : idleFrames;
        int frameIndex = hasAttackArt ? Mathf.FloorToInt((0.22f - attackPoseRemaining) * 12f)
            : walking ? Mathf.FloorToInt(walkFrame) : 0;
        if (frames != null && frames.Length > 0) ApplyFrame(frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)]);
        Color target = CanBeEatenBy(player) ? Color.Lerp(baseColor, new Color(0.45f, 1f, 0.55f), 0.45f + 0.15f * Mathf.Sin(Time.time * 8f))
            : IsStunned ? Color.Lerp(baseColor, new Color(0.6f, 0.65f, 0.9f), 0.5f)
            : state == AttackState.Warning ? Color.Lerp(baseColor, new Color(1f, 0.65f, 0.6f),
                0.3f + 0.25f * Mathf.Sin(Time.time * 16f)) : baseColor;
        visual.color = Color.Lerp(visual.color, target, 1f - Mathf.Exp(-20f * Time.deltaTime));
    }

    private void ApplyFrame(Sprite frame)
    {
        visual.sprite = frame;
        if (frameWidth <= 0f) return;
        // Align tightly sliced sprites to the same original cell, preventing pose-to-pose jitter.
        float cellCenter = Mathf.Floor(frame.rect.x / frameWidth) * frameWidth + frameWidth * 0.5f;
        Vector2 offset = new Vector2(frame.rect.x + frame.pivot.x - cellCenter,
            frame.rect.y + frame.pivot.y - frame.texture.height * 0.5f) / frame.pixelsPerUnit;
        if (visual.flipX) offset.x *= -1f;
        visual.transform.localPosition = artOrigin + (Vector3)offset;
    }

    private void OnDestroy() => activeEnemies.Remove(this);

    private void OnCollisionStay2D(Collision2D collision)
    {
        var player = collision.collider.GetComponentInParent<PlayerAnimationController>();
        if (player != null) TryBeEaten(player);
    }
}
