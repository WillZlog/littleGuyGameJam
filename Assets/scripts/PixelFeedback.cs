using UnityEngine;

// A bounded pool of tiny world-space pixels: no text, colliders, or UI objects.
public class PixelFeedback : MonoBehaviour
{
    private struct Pixel
    {
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public Color color;
        public float remaining;
        public float lifetime;
        public float size;
    }
    private static PixelFeedback instance;
    private Pixel[] pixels;
    private Sprite sprite;
    private Texture2D texture;
    private int cursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    public static void Burst(Vector2 position, Color color, int count = 7, float scale = 1f)
    {
        if (instance == null)
        {
            instance = new GameObject("Pixel Feedback").AddComponent<PixelFeedback>();
            instance.pixels = new Pixel[64];
            instance.texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            instance.texture.SetPixel(0, 0, Color.white);
            instance.texture.Apply();
            instance.sprite = Sprite.Create(instance.texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        }
        for (int i = 0; i < Mathf.Clamp(count, 1, 16); i++)
        {
            int index = instance.cursor++ % instance.pixels.Length;
            var pixel = instance.pixels[index];
            if (pixel.renderer == null)
            {
                var go = new GameObject("Pixel");
                go.transform.SetParent(instance.transform, false);
                pixel.renderer = go.AddComponent<SpriteRenderer>();
                pixel.renderer.sprite = instance.sprite;
                pixel.renderer.sortingOrder = 20;
            }
            pixel.color = color;
            pixel.lifetime = pixel.remaining = Random.Range(0.18f, 0.32f);
            pixel.size = Random.Range(0.045f, 0.085f) * scale;
            pixel.velocity = Random.insideUnitCircle.normalized * Random.Range(1f, 2.8f) * scale;
            pixel.renderer.transform.position = position + Random.insideUnitCircle * 0.12f * scale;
            pixel.renderer.transform.localScale = Vector3.one * pixel.size;
            pixel.renderer.color = color;
            pixel.renderer.enabled = true;
            instance.pixels[index] = pixel;
        }
    }

    private void Update()
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if (pixel.remaining <= 0f || pixel.renderer == null) continue;
            float delta = Time.unscaledDeltaTime;
            pixel.remaining -= delta;
            pixel.renderer.transform.position += (Vector3)(pixel.velocity * delta);
            pixel.velocity *= Mathf.Exp(-7f * delta);
            float fade = Mathf.Clamp01(pixel.remaining / pixel.lifetime);
            pixel.renderer.color = new Color(pixel.color.r, pixel.color.g, pixel.color.b, pixel.color.a * fade);
            pixel.renderer.transform.localScale = Vector3.one * pixel.size * Mathf.Lerp(0.35f, 1f, fade);
            pixel.renderer.enabled = pixel.remaining > 0f;
            pixels[i] = pixel;
        }
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
        if (instance == this) instance = null;
    }
}
