using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ChargingHunter : MonoBehaviour
{
    public enum AttackStyle
    {
        Dash,
        Shoot
    }

    [SerializeField] private AttackStyle attackStyle = AttackStyle.Dash;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField, Min(0.1f)] private float characterHeight = 1.4f;
    [SerializeField, Min(0.1f)] private float pursuitSpeed = 2.3f;
    [SerializeField, Min(0.1f)] private float dashSpeed = 11f;
    [SerializeField, Min(0.1f)] private float warningDuration = 0.9f;
    [SerializeField, Min(0.1f)] private float recoveryDuration = 1.4f;
    private enum AttackState
    {
        Pursuing,
        Warning,
        Charging,
        Recovering
    }

    private AttackState state;
    private float stateTime = 1f;
    private Vector2 chargeDirection;
    private Vector2 chaseVelocity;
    private Rigidbody2D body;
    private bool initialized;
    private float baseScale = 1f;
    private float size = 1f;
    private Color baseColor;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private const float ChargeDuration = 0.65f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (visual == null)
        {
            visual = GetComponentInChildren<SpriteRenderer>();
        }
    }

    public static ChargingHunter Create(Sprite fallbackSprite, PlayerAnimationController player, ChargingHunter template = null)
    {
        ChargingHunter prefab = template != null ? template : Resources.Load<ChargingHunter>("Hunter");
        ChargingHunter hunter = prefab != null ? Instantiate(prefab) : new GameObject("Hunter").AddComponent<ChargingHunter>();
        hunter.Initialize(fallbackSprite, player);
        return hunter;
    }

    private void Initialize(Sprite fallbackSprite, PlayerAnimationController player)
    {
        name = "Hunter";
        if (visual == null)
        {
            GameObject art = new GameObject("Hunter Art");
            art.transform.SetParent(transform, false);
            visual = art.AddComponent<SpriteRenderer>();
        }

        bool placeholder = visual.sprite == null;
        if (placeholder)
        {
            visual.sprite = fallbackSprite;
        }

        baseColor = placeholder ? new Color(0.85f, 0.15f, 0.24f) : visual.color;
        visual.sortingOrder = 12;
        visual.color = baseColor;
        baseScale = characterHeight / Mathf.Max(0.1f, visual.bounds.size.y);
        transform.localScale = Vector3.one * baseScale;
        Vector2[] directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        Vector2 spawn = player.transform.position;
        float bestDistance = -1f;
        foreach (Vector2 direction in directions)
        {
            Vector2 candidate = ArenaBounds.Clamp((Vector2)player.transform.position + direction * 5f, Vector2.one);
            float distance = Vector2.SqrMagnitude(candidate - (Vector2)player.transform.position);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                spawn = candidate;
            }
        }

        body.position = spawn;
        initialized = true;
    }

    private void FixedUpdate()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (player == null || player.IsGameOver || !initialized)
        {
            return;
        }

        float movementScale = Mathf.Sqrt(player.WorldScale);
        size = Mathf.Min(2.5f, movementScale);
        Vector2 position = body.position;
        Vector2 toPlayer = (Vector2)player.transform.position - position;
        stateTime -= Time.fixedDeltaTime;
        if (state == AttackState.Pursuing)
        {
            if (stateTime <= 0f && toPlayer.magnitude < 5.5f * movementScale)
            {
                chargeDirection = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.right;
                chaseVelocity = Vector2.zero;
                state = AttackState.Warning;
                stateTime = Mathf.Max(0.55f, warningDuration - player.GrowthStage * 0.05f);
            }
            else
            {
                chaseVelocity = Vector2.MoveTowards(chaseVelocity, toPlayer.normalized * pursuitSpeed * movementScale,
                    12f * movementScale * Time.fixedDeltaTime);
                body.MovePosition(Clamp(position + chaseVelocity * Time.fixedDeltaTime));
            }
        }
        else if (state == AttackState.Warning && stateTime <= 0f)
        {
            if (attackStyle == AttackStyle.Shoot)
            {
                HunterProjectile.Create(position, chargeDirection, 9f * movementScale, size);
                Recover();
            }
            else
            {
                state = AttackState.Charging;
                stateTime = ChargeDuration;
            }
        }
        else if (state == AttackState.Charging)
        {
            Vector2 next = Clamp(position + chargeDirection * dashSpeed * movementScale * Time.fixedDeltaTime);
            Vector2 travel = next - position;
            bool hitPlayer = false;
            int count = Physics2D.CircleCast(position, 0.32f * size, travel.normalized,
                new ContactFilter2D().NoFilter(), hits, travel.magnitude);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.GetComponentInParent<PlayerAnimationController>() == player)
                {
                    hitPlayer = player.TakeHit(chargeDirection);
                    break;
                }
            }

            body.MovePosition(next);
            if (hitPlayer || stateTime <= 0f || travel.sqrMagnitude < 0.000001f)
            {
                Recover();
            }
        }
        else if (state == AttackState.Recovering && stateTime <= 0f)
        {
            state = AttackState.Pursuing;
            stateTime = 0.6f;
        }
    }

    private void Recover()
    {
        state = AttackState.Recovering;
        stateTime = recoveryDuration;
    }

    private void LateUpdate()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (player == null || !initialized || player.IsGameOver)
        {
            return;
        }
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * baseScale * size, 1f - Mathf.Exp(-12f * Time.deltaTime));
        Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
        visual.flipX = (state == AttackState.Charging || state == AttackState.Warning ? chargeDirection.x : toPlayer.x) < 0f;
        Color targetColor = state == AttackState.Warning
            ? Color.Lerp(baseColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f))
            : state == AttackState.Recovering ? baseColor * new Color(0.65f, 0.65f, 0.65f, 1f) : baseColor;
        visual.color = Color.Lerp(visual.color, targetColor, 1f - Mathf.Exp(-20f * Time.deltaTime));
    }

    private Vector2 Clamp(Vector2 position) => ArenaBounds.Clamp(position, visual.bounds.extents,
        visual.bounds.center - transform.position);
}
