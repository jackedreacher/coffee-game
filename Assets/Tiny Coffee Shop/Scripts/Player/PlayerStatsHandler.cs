using UnityEngine;

// Bridge between the player's stat data and the components that consume it
public class PlayerStatsHandler : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Plateau plateau;

    private bool hasApplied;

    // The upgrade desk is what normally pushes stats onto the player, and it
    // sits inside a LockedElement. While that is switched off its Start never
    // runs, nothing applies the stats asset at all, and the player quietly runs
    // on whatever numbers happen to be serialised on its own components --
    // which is why editing the asset appeared to do nothing.
    //
    // Guarded rather than unconditional: Start order between two components is
    // undefined, and clobbering the desk's saved levels with a row of zeroes
    // would be a worse bug than the one being fixed
    private void Start()
    {
        if (!hasApplied)
            UpdateSelf(new int[3]);
    }

    // Not called Update, that would collide with MonoBehaviour's
    public void UpdateSelf(int[] statLevels)
    {
        hasApplied = true;

        characterStats.SetupStats(
            new Vector3Int(statLevels[0], statLevels[1], statLevels[2]));

        playerController.SetMoveSpeed(characterStats.Speed);
        plateau.UpdateMaxCapacity(characterStats.Capacity);

        // Revenue needs no wiring: FoodServingStation reads CharacterStats
        // straight off whoever is serving
    }
}
