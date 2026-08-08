using UnityEngine;

// The sit-down counter: customers are served, then sent to a table.
// Serving is refused outright while every table is dirty or occupied
public class CashierStation : FoodServingStation
{
    [Header(" Elements ")]
    [SerializeField] private TableManager tableManager;

    protected override void ActuallyServeFood(CharacterStats characterStats)
    {
        if (!tableManager.IsAnyTableAvailable())
            return;

        servingTimer = 0;
        ServeCustomers(characterStats);
    }

    protected override void DequeueCustomer(Customer customer)
    {
        if (!tableManager.IsAnyTableAvailable())
            return;

        customerManager.Dequeue(customer);
        tableManager.HandleCustomerServed(customer);
    }
}
