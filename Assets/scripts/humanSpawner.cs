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
    [Header("Enemies")]
    [SerializeField] private ChargingHunter knifeEnemyPrefab;
    [SerializeField] private ChargingHunter starEnemyPrefab;
    [SerializeField, Range(1, 6)] private int maxEnemies = 4;
    [SerializeField, Min(0.5f)] private float enemySpawnInterval = 2.6f;
    [SerializeField, Min(0f)] private float firstEnemyDelay = 3f;

    [SerializeField, HideInInspector] private GameObject[] stageFoodPrefabs = new GameObject[0];

    [SerializeField, Min(0.05f)] private float spawnInterval = 1f;
    [SerializeField] private Vector2 spawnHalfExtents = new Vector2(4f, 4.8f);
    [SerializeField, Min(0f)] private float playerClearance = 0.7f;
    [SerializeField, Min(0f)] private float pickupClearance = 0.6f;
    [SerializeField] private bool useCameraBounds = true;
    [SerializeField, Min(0f)] private float cameraPadding = 0.6f;
    [SerializeField, Range(0, 6)] private int minimumVisibleHumans = 2;
    private readonly List<GameObject> livingHumans = new List<GameObject>();
    private float spawnTimer;
    private Camera spawnCamera;
    private int totalSpawned;
    private readonly List<ChargingHunter> enemies = new List<ChargingHunter>();
    private float enemySpawnTimer;
    private readonly RaycastHit2D[] enemySpawnHits = new RaycastHit2D[32];
    public int ActiveHumans => livingHumans.Count;
    public int ActiveEnemies => enemies.Count;
    public int VisibleHumans
    {
        get
        {
            int count = 0;
            foreach (GameObject human in livingHumans)
            {
                if (human == null) continue;
                SignalTrigger food = human.GetComponent<SignalTrigger>();
                if (food != null && food.IsVisible(spawnCamera) && !food.IsExpiring) count++;
            }
            return count;
        }
    }

    private void Start()
    {
        spawnCamera = Camera.main;
        enemySpawnTimer = firstEnemyDelay;
        if (knifeEnemyPrefab == null) knifeEnemyPrefab = Resources.Load<ChargingHunter>("HumanKnife");
        if (starEnemyPrefab == null) starEnemyPrefab = Resources.Load<ChargingHunter>("HumanStar");

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

        UpdateEnemies(player);

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
        {
            return;
        }

        spawnTimer = Mathf.Max(0.05f, spawnInterval * 1.25f);
        livingHumans.RemoveAll(item => item == null);
        int stage = player != null ? player.GrowthStage : 0;
        int populationLimit = Mathf.Min(Mathf.Max(1, maxHumans), 6);
        int visibleDeficit = Mathf.Max(0, Mathf.Min(minimumVisibleHumans, populationLimit) - VisibleHumans);
        // Recycle only targets that have been fully offscreen for a while, never a visible chase.
        for (int i = livingHumans.Count - 1; i >= 0 && livingHumans.Count + visibleDeficit > populationLimit; i--)
        {
            SignalTrigger food = livingHumans[i].GetComponent<SignalTrigger>();
            if (food == null || food.IsVisible(spawnCamera) || food.OffscreenTime < 2f) continue;
            GameObject old = livingHumans[i];
            livingHumans.RemoveAt(i);
            Destroy(old);
        }
        int count = Mathf.Min(Mathf.Max(Mathf.Max(1, humans), visibleDeficit), populationLimit - livingHumans.Count);
        for (int i = 0; i < count; i++)
        {
            int foodTier = Random.Range(Mathf.Max(0, stage - 1), stage + 1);
            float foodScale = player != null ? player.ScaleForStage(foodTier) : 1f;

            if (!TryFindPosition(player, foodScale, out Vector2 position, i < visibleDeficit))
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

    private void UpdateEnemies(PlayerAnimationController player)
    {
        if (player == null) return;
        if (enemies.RemoveAll(enemy => enemy == null) > 0)
            enemySpawnTimer = Mathf.Max(enemySpawnTimer, enemySpawnInterval);
        enemySpawnTimer -= Time.deltaTime;
        if (enemySpawnTimer > 0f || enemies.Count >= maxEnemies) return;
        enemySpawnTimer = enemySpawnInterval;
        int knives = 0, stars = 0;
        foreach (ChargingHunter enemy in enemies)
            if (enemy.Style == ChargingHunter.AttackStyle.Shoot) stars++; else knives++;
        ChargingHunter template = knives <= stars ? knifeEnemyPrefab : starEnemyPrefab;
        if (template == null) template = attackerPrefab;
        if (template == null) return;
        float clearance = Mathf.Clamp(4.5f * Mathf.Sqrt(player.WorldScale), 4.5f, 8f);
        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            Vector2 position = ArenaBounds.Clamp((Vector2)player.transform.position
                + direction * Random.Range(clearance, clearance + 2.5f), Vector2.one * 1.2f);
            if (Vector2.Distance(position, player.transform.position) < clearance) continue;
            int hitCount = Physics2D.Linecast(player.transform.position, position, ContactFilter2D.noFilter, enemySpawnHits);
            bool blocked = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D collider = enemySpawnHits[i].collider;
                if (!collider.isTrigger && collider.GetComponentInParent<PlayerAnimationController>() == null
                    && collider.GetComponentInParent<ChargingHunter>() == null) { blocked = true; break; }
            }
            if (blocked) continue;
            bool crowded = false;
            foreach (ChargingHunter enemy in enemies)
                if (Vector2.Distance(position, enemy.transform.position) < 2f) { crowded = true; break; }
            if (crowded) continue;
            ChargingHunter spawned = ChargingHunter.Create(null, player, template);
            spawned.SetSpawnPosition(position);
            enemies.Add(spawned);
            break;
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

    private bool TryFindPosition(PlayerAnimationController player, float foodScale, out Vector2 position, bool preferNearby = false)
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
        if (preferNearby && player != null)
        {
            float radius = 3.5f * Mathf.Sqrt(player.WorldScale);
            minimum = Vector2.Max(minimum, (Vector2)player.transform.position - Vector2.one * radius);
            maximum = Vector2.Min(maximum, (Vector2)player.transform.position + Vector2.one * radius);
        }
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

            if (player != null)
            {
                int hits = Physics2D.Linecast(player.transform.position, position, ContactFilter2D.noFilter, enemySpawnHits);
                bool blocked = false;
                for (int i = 0; i < hits; i++)
                {
                    Collider2D collider = enemySpawnHits[i].collider;
                    if (!collider.isTrigger && collider.GetComponentInParent<PlayerAnimationController>() == null
                        && collider.GetComponentInParent<ChargingHunter>() == null) { blocked = true; break; }
                }
                if (blocked) continue;
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
