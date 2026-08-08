using UnityEngine;

// Bridge between the player's stat data and the components that consume it
public class PlayerStatsHandler : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Plateau plateau;

    // Not called Update, that would collide with MonoBehaviour's
    public void UpdateSelf(int[] statLevels)
    {
        characterStats.SetupStats(
            new Vector3Int(statLevels[0], statLevels[1], statLevels[2]));

        playerController.SetMoveSpeed(characterStats.Speed);
        plateau.UpdateMaxCapacity(characterStats.Capacity);

        // Revenue needs no wiring: FoodServingStation reads CharacterStats
        // straight off whoever is serving
    }
}
