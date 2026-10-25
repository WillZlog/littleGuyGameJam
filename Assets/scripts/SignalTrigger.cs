using UnityEngine;

public class SignalTrigger : MonoBehaviour
{
    [SerializeField, Min(0f)] private float lifetime = 10f;
    [SerializeField, Min(0.01f)] private float nutrition = 1f;
    [SerializeField, Min(0f)] private float requiredPlayerSize;
    private bool consumed;

    private void Start()
    {
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    public void Configure(float foodValue, float minimumSize)
    {
        nutrition = Mathf.Max(0.01f, foodValue);
        requiredPlayerSize = Mathf.Max(0f, minimumSize);
    }

    private void OnCollisionEnter2D(Collision2D collision) => TryCollect(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
    private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

    private void TryCollect(Collider2D other)
    {
        if (consumed || Time.timeScale == 0f) return;
        PlayerAnimationController player = other.GetComponentInParent<PlayerAnimationController>();
        if (player == null || player.IsGameOver || player.scale < requiredPlayerSize) return;
        consumed = true;
        player.ConsumeFood(nutrition);
        Destroy(gameObject);
    }
}
