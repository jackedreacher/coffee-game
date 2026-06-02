using System.Collections.Generic;
using UnityEngine;

public class WorkerManager : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private List<Worker> workers = new List<Worker>();

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
