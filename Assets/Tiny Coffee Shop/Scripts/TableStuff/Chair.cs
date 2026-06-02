using UnityEngine;
using Random = UnityEngine.Random;

public class Chair : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Transform customerTargetPoint;
    [SerializeField] private Transform customerTargetWalkPoint;
    [SerializeField] private Transform render;

    [Header(" Components ")]
    private TableSet tableSet;

    [Header(" Settings ")]
    private bool isEmpty;
    private Customer customer;

    [Header(" Timer ")]
    private float eatTimer;
    private bool isEating;

    public bool IsEmpty => isEmpty;
    public Vector3 CustomerTargetWalkPosition => customerTargetWalkPoint.position;

    private void Awake()
    {
        isEmpty = true;
        tableSet = GetComponentInParent<TableSet>();
    }

    private void Update()
    {
        if (!isEating)
            return;

        eatTimer -= Time.deltaTime;

        if (eatTimer <= 0)
            FireCustomer();
    }

    public void MarkAsOccupied()
    {
        isEmpty = false;
    }

    public void PushCustomer(Customer customer)
    {
        this.customer = customer;
        customer.transform.SetParent(transform);
        customer.SitDown(customerTargetPoint.position, transform.forward);

        eatTimer = customer.FoodTakenCount * Constants.TimeToConsumeFood;
        isEating = true;
    }

    private void FireCustomer()
    {
        isEating = false;

        Customer firedCustomer = Pop();

        if (firedCustomer != null)
            CustomerManager.Instance.HandleFiredCustomer(firedCustomer);

        int foodCount = firedCustomer != null ? firedCustomer.FoodTakenCount : 0;

        Messup();
        tableSet.MarkPlateauDirty(foodCount);
        tableSet.OnCustomerLeft();
    }

    public void Messup()
    {
        float sign = Mathf.Sign(Random.Range(-1f, 1f));
        render.localRotation = Quaternion.Euler(0, 60 * sign, 0);
    }

    public Customer Pop()
    {
        isEmpty = true;

        if (customer == null)
            return null;

        customer.transform.SetParent(null);

        Customer customerToReturn = customer;
        customer = null;

        return customerToReturn;
    }
}
