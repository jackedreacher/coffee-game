using UnityEngine;

// Attached to any character that should be driven by stats: workers today,
// the player later on
public class CharacterStats : MonoBehaviour
{
    [Header(" Data ")]
    [SerializeField] private BaseCharacterStatsSO baseStats;

    [Header(" Settings ")]
    private float speed;
    private int capacity;
    private float revenue;

    #region Properties
    public float Speed => speed;
    public int Capacity => capacity;
    public float Revenue => revenue;
    #endregion

    private void Awake()
    {
        // Start from the base values, SetupStats() layers the level bonuses on top
        speed = baseStats.Speed;
        capacity = baseStats.Capacity;
        revenue = baseStats.Revenue;
    }

    // Per-level increments and the ceilings live on the stats asset now, so a
    // character can be rebalanced without touching a script
    public void SetupStats(Vector3Int statsLevels)
    {
        speed = baseStats.SpeedAtLevel(statsLevels.x);
        capacity = baseStats.CapacityAtLevel(statsLevels.y);
        revenue = baseStats.RevenueAtLevel(statsLevels.z);
    }
}

// Kept in this file on purpose: it is a single tiny helper, no need for its
// own script. One shared level feeds three stat rows in column order:
// lvl1 -> speed, lvl2 -> capacity, lvl3 -> revenue, lvl4 -> speed, ...
// so each row gains a level every 3 levels, offset by its position.
public static class WorkerUtilities
{
    public static Vector3Int GetStatsLevelsFromLevel(int level)
    {
        int speedLevel = Mathf.CeilToInt(Mathf.Max(0f, level) / 3f);
        int capacityLevel = Mathf.CeilToInt(Mathf.Max(0f, level - 1) / 3f);
        int revenueLevel = Mathf.CeilToInt(Mathf.Max(0f, level - 2) / 3f);

        return new Vector3Int(speedLevel, capacityLevel, revenueLevel);
    }
}
