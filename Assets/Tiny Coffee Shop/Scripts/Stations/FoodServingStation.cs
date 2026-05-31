using UnityEngine;

[RequireComponent(typeof(FoodServingCustomerManager))]
public class FoodServingStation : MonoBehaviour
{
    [Header(" Components ")]
    private FoodServingCustomerManager customerManager;

    [Header(" Elements ")]
    [SerializeField] private FoodDropZone dropZone;

    [Header(" Settings ")]
    [SerializeField] private float servingDelay;
    private float servingTimer;
    private int workerCount;

    private void Awake()
    {
        customerManager = GetComponent<FoodServingCustomerManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerDetector playerDetector))
            return;

        workerCount++;
    }

    private void OnTriggerStay(Collider other)
    {
        if (workerCount > 0)
            HandleFoodServing();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PlayerDetector playerDetector))
            return;

        workerCount--;
        workerCount = Mathf.Max(0, workerCount);
    }

    private void HandleFoodServing()
    {
        if (servingTimer < servingDelay)
        {
            servingTimer += Time.deltaTime;
            return;
        }

        if (!customerManager.IsCustomerReadyToTakeFood())
            return;

        if (!customerManager.PeekFirstCustomer().NeedsMoreFood())
            return;

        if (GetFirstFullPosition() == null)
            return;

        servingTimer = 0;
        ServeFood();
    }

    private FoodPosition GetFirstFullPosition()
    {
        return dropZone.GetFirstFullPosition();
    }

    private SpawnableFood Pop()
    {
        return dropZone.Pop();
    }

    private void ServeFood()
    {
        Customer customerToServe = customerManager.PeekFirstCustomer();
        SpawnableFood foodToServe = Pop();
        customerToServe.CollectFood(foodToServe);

        if (customerToServe.NeedsMoreFood())
            return;

        DequeueCustomer(customerToServe);
    }

    private void DequeueCustomer(Customer customer)
    {
        customerManager.DequeueCustomer(customer);
    }
}
