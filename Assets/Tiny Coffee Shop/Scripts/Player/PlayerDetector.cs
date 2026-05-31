using UnityEngine;

[RequireComponent(typeof(HoldFoodAbility))]
public class PlayerDetector : MonoBehaviour
{
    [Header(" Components ")]
    private HoldFoodAbility holdFoodAbility;

    private void Awake()
    {
        holdFoodAbility = GetComponent<HoldFoodAbility>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent(out FoodSpawnerStation station))
            HandleFoodSpawnerStationTriggered(station);
        else if (other.TryGetComponent(out FoodDropZone dropZone))
            HandleFoodDropZoneTriggered(dropZone);
    }

    private void HandleFoodSpawnerStationTriggered(FoodSpawnerStation station)
    {
        holdFoodAbility.HandleFoodSpawnerStation(station);
    }

    private void HandleFoodDropZoneTriggered(FoodDropZone dropZone)
    {
        holdFoodAbility.HandleFoodDropZone(dropZone);
    }
}
