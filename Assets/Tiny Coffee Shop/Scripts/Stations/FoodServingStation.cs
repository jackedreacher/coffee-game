using UnityEngine;

[RequireComponent(typeof(FoodServingCustomerManager))]
[RequireComponent(typeof(GuidGenerator))]
public class FoodServingStation : MonoBehaviour
{
    [Header(" Components ")]
    private FoodServingCustomerManager customerManager;
    private GuidGenerator guidGenerator;

    [Header(" Elements ")]
    [SerializeField] private FoodDropZone dropZone;
    [SerializeField] private TableManager tableManager;
    [SerializeField] private TaskRequester taskRequester;
    [SerializeField] private SpawnableFood foodServedPrefab;
    [SerializeField] private Transform workerServingTargetPoint;
    [SerializeField] private CashFile cashFile;

    [Header(" Settings ")]
    [SerializeField] private float servingDelay;
    private float servingTimer;
    private int workerCount;

    [Header(" Request Settings ")]
    private const float requestCheckDelay = 1f;
    private float requestCheckTimer;

    private void Awake()
    {
        customerManager = GetComponent<FoodServingCustomerManager>();
        guidGenerator = GetComponent<GuidGenerator>();
    }

    private void Update()
    {
        HandleRequestTimer();
    }

    private void HandleRequestTimer()
    {
        if (requestCheckTimer < requestCheckDelay)
        {
            requestCheckTimer += Time.deltaTime;
            return;
        }

        requestCheckTimer = 0;
        CheckRequests();
    }

    private void CheckRequests()
    {
        if (!HasEnoughFood())
            EmitFillRequest();

        if (CanSendServeCustomersRequest())
            EmitServeCustomersRequest();
    }

    private bool HasEnoughFood() => dropZone.FoodCount > 4;

    private bool CanSendServeCustomersRequest()
    {
        return workerCount <= 0 &&
               customerManager.IsCustomerReadyToTakeFood() &&
               HasEnoughFood();
    }

    private void EmitFillRequest()
    {
        taskRequester.CreateTaskRequest(
            new FillStationPlateauRequest(guidGenerator.GUID, foodServedPrefab, dropZone.WorkerTargetPosition)
        );
    }

    private void EmitServeCustomersRequest()
    {
        taskRequester.CreateTaskRequest(
            new ServeCustomersRequest(guidGenerator.GUID, workerServingTargetPoint.position, dropZone)
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerDetector _) && !other.TryGetComponent(out Worker _))
            return;

        workerCount++;
    }

    private void OnTriggerStay(Collider other)
    {
        // The server's stats decide how much cash the serving generates, so
        // anything without them can't serve at all
        if (!other.TryGetComponent(out CharacterStats characterStats))
            return;

        if (workerCount > 0)
            HandleFoodServing(characterStats);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PlayerDetector _) && !other.TryGetComponent(out Worker _))
            return;

        workerCount--;
        workerCount = Mathf.Max(0, workerCount);
    }

    private void HandleFoodServing(CharacterStats characterStats)
    {
        if (servingTimer < servingDelay)
        {
            servingTimer += Time.deltaTime;
            return;
        }

        if (!customerManager.IsCustomerReadyToTakeFood())
            return;

        Customer customer = customerManager.PeekFirstCustomer();

        if (!customer.NeedsMoreFood())
        {
            servingTimer = 0;
            DequeueCustomer(customer);
            return;
        }

        if (GetFirstFullPosition() == null)
            return;

        servingTimer = 0;
        ServeFood(characterStats);
    }

    private FoodPosition GetFirstFullPosition()
    {
        return dropZone.GetFirstFullPosition();
    }

    private SpawnableFood Pop()
    {
        return dropZone.Pop();
    }

    private void ServeFood(CharacterStats characterStats)
    {
        Customer customerToServe = customerManager.PeekFirstCustomer();

        // Fully subjective formula — tweak baseRevenue to be more generous.
        // Max(1, ...) keeps a worker with no revenue upgrades earning something
        int baseRevenue = 1;
        float revenueMultiplier = Mathf.Max(1f, characterStats.Revenue);
        int revenue = Mathf.CeilToInt(baseRevenue * revenueMultiplier);

        cashFile?.GenerateCash(revenue);
        SpawnableFood foodToServe = Pop();
        customerToServe.CollectFood(foodToServe);

        if (customerToServe.NeedsMoreFood())
            return;

        DequeueCustomer(customerToServe);
    }

    private void DequeueCustomer(Customer customer)
    {
        if (!tableManager.IsAnyTableAvailable())
            return;

        customerManager.Dequeue();
        tableManager.HandleCustomerServed(customer);
    }
}
