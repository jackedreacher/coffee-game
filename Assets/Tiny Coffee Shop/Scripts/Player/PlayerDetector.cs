using UnityEngine;

[RequireComponent(typeof(HoldFoodAbility))]
public class PlayerDetector : MonoBehaviour
{
    [Header(" Components ")]
    private HoldFoodAbility holdFoodAbility;
    [SerializeField] private NavigationAbility navigationAbility;

    private void Awake()
    {
        holdFoodAbility = GetComponent<HoldFoodAbility>();
        if (navigationAbility == null)
            navigationAbility = GetComponent<NavigationAbility>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (navigationAbility != null && navigationAbility.IsMoving)
            return;

        if (TryGetComponent(out Worker worker) && worker.CurrentTask is IdleTask)
            return;

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
