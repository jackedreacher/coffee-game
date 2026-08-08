using UnityEngine;
using Random = UnityEngine.Random;

public class FoodServingCustomerManager : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform queueStartPoint;

    [Header(" Settings ")]
    [SerializeField] private int maxCustomers;

    // Offset from one ROW of customers to the next, away from the counter
    [SerializeField] private Vector3 queueSpacing;

    // How many customers stand shoulder to shoulder before a new row starts.
    // 1 keeps the classic single file, which is what the coffee shop uses
    [SerializeField][Min(1)] private int customersPerRow = 1;

    // Offset between two neighbours inside the same row
    [SerializeField] private Vector3 sideSpacing = new Vector3(0f, 0f, 1f);

    [SerializeField] private Vector2Int minMaxCustomerFoodCount;

    // Fixed standing spots rather than a queue. A customer keeps their spot
    // for as long as they are here, so nobody shuffles sideways when someone
    // else is served — only the column that opened up moves forward
    private Customer[] slots;

    // Handed over by OrderCounter in Awake, so the orders a customer can ask
    // for are always exactly what the counter is able to serve. Left null by
    // the single-item coffee shop stations, which fall back to the old behaviour
    private SpawnableFood[] possibleOrders;

    private const float arrivedDistance = .1f;

    private void Awake()
    {
        slots = new Customer[Mathf.Max(1, maxCustomers)];
    }

    private void Start()
    {
        StartSpawningCustomers();
    }

    private void StartSpawningCustomers()
    {
        InvokeRepeating("SpawnNewCustomer", 1f, 1f);
    }

    private void SpawnNewCustomer()
    {
        int slot = GetFirstEmptySlot();

        if (slot < 0)
            return;

        Customer newCustomer = CustomerManager.Instance.Pop(spawnPoint.position);
        newCustomer.name = "Customer " + Random.Range(0, 1000);

        slots[slot] = newCustomer;

        int foodCount = Random.Range(minMaxCustomerFoodCount.x, minMaxCustomerFoodCount.y + 1);
        Vector3 targetPosition = GetTargetCustomerPosition(slot);

        if (possibleOrders == null || possibleOrders.Length <= 0)
        {
            newCustomer.Initialize(foodCount, targetPosition, -QueueOffset.normalized);
            return;
        }

        SpawnableFood order = possibleOrders[Random.Range(0, possibleOrders.Length)];
        newCustomer.Initialize(foodCount, targetPosition, -QueueOffset.normalized, order);
    }

    // Called by OrderCounter before the first spawn tick fires
    public void SetPossibleOrders(SpawnableFood[] orders)
    {
        possibleOrders = orders;
    }

    // Both spacings are authored in the queue start point's LOCAL space, so
    // rotating the station turns the whole block with it. In world space a
    // rotated station lines its customers up into a wall and pathing fails
    private Vector3 QueueOffset => queueStartPoint.rotation * queueSpacing;
    private Vector3 SideOffset => queueStartPoint.rotation * sideSpacing;

    private Vector3 GetTargetCustomerPosition(int index)
    {
        int row = index / customersPerRow;
        int column = index % customersPerRow;

        // Centre the row on the start point instead of growing to one side,
        // so a 3-wide block sits square in front of the counter. With
        // customersPerRow = 1 this term is zero and nothing moves
        float centredColumn = column - (customersPerRow - 1) * .5f;

        return queueStartPoint.position + QueueOffset * row + SideOffset * centredColumn;
    }

    private int GetFirstEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                return i;
        }

        return -1;
    }

    // Only the front row can be reached across the counter
    public int ServableSlotCount => Mathf.Min(customersPerRow, slots.Length);

    // Null while the customer is still walking to their spot, so a slow one
    // never blocks the neighbours standing next to them
    public Customer GetArrivedCustomer(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return null;

        Customer customer = slots[slot];

        if (customer == null)
            return null;

        float distance = Vector3.Distance(
            customer.transform.position.With(y: 0),
            GetTargetCustomerPosition(slot).With(y: 0));

        return distance < arrivedDistance ? customer : null;
    }

    // Used by tap-to-serve. Nearest rather than an exact hit, because the
    // customer prefab has no collider and a finger is not a pixel
    public Customer FindNearestCustomer(Vector3 worldPoint, float maxDistance)
    {
        Customer nearest = null;
        float nearestDistance = maxDistance;

        for (int i = 0; i < slots.Length; i++)
        {
            Customer customer = slots[i];

            if (customer == null)
                continue;

            float distance = Vector3.Distance(
                customer.transform.position.With(y: 0),
                worldPoint.With(y: 0));

            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = customer;
        }

        return nearest;
    }

    // Lets a server ask "is this customer mine?" without exposing the slots
    public bool Contains(Customer customer)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == customer)
                return true;
        }

        return false;
    }

    private int GetServableSlot()
    {
        for (int i = 0; i < ServableSlotCount; i++)
        {
            if (GetArrivedCustomer(i) != null)
                return i;
        }

        return -1;
    }

    public bool IsCustomerReadyToTakeFood()
    {
        return GetServableSlot() >= 0;
    }

    public Customer PeekFirstCustomer()
    {
        int slot = GetServableSlot();

        return slot < 0 ? null : slots[slot];
    }

    // Takes the customer rather than assuming the front of a line: the order
    // counter serves whoever it happens to have food for, not who arrived first
    public void Dequeue(Customer customer)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != customer)
                continue;

            slots[i] = null;
            AdvanceColumn(i);
            return;
        }
    }

    // Only the freed column steps forward one row. Every other customer keeps
    // standing exactly where they were
    private void AdvanceColumn(int freedSlot)
    {
        for (int i = freedSlot; i + customersPerRow < slots.Length; i += customersPerRow)
        {
            Customer behind = slots[i + customersPerRow];

            slots[i] = behind;
            slots[i + customersPerRow] = null;

            if (behind != null)
                behind.GoTo(GetTargetCustomerPosition(i));
        }
    }
}
