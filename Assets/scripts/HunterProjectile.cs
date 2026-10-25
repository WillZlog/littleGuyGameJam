using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HunterProjectile : MonoBehaviour
{
    private Rigidbody2D body;
    private Vector2 direction;
    private float speed;
    private float size;
    private float remaining = 2.5f;
    private LineRenderer tracer;
    private Material material;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];

    public static void Create(Vector2 position, Vector2 direction, float speed, float size)
    {
        HunterProjectile shot = new GameObject("Hunter shot").AddComponent<HunterProjectile>();
        shot.body = shot.GetComponent<Rigidbody2D>();
        shot.body.bodyType = RigidbodyType2D.Kinematic;
        shot.body.gravityScale = 0f;
        shot.body.interpolation = RigidbodyInterpolation2D.Interpolate;
        shot.body.position = position;
        shot.direction = direction.normalized;
        shot.speed = speed;
        shot.size = size;
        shot.tracer = shot.gameObject.AddComponent<LineRenderer>();
        shot.material = new Material(Shader.Find("Sprites/Default"));
        shot.tracer.sharedMaterial = shot.material;
        shot.tracer.positionCount = 2;
        shot.tracer.useWorldSpace = true;
        shot.tracer.startWidth = shot.tracer.endWidth = 0.18f * size;
        shot.tracer.startColor = new Color(1f, 0.9f, 0.4f);
        shot.tracer.endColor = new Color(1f, 0.2f, 0.1f);
        shot.tracer.sortingOrder = 15;
    }

    private void FixedUpdate()
    {
        var player = PlayerAnimationController.Instance;
        if (player == null || player.IsGameOver)
        {
            return;
        }

        float distance = speed * Time.fixedDeltaTime;
        Vector2 next = body.position + direction * distance;
        int count = Physics2D.CircleCast(body.position, 0.15f * size, direction,
            new ContactFilter2D().NoFilter(), hits, distance);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.GetComponentInParent<PlayerAnimationController>() == player)
            {
                player.TakeHit(direction);
                Destroy(gameObject);
                return;
            }
        }

        body.MovePosition(next);
        remaining -= Time.fixedDeltaTime;
        if (remaining <= 0f || next.x < ArenaBounds.Min.x || next.x > ArenaBounds.Max.x
            || next.y < ArenaBounds.Min.y || next.y > ArenaBounds.Max.y)
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        tracer.SetPosition(0, transform.position);
        tracer.SetPosition(1, transform.position - (Vector3)(direction * 0.35f * size));
    }
    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
