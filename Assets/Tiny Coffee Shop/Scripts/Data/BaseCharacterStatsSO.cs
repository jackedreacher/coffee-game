using UnityEngine;

[CreateAssetMenu(fileName = "BaseCharacterStatsSO", menuName = "Scriptable Objects/BaseCharacterStatsSO")]
public class BaseCharacterStatsSO : ScriptableObject
{
    [Header(" Stats ")]
    // Where an unupgraded character starts. The old ceiling of 4 was the reason
    // a player who felt slow could not simply be made faster
    [SerializeField][Range(1, 20)] private float speed = 3f;
    // Starts at 1 so a hand-serving game can limit the player to a single item
    [SerializeField][Range(1, 14)] private int capacity = 1;
    [SerializeField][Range(1, 5)] private float revenue = 1.5f;

    [Header(" Upgrade Per Level ")]
    // What one level adds. These were literals buried in CharacterStats, where
    // nobody balancing the game could reach them
    [SerializeField] private float speedPerLevel = .2f;
    [SerializeField] private int capacityPerLevel = 1;
    // The course uses 2 per level, which pays a maxed worker 15 bills per cup
    // against an unupgraded worker's 1 -- far too steep
    [SerializeField] private float revenuePerLevel = .2f;

    [Header(" Caps ")]
    // Where upgrading stops paying out. Speed especially: past a point the run
    // clip stops keeping up and the player skates past stations
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private int maxCapacity = 14;
    [SerializeField] private float maxRevenue = 5f;

    [Header(" UI ")]
    // One icon per stat, so a set of base stats fully describes how its
    // upgrade cards look. Lets workers and the player use different art
    [SerializeField] private Sprite speedIcon;
    [SerializeField] private Sprite capacityIcon;
    [SerializeField] private Sprite revenueIcon;

    #region Properties
    public float Speed => speed;
    public int Capacity => capacity;
    public float Revenue => revenue;

    public Sprite SpeedIcon => speedIcon;
    public Sprite CapacityIcon => capacityIcon;
    public Sprite RevenueIcon => revenueIcon;

    public float MaxSpeed => maxSpeed;
    public int MaxCapacity => maxCapacity;
    public float MaxRevenue => maxRevenue;
    #endregion

    // The formula lives next to the numbers it reads, so balancing is one asset
    // rather than an asset plus a script.
    //
    // The ceiling is where upgrades stop paying out, never a limit on the number
    // typed into the base field. A base of 20 against a cap of 8 has to mean 20,
    // not a character silently held to 8 by a field nobody was looking at
    public float SpeedAtLevel(int level)
    {
        return Mathf.Min(speed + level * speedPerLevel, Mathf.Max(speed, maxSpeed));
    }

    public int CapacityAtLevel(int level)
    {
        return Mathf.Min(capacity + level * capacityPerLevel, Mathf.Max(capacity, maxCapacity));
    }

    public float RevenueAtLevel(int level)
    {
        return Mathf.Min(revenue + level * revenuePerLevel, Mathf.Max(revenue, maxRevenue));
    }

    // statIndex follows the usual 0 = speed, 1 = capacity, 2 = revenue order
    public float GetStatValue(int statIndex)
    {
        if (statIndex == 0)
            return speed;

        if (statIndex == 1)
            return capacity;

        return revenue;
    }

    public Sprite GetStatIcon(int statIndex)
    {
        if (statIndex == 0)
            return speedIcon;

        if (statIndex == 1)
            return capacityIcon;

        return revenueIcon;
    }
}
