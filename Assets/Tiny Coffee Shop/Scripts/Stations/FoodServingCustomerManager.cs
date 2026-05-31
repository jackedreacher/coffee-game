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

        Vector3 targetPosition = GetLastCustomerPosition();
        newCustomer.Initialize(targetPosition, -queueSpacing.normalized);
    }

    private Vector3 GetLastCustomerPosition()
    {
        return queueStartPoint.position + queueSpacing * (customers.Count - 1);
    }
}
