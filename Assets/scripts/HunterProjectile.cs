using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HunterProjectile : MonoBehaviour
{
    private Rigidbody2D body;
    private Vector2 direction;
    private float speed;
    private float size;
    private float remaining = 4f;
    private SpriteRenderer visual;
    private Transform owner;
    private bool spent;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

    public static void Create(Vector2 position, Vector2 direction, float speed, float size,
        Sprite sprite = null, Transform owner = null)
    {
        if (sprite == null) sprite = Resources.Load<Sprite>("ThrowingStar");
        if (sprite == null) return;
        HunterProjectile shot = new GameObject("Throwing Star").AddComponent<HunterProjectile>();
        shot.body = shot.GetComponent<Rigidbody2D>();
        shot.body.bodyType = RigidbodyType2D.Kinematic;
        shot.body.gravityScale = 0f;
        shot.body.interpolation = RigidbodyInterpolation2D.Interpolate;
        shot.body.constraints = RigidbodyConstraints2D.FreezeRotation;
        shot.body.position = position;
        shot.direction = direction.normalized;
        shot.speed = speed;
        shot.size = size;
        shot.owner = owner;
        var collider = shot.gameObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.15f * size;
        var art = new GameObject("Spinning Star Art");
        art.transform.SetParent(shot.transform, false);
        shot.visual = art.AddComponent<SpriteRenderer>();
        shot.visual.sprite = sprite;
        shot.visual.sortingOrder = 15;
        art.transform.localScale = Vector3.one * (0.42f * size / Mathf.Max(0.1f, sprite.bounds.size.y));
    }

    private void FixedUpdate()
    {
        var player = PlayerAnimationController.Instance;
        if (spent || player == null || player.IsGameOver) return;
        float distance = speed * Time.fixedDeltaTime;
        Vector2 next = body.position + direction * distance;
        // Sweep as well as triggering, so fast stars cannot tunnel through bodies or walls.
        int count = Physics2D.CircleCast(body.position, 0.15f * size, direction, ContactFilter2D.noFilter, hits, distance);
        for (int i = 0; i < count; i++) if (TryImpact(hits[i].collider)) return;
        body.MovePosition(next);
        remaining -= Time.fixedDeltaTime;
        if (remaining <= 0f || next.x < ArenaBounds.Min.x || next.x > ArenaBounds.Max.x
            || next.y < ArenaBounds.Min.y || next.y > ArenaBounds.Max.y) Destroy(gameObject);
    }

    private bool TryImpact(Collider2D collider)
    {
        if (spent || collider == null || collider.transform.IsChildOf(transform)
            || (owner != null && collider.transform.IsChildOf(owner))) return false;
        ChargingHunter enemy = collider.GetComponentInParent<ChargingHunter>();
        if (enemy != null)
        {
            enemy.Stun();
            spent = true;
            Destroy(gameObject);
            return true;
        }
        PlayerAnimationController player = collider.GetComponentInParent<PlayerAnimationController>();
        if (player != null)
        {
            player.TakeHit(direction);
            spent = true;
        }
        else if (!collider.isTrigger) spent = true;
        if (spent) Destroy(gameObject);
        return spent;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.timeScale > 0f) TryImpact(other);
    }

    private void LateUpdate()
    {
        var player = PlayerAnimationController.Instance;
        if (visual != null && player != null && !player.IsGameOver)
            visual.transform.Rotate(0f, 0f, -720f * Time.deltaTime);
    }
}
