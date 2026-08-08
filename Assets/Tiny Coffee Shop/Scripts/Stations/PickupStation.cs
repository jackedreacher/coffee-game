using UnityEngine;

// The grab-and-go counter: no tables, no TableManager. Customers take their
// food and walk off to the exit point, where they are destroyed
public class PickupStation : FoodServingStation
{
    [Header(" Elements ")]
    [SerializeField] private Transform customerExitPoint;

    protected override void ActuallyServeFood(CharacterStats characterStats)
    {
        // Nothing to refuse for: there is no table to wait on
        servingTimer = 0;
        ServeCustomers(characterStats);
    }

    protected override void DequeueCustomer(Customer customer)
    {
        customerManager.Dequeue(customer);
        HandleCustomerServed(customer);
    }

    private void HandleCustomerServed(Customer customer)
    {
        customer.GoToThen(customerExitPoint.position, () => Destroy(customer.gameObject));
    }
}
