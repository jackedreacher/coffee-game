using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(FoodServingCustomerManager))]
[RequireComponent(typeof(GuidGenerator))]
// Abstract on purpose: how a served customer leaves is the only thing that
// differs between a sit-down cashier and a grab-and-go pickup counter.
// CashierStation and PickupStation fill in that gap
public abstract class FoodServingStation : MonoBehaviour
{
    [Header(" Components ")]
    protected FoodServingCustomerManager customerManager;
    private GuidGenerator guidGenerator;

    [Header(" Elements ")]
    [SerializeField] private FoodDropZone dropZone;
    [SerializeField] private TaskRequester taskRequester;
    // Renamed from foodServedPrefab: it is both what workers fetch and the only
    // thing the drop zone will take. FormerlySerializedAs keeps the scene wiring
    [FormerlySerializedAs("foodServedPrefab")]
    [SerializeField] private SpawnableFood acceptedFood;
    [SerializeField] private Transform workerServingTargetPoint;
    [SerializeField] private CashFile cashFile;

    [Header(" Settings ")]
    [SerializeField] private float servingDelay;

    // protected so a subclass can decide NOT to reset it: a cashier with no free
    // table leaves the timer full and retries on the very next frame
    protected float servingTimer;
    private int workerCount;

    [Header(" Request Settings ")]
    private const float requestCheckDelay = 1f;
    private float requestCheckTimer;

    protected virtual void Awake()
    {
        customerManager = GetComponent<FoodServingCustomerManager>();
        guidGenerator = GetComponent<GuidGenerator>();

        // The zone has no business knowing this on its own — the station owns
        // what it serves, so it tells the zone what to let through
        dropZone.SetAcceptedFood(acceptedFood);
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
            new FillStationPlateauRequest(guidGenerator.GUID, acceptedFood, dropZone.WorkerTargetPosition)
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

        // The subclass resets servingTimer itself, since it may refuse to serve
        ActuallyServeFood(characterStats);
    }

    protected abstract void ActuallyServeFood(CharacterStats characterStats);

    // Where a served customer goes next: a table, or the exit
    protected abstract void DequeueCustomer(Customer customer);

    private FoodPosition GetFirstFullPosition()
    {
        return dropZone.GetFirstFullPosition();
    }

    private SpawnableFood Pop()
    {
        return dropZone.Pop();
    }

    // The part that is identical everywhere: take money, hand over one food.
    // Subclasses call this once they have decided they are willing to serve
    protected void ServeCustomers(CharacterStats characterStats)
    {
        Customer customerToServe = customerManager.PeekFirstCustomer();

        // Null once the queue hands out fixed slots: the servable customer can
        // vanish between the readiness check and here
        if (customerToServe == null)
            return;

        // Fully subjective formula — tweak baseRevenue to be more generous.
        // Max(1, ...) keeps a worker with no revenue upgrades earning something
        int baseRevenue = 1;
        float revenueMultiplier = Mathf.Max(1f, characterStats.Revenue);
        int revenue = Mathf.CeilToInt(baseRevenue * revenueMultiplier);

        SpawnableFood foodToServe = Pop();
        customerToServe.CollectFood(foodToServe);

        // Rung up per item, paid once. Moved below CollectFood so the order can
        // answer whether it is finished -- while it is not, RingUp answers 0 and
        // nothing flies. A customer buying three things is one sale
        int due = customerToServe.RingUp(revenue);

        if (due > 0)
            cashFile?.GenerateCash(due, customerToServe.transform.position);

        if (customerToServe.NeedsMoreFood())
            return;

        DequeueCustomer(customerToServe);
    }
}
