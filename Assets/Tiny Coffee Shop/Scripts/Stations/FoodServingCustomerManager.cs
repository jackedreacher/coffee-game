using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class FoodServingCustomerManager : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform queueStartPoint;

    [Header(" Settings ")]
    [SerializeField] private int maxCustomers;
    [SerializeField] private Vector3 queueSpacing;
    [SerializeField] private Vector2Int minMaxCustomerFoodCount;
    private Queue<Customer> customers = new Queue<Customer>();

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
        if (customers.Count >= maxCustomers)
            return;

        Customer newCustomer = CustomerManager.Instance.Pop(spawnPoint.position);
        newCustomer.name = "Customer " + Random.Range(0, 1000);

        customers.Enqueue(newCustomer);

        int foodCount = Random.Range(minMaxCustomerFoodCount.x, minMaxCustomerFoodCount.y + 1);
        Vector3 targetPosition = GetLastCustomerPosition();
        newCustomer.Initialize(foodCount, targetPosition, -queueSpacing.normalized);
    }

    private Vector3 GetLastCustomerPosition()
    {
        return queueStartPoint.position + queueSpacing * (customers.Count - 1);
    }

    public Customer PeekFirstCustomer()
    {
        return customers.Peek();
    }

    public void DequeueCustomer(Customer customer)
    {
        customers.Dequeue();
    }

    public bool IsCustomerReadyToTakeFood()
    {
        if (customers.Count <= 0)
            return false;

        Customer firstCustomer = customers.Peek();

        float distance = Vector3.Distance(
            firstCustomer.transform.position.With(y: 0),
            queueStartPoint.position.With(y: 0));

        return distance < .1f;
    }
}
