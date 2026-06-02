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
        else if (other.TryGetComponent(out TableSet table))
            HandleTableTriggered(table);
    }

    private void HandleFoodSpawnerStationTriggered(FoodSpawnerStation station)
    {
        holdFoodAbility.HandleFoodSpawnerStation(station);
    }

    private void HandleFoodDropZoneTriggered(FoodDropZone dropZone)
    {
        holdFoodAbility.HandleFoodDropZone(dropZone);
    }

    private void HandleTableTriggered(TableSet table)
    {
        if (!table.IsDirty)
            return;

        if (!TryGetComponent(out HoldDishAbility holdDishAbility))
            return;

        if (!holdDishAbility.CanCollectDishes())
            return;

        table.GetCleanedBy(holdDishAbility);
    }
}
