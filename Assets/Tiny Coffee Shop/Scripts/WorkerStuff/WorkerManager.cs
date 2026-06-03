using System.Collections.Generic;
using UnityEngine;

public class WorkerManager : MonoBehaviour
{
    public static WorkerManager Instance;

    [Header(" Elements ")]
    [SerializeField] private List<Worker> workers = new List<Worker>();

    [SerializeReference] private List<TaskRequest> pendingRequests = new List<TaskRequest>();

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
        if (pendingRequests.Count <= 0)
            return;

        if (workers.Count <= 0)
            return;

        // TODO: idle worker kontrolü burada yapılacak
        HandleRequest(pendingRequests[0], workers[0]);
        pendingRequests.RemoveAt(0);
    }

    private void HandleRequest(TaskRequest request, Worker worker)
    {
        if (request is FillStationPlateauRequest)
            HandleFillStationPlateauRequest(request, worker);
        else if (request is ServeCustomersRequest)
            HandleServeCustomersRequest(request, worker);
    }

    private void HandleServeCustomersRequest(TaskRequest request, Worker worker)
    {
        ServeCustomersTask task = new ServeCustomersTask(worker, request);
        worker.AssignTask(task);
    }

    private void HandleFillStationPlateauRequest(TaskRequest request, Worker worker)
    {
        FillStationPlateauRequest fillRequest = request as FillStationPlateauRequest;

        FoodSpawnerStation[] foodSpawnerStations = FindObjectsByType<FoodSpawnerStation>(FindObjectsSortMode.None);

        if (foodSpawnerStations.Length <= 0)
        {
            Debug.LogError("No food spawner station found");
            return;
        }

        List<FoodSpawnerStation> potentialStations = new List<FoodSpawnerStation>();

        for (int i = 0; i < foodSpawnerStations.Length; i++)
        {
            if (foodSpawnerStations[i].FoodType == fillRequest.Food.GetType())
                potentialStations.Add(foodSpawnerStations[i]);
        }

        if (potentialStations.Count <= 0)
        {
            Debug.LogError("No potential food spawner station found");
            return;
        }

        FoodSpawnerStation randomFoodSpawnerStation = potentialStations.ToArray().GetRandom();

        FillStationPlateauTask fillTask = new FillStationPlateauTask(
            worker,
            randomFoodSpawnerStation.WorkerTargetPosition,
            fillRequest.DropZonePosition,
            fillRequest
        );

        worker.AssignTask(fillTask);
    }
}
