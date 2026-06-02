using UnityEngine;

public class TableSet : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;
    private Chair[] chairs;

    [Header(" Components ")]
    private TableManager tableManager;

    [Header(" Settings ")]
    private bool isFull;
    private bool isDirty;

    public bool IsFull => isFull;
    public bool IsDirty => isDirty;

    private void Awake()
    {
        isFull = false;
        isDirty = false;
        chairs = GetComponentsInChildren<Chair>();
    }

    public void OnCustomerLeft()
    {
        // Check if all chairs are empty → mark table dirty
        for (int i = 0; i < chairs.Length; i++)
        {
            if (!chairs[i].IsEmpty)
                return;
        }

        isDirty = true;
        isFull = false;
        plateau.MarkAsDirty();
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
