using System;
using UnityEngine;

public class CustomerManager : MonoBehaviour
{
    public static CustomerManager Instance;

    [Header(" Elements ")]
    [SerializeField] private Customer customerPrefab;
    [SerializeField] private Transform customerExitPoint;

    private void Awake()
    {
        Instance = this;
    }

    public Customer Pop(Vector3 spawnPosition)
    {
        return Instantiate(customerPrefab, spawnPosition, Quaternion.identity, transform);
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
