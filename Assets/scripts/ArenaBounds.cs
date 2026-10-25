using UnityEngine;

public static class ArenaBounds
{
    public static readonly Vector2 Min = new Vector2(-15f, -10f);
    public static readonly Vector2 Max = new Vector2(15f, 10f);

    public static Vector2 Clamp(Vector2 position, Vector2 halfSize, Vector2 offset = default)
    {
        Vector2 min = Min + halfSize - offset;
        Vector2 max = Max - halfSize - offset;
        return new Vector2(min.x <= max.x ? Mathf.Clamp(position.x, min.x, max.x) : (min.x + max.x) * 0.5f,
            min.y <= max.y ? Mathf.Clamp(position.y, min.y, max.y) : (min.y + max.y) * 0.5f);
    }
}
