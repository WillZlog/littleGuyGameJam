using UnityEngine;

public class SignalTrigger : MonoBehaviour
{
    [SerializeField, Min(0f)] private float lifetime = 10f;
    [SerializeField, Min(0.01f)] private float nutrition = 1f;
    [SerializeField, Min(0f)] private float requiredPlayerSize;
    private bool consumed;
    [SerializeField] private bool fleeFromPlayer = true;
    private Rigidbody2D body;
    private SpriteRenderer[] sprites;
    private Color[] originalColors;
    private Vector2 wanderDirection;
    private float turnTimer;
    private float movementScale = 1f;
    private bool golden;
    private Vector2 fleeVelocity;
    private Collider2D[] colliders;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        colliders = GetComponentsInChildren<Collider2D>();
        sprites = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            originalColors[i] = sprites[i].color;
        }

        if (body != null && fleeFromPlayer)
        {
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        wanderDirection = Random.insideUnitCircle.normalized;
    }

    private void Start()
    {
        SetPosition(ConstrainPosition(transform.position));
        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    public void Configure(float foodValue, float minimumSize, bool isGolden = false)
    {
        golden = isGolden;
        nutrition = Mathf.Max(0.01f, foodValue) * (golden ? 3f : 1f);
        movementScale = Mathf.Sqrt(Mathf.Max(1f, foodValue));
        requiredPlayerSize = Mathf.Max(0f, minimumSize);
    }

    private void Update()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (!golden || (player != null && player.IsGameOver))
        {
            return;
        }

        Color tint = Color.Lerp(new Color(1f, 0.65f, 0.12f), new Color(1f, 1f, 0.65f),
            0.5f + 0.5f * Mathf.Sin(Time.time * 8f));
        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i].color = originalColors[i] * tint;
        }
    }

    private void FixedUpdate()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (consumed || (player != null && player.IsGameOver))
        {
            return;
        }

        if (!fleeFromPlayer || player == null)
        {
            SetPosition(ConstrainPosition(transform.position));
            return;
        }
        turnTimer -= Time.fixedDeltaTime;
        if (turnTimer <= 0f)
        {
            wanderDirection = Random.insideUnitCircle.normalized;
            turnTimer = Random.Range(0.7f, 1.6f);
        }
        Vector2 position = body != null ? body.position : (Vector2)transform.position;
        Vector2 away = position - (Vector2)player.transform.position;
        bool scared = away.sqrMagnitude < 9f * player.WorldScale * player.WorldScale;
        Vector2 direction = scared ? (away.normalized + wanderDirection * 0.3f).normalized : wanderDirection;
        float speed = (scared ? (golden ? 3.8f : 2.8f) : 0.65f) * movementScale;
        fleeVelocity = Vector2.MoveTowards(fleeVelocity, direction * speed, 14f * movementScale * Time.fixedDeltaTime);
        Vector2 next = position + fleeVelocity * Time.fixedDeltaTime;
        Vector2 constrained = ConstrainPosition(next);
        if (Mathf.Abs(constrained.x - next.x) > 0.001f)
        {
            direction.x *= -1f;
            wanderDirection.x *= -1f;
            fleeVelocity.x *= -1f;
        }
        if (Mathf.Abs(constrained.y - next.y) > 0.001f)
        {
            direction.y *= -1f;
            wanderDirection.y *= -1f;
            fleeVelocity.y *= -1f;
        }
        next = ConstrainPosition(position + fleeVelocity * Time.fixedDeltaTime);
        if (body != null)
        {
            body.MovePosition(next);
        }
        else
        {
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }
    }

    private Vector2 ConstrainPosition(Vector2 position)
    {
        Bounds footprint = new Bounds(transform.position, Vector3.zero);
        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite.enabled && sprite.sprite != null)
            {
                footprint.Encapsulate(sprite.bounds);
            }
        }

        foreach (Collider2D collider in colliders)
        {
            if (collider.enabled)
            {
                footprint.Encapsulate(collider.bounds);
            }
        }

        return ArenaBounds.Clamp(position, footprint.extents, footprint.center - transform.position);
    }

    private void SetPosition(Vector2 position)
    {
        if (body != null)
        {
            body.position = position;
        }
        else
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (consumed || Time.timeScale == 0f)
        {
            return;
        }

        PlayerAnimationController player = other.GetComponentInParent<PlayerAnimationController>();
        if (player == null || player.IsGameOver || player.scale < requiredPlayerSize)
        {
            return;
        }

        consumed = true;
        player.ConsumeFood(nutrition);
        Destroy(gameObject);
    }
}
