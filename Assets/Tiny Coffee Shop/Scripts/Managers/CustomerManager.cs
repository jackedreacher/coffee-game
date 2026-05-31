using UnityEngine;

public class CustomerManager : MonoBehaviour
{
    public static CustomerManager Instance;

    [Header(" Elements ")]
    [SerializeField] private Customer customerPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public Customer Pop(Vector3 spawnPosition)
    {
        return Instantiate(customerPrefab, spawnPosition, Quaternion.identity, transform);
    }
}
