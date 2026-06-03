using UnityEngine;

[RequireComponent(typeof(GuidGenerator))]
public class TableSet : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;
    [SerializeField] private Transform workerTargetPoint;
    private Chair[] chairs;

    [Header(" Components ")]
    private TableManager tableManager;
    private GuidGenerator guidGenerator;

    public string GUID => guidGenerator.GUID;
    public Vector3 WorkerTargetPosition => workerTargetPoint.position;

    [Header(" Settings ")]
    private bool isFull;
    private bool isDirty;
    private int activeCustomerCount;

    [Header(" Food Timer ")]
    private float foodTimer;
    private int foodConsumed;

    public bool IsFull => isFull;
    public bool IsDirty => isDirty;

    private void Awake()
    {
        isFull = false;
        isDirty = false;
        chairs = GetComponentsInChildren<Chair>();
        guidGenerator = GetComponent<GuidGenerator>();
    }

    private void Update()
    {
        if (activeCustomerCount > 0)
            HandleFoodTimer();
    }

    private void HandleFoodTimer()
    {
        foodTimer += Time.deltaTime;

        if (foodTimer > (foodConsumed + 1) * Constants.TimeToConsumeFood)
            HideNextFood();
    }

    private void HideNextFood()
    {
        foodConsumed++;
        plateau.HideNextFood();
    }

    public void GetCleanedBy(HoldDishAbility holdDishAbility)
    {
        SpawnableFood[] dishes = plateau.PopAll();
        plateau.gameObject.SetActive(false);

        holdDishAbility.CollectDishes(dishes);

        for (int i = 0; i < chairs.Length; i++)
            chairs[i].Fix();

        isDirty = false;
        isFull = false;

        tableManager?.RemoveDirtyTable(this, holdDishAbility);
    }

    public void MarkPlateauDirty(int foodCount)
    {
        plateau.MarkAsDirty(foodCount);
    }

    public void OnCustomerLeft()
    {
        activeCustomerCount--;
        activeCustomerCount = Mathf.Max(0, activeCustomerCount);

        for (int i = 0; i < chairs.Length; i++)
        {
            if (!chairs[i].IsEmpty)
                return;
        }

        isDirty = true;
        isFull = false;
        foodTimer = 0;
        foodConsumed = 0;

        tableManager?.PushDirtyTable(this);
    }

    public void AcceptCustomer(Customer customer, TableManager tableManager)
    {
        this.tableManager = tableManager;

        Chair targetChair = GetFirstEmptyChair();

        if (targetChair == null)
        {
            Debug.LogError("TableSet: No empty chair was found. This should not happen.");
            return;
        }

        targetChair.MarkAsOccupied();
        CheckIfFull();

        customer.GoToThen(targetChair.CustomerTargetWalkPosition,
            () => HandleCustomerReachedChair(customer, targetChair));
    }

    private Chair GetFirstEmptyChair()
    {
        for (int i = 0; i < chairs.Length; i++)
        {
            if (chairs[i].IsEmpty)
                return chairs[i];
        }

        return null;
    }

    private void CheckIfFull()
    {
        for (int i = 0; i < chairs.Length; i++)
        {
            if (chairs[i].IsEmpty)
            {
                isFull = false;
                return;
            }
        }

        isFull = true;
    }

    private void HandleCustomerReachedChair(Customer customer, Chair targetChair)
    {
        activeCustomerCount++;

        for (int i = 0; i < customer.FoodTakenCount; i++)
        {
            SpawnableFood food = customer.Pop();

            if (food == null)
                break;

            plateau.gameObject.SetActive(true);
            plateau.Push(food);
        }

        targetChair.PushCustomer(customer);
    }
}
