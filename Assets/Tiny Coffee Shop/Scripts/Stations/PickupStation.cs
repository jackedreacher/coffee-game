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
        // Gone either way. They are out of the queue by this line, so nothing
        // will ever ask them to move again -- a walk that fails to start leaves
        // them standing in the shop for the rest of the game
        if (!customer.GoToThen(customerExitPoint.position, () => Destroy(customer.gameObject)))
            Destroy(customer.gameObject);
    }
}
