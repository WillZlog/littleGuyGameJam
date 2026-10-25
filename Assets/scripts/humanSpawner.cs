using System.Collections.Generic;
using UnityEngine;

public class humanSpawner : MonoBehaviour
{
    [Min(1)] public int humans = 1;

    [Min(1)] public int maxHumans = 30;

    [SerializeField] private GameObject human;

    [SerializeField] private GameObject stage1Prefab;

    [SerializeField] private GameObject stage2Prefab;

    [SerializeField] private GameObject stage3Prefab;

    [SerializeField] private GameObject stage4Prefab;

    [SerializeField] private GameObject stage5Prefab;

    [SerializeField] private GameObject stage6Prefab;
    [SerializeField] private ChargingHunter attackerPrefab;

    [SerializeField, HideInInspector] private GameObject[] stageFoodPrefabs = new GameObject[0];

    [SerializeField, Min(0.05f)] private float spawnInterval = 1f;
    [SerializeField] private Vector2 spawnHalfExtents = new Vector2(4f, 4.8f);
    [SerializeField, Min(0f)] private float playerClearance = 0.7f;
    [SerializeField, Min(0f)] private float pickupClearance = 0.6f;
    [SerializeField] private bool useCameraBounds = true;
    [SerializeField, Min(0f)] private float cameraPadding = 0.6f;
    private readonly List<GameObject> livingHumans = new List<GameObject>();
    private float spawnTimer;
    private Camera spawnCamera;
    private int totalSpawned;
    private ChargingHunter hunter;
    public int ActiveHumans => livingHumans.Count;

    private void Start()
    {
        spawnCamera = Camera.main;

        if (GetStagePrefab(0) != null)
        {
            return;
        }

        Debug.LogWarning("Assign Stage 1 Prefab or a fallback Human prefab to the spawner.", this);
        enabled = false;
    }

    private void Update()
    {
        PlayerAnimationController player = PlayerAnimationController.Instance;
        if (Time.timeScale == 0f || (player != null && player.IsGameOver))
        {
            return;
        }

        if (hunter == null && player != null && player.SurvivalTime >= 3f)
        {
            GameObject prefab = GetStagePrefab(0);
            SpriteRenderer[] renderers = prefab != null ? prefab.GetComponentsInChildren<SpriteRenderer>() : new SpriteRenderer[0];
            Sprite hunterSprite = null;
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer.sprite != null)
                {
                    hunterSprite = renderer.sprite;
                    break;
                }
            }

            if (hunterSprite != null)
            {
                hunter = ChargingHunter.Create(hunterSprite, player, attackerPrefab);
            }
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
        {
            return;
        }

        spawnTimer = Mathf.Max(0.05f, spawnInterval * 1.25f);
        livingHumans.RemoveAll(item => item == null);
        int stage = player != null ? player.GrowthStage : 0;
        int populationLimit = Mathf.Min(Mathf.Max(1, maxHumans), 6);
        int count = Mathf.Min(Mathf.Max(1, humans), populationLimit - livingHumans.Count);
        for (int i = 0; i < count; i++)
        {
            int foodTier = Random.Range(Mathf.Max(0, stage - 1), stage + 1);
            float foodScale = player != null ? player.ScaleForStage(foodTier) : 1f;

            if (!TryFindPosition(player, foodScale, out Vector2 position))
            {
                break;
            }

            GameObject prefab = GetStagePrefab(foodTier);
            if (prefab == null)
            {
                continue;
            }

            GameObject clone = Instantiate(prefab, position, Quaternion.identity);
            clone.transform.localScale *= foodScale;
            SignalTrigger food = clone.GetComponent<SignalTrigger>();

            if (food != null)
            {
                food.Configure(foodScale, 0f, ++totalSpawned % 7 == 0);
            }

            livingHumans.Add(clone);
        }
    }

    private GameObject GetStagePrefab(int tier)
    {
        GameObject assigned;
        switch (tier)
        {
            case 0:
                assigned = stage1Prefab;
                break;
            case 1:
                assigned = stage2Prefab;
                break;
            case 2:
                assigned = stage3Prefab;
                break;
            case 3:
                assigned = stage4Prefab;
                break;
            case 4:
                assigned = stage5Prefab;
                break;
            case 5:
                assigned = stage6Prefab;
                break;
            default:
                assigned = null;
                break;
        }

        if (assigned != null)
        {
            return assigned;
        }

        if (stageFoodPrefabs != null && tier >= 0 && tier < stageFoodPrefabs.Length && stageFoodPrefabs[tier] != null)
        {
            return stageFoodPrefabs[tier];
        }

        if (human != null)
        {
            return human;
        }

        if (stage1Prefab != null)
        {
            return stage1Prefab;
        }

        return stageFoodPrefabs != null && stageFoodPrefabs.Length > 0 ? stageFoodPrefabs[0] : null;
    }

    private bool TryFindPosition(PlayerAnimationController player, float foodScale, out Vector2 position)
    {
        Vector2 center = transform.position;
        Vector2 extents = new Vector2(Mathf.Abs(spawnHalfExtents.x), Mathf.Abs(spawnHalfExtents.y));

        if (useCameraBounds && spawnCamera != null && spawnCamera.orthographic)
        {
            center = spawnCamera.transform.position;
            extents = new Vector2(spawnCamera.orthographicSize * spawnCamera.aspect, spawnCamera.orthographicSize);
        }

        extents = Vector2.Max(Vector2.zero, extents - Vector2.one * (cameraPadding * foodScale));
        Vector2 minimum = Vector2.Max(center - extents, ArenaBounds.Min + Vector2.one * 0.6f);
        Vector2 maximum = Vector2.Min(center + extents, ArenaBounds.Max - Vector2.one * 0.6f);
        if (minimum.x > maximum.x || minimum.y > maximum.y)
        {
            position = default;
            return false;
        }

        Collider2D playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        float clearance = playerClearance * foodScale + (playerCollider != null ? playerCollider.bounds.extents.magnitude : 0f);

        for (int attempt = 0; attempt < 16; attempt++)
        {
            position = new Vector2(Random.Range(minimum.x, maximum.x), Random.Range(minimum.y, maximum.y));

            if (player != null && Vector2.Distance(position, player.transform.position) < clearance)
            {
                continue;
            }

            bool crowded = false;
            foreach (GameObject item in livingHumans)
            {
                if (item != null && Vector2.Distance(position, item.transform.position) < pickupClearance * foodScale)
                {
                    crowded = true;
                    break;
                }
            }

            if (!crowded)
            {
                return true;
            }
        }

        position = default;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(spawnHalfExtents.x * 2f, spawnHalfExtents.y * 2f, 0f));
    }
}
