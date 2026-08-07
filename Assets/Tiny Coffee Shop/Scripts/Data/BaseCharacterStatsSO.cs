using UnityEngine;

[CreateAssetMenu(fileName = "BaseCharacterStatsSO", menuName = "Scriptable Objects/BaseCharacterStatsSO")]
public class BaseCharacterStatsSO : ScriptableObject
{
    [Header(" Stats ")]
    [SerializeField][Range(1, 4)] private float speed;
    [SerializeField][Range(7, 14)] private int capacity;
    [SerializeField][Range(1, 2)] private float revenue;

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
    #endregion

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
