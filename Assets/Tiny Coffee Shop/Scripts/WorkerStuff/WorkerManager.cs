using System.Collections.Generic;
using UnityEngine;

public class WorkerManager : MonoBehaviour
{
    public static WorkerManager Instance;

    [Header(" Elements ")]
    [SerializeField] private List<Worker> workers = new List<Worker>();

    private List<TaskRequest> pendingRequests = new List<TaskRequest>();

    private void Awake()
    {
        Instance = this;
    }

    public static void RegisterRequest(TaskRequest request)
    {
        Instance.pendingRequests.Add(request);
    }

    private void Update()
    {
        HandleRequests();
    }

    private void HandleRequests()
    {
        // Pending request var mı?
        // Idle worker var mı?
        // Pending request'i idle worker'a ata
        // Request'i pending listesinden kaldır
    }
}
