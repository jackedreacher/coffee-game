using System;
using UnityEngine;

// System is imported too, so Random on its own is ambiguous
using Random = UnityEngine.Random;

public class CustomerManager : MonoBehaviour
{
    public static CustomerManager Instance;

    [Header(" Elements ")]
    // Fallback for scenes that only ever had one look
    [SerializeField] private Customer customerPrefab;

    // Filled in, one per character variant. A random one walks in each time
    [SerializeField] private Customer[] customerPrefabs;

    [SerializeField] private Transform customerExitPoint;

    private void Awake()
    {
        Instance = this;
    }

    public Customer Pop(Vector3 spawnPosition)
    {
        return Instantiate(PickPrefab(), spawnPosition, Quaternion.identity, transform);
    }

    private Customer PickPrefab()
    {
        if (customerPrefabs == null || customerPrefabs.Length <= 0)
            return customerPrefab;

        Customer picked = customerPrefabs[Random.Range(0, customerPrefabs.Length)];

        return picked != null ? picked : customerPrefab;
    }

    public void HandleFiredCustomer(Customer customer)
    {
        customer.GetUpAndGo(customerExitPoint.position, () => HandleCustomerReachedExitPoint(customer));
    }

    private void HandleCustomerReachedExitPoint(Customer customer)
    {
        Destroy(customer.gameObject);
    }
}
