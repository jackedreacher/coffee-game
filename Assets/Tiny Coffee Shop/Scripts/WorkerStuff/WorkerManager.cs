using System.Collections.Generic;
using UnityEngine;

public class WorkerManager : MonoBehaviour
{
    public static WorkerManager Instance;

    [Header(" Elements ")]
    [SerializeField] private List<Worker> workers = new List<Worker>();
    [SerializeField] private Trash trash;

    [SerializeReference] private List<TaskRequest> pendingRequests = new List<TaskRequest>();

    private void Awake()
    {
        Instance = this;
        TableManager.TableCleaned += OnTableCleaned;
    }

    private void OnDestroy()
    {
        TableManager.TableCleaned -= OnTableCleaned;
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

        List<Worker> idleWorkers = new List<Worker>();
        for (int i = 0; i < workers.Count; i++)
        {
            if (workers[i].CurrentTask != null)
                continue;
            idleWorkers.Add(workers[i]);
        }

        if (idleWorkers.Count <= 0)
        {
            HandleNoIdleWorkersFound();
            return;
        }

        TaskRequest highestPriorityRequest = GetHighestPriorityRequest();
        Vector3 taskTargetPosition = GetTargetPositionFromRequest(highestPriorityRequest);
        Worker closestWorker = GetClosestWorker(idleWorkers, taskTargetPosition);
        HandleRequest(highestPriorityRequest, closestWorker);
        pendingRequests.Remove(highestPriorityRequest);
    }

    private TaskRequest GetHighestPriorityRequest()
    {
        int maxPriority = int.MinValue;
        int requestIndex = -1;

        for (int i = 0; i < pendingRequests.Count; i++)
        {
            if (pendingRequests[i].Priority > maxPriority)
            {
                maxPriority = pendingRequests[i].Priority;
                requestIndex = i;
            }
        }

        return pendingRequests[requestIndex];
    }

    private Vector3 GetTargetPositionFromRequest(TaskRequest request)
    {
        if (request is FillStationPlateauRequest fillRequest)
            return fillRequest.DropZonePosition;
        else if (request is ServeCustomersRequest serveRequest)
            return serveRequest.WorkerTargetPosition;
        else if (request is CleanTableRequest cleanRequest)
            return cleanRequest.Table.WorkerTargetPosition;

        return Vector3.zero;
    }

    private Worker GetClosestWorker(List<Worker> idleWorkers, Vector3 taskTargetPosition)
    {
        float minDistance = float.MaxValue;
        int workerIndex = -1;

        for (int i = 0; i < idleWorkers.Count; i++)
        {
            float distance = Vector3.Distance(idleWorkers[i].transform.position, taskTargetPosition);

            if (distance < minDistance)
            {
                minDistance = distance;
                workerIndex = i;
            }
        }

        return idleWorkers[workerIndex];
    }

    private void HandleNoIdleWorkersFound()
    {
        for (int i = 0; i < pendingRequests.Count; i++)
        {
            TaskRequest pendingRequest = pendingRequests[i];
            bool workerFound = false;

            for (int j = 0; j < workers.Count; j++)
            {
                Worker worker = workers[j];

                if (!worker.CanCancelTask)
                    continue;

                if (pendingRequest.Priority <= worker.CurrentTask.Request.Priority)
                    continue;

                pendingRequests.Add(worker.CurrentTask.Request);
                HandleRequest(pendingRequest, worker);
                workerFound = true;
                break;
            }

            if (workerFound)
            {
                pendingRequests.Remove(pendingRequest);
                break;
            }
        }
    }

    private void HandleRequest(TaskRequest request, Worker worker)
    {
        if (request is FillStationPlateauRequest)
            HandleFillStationPlateauRequest(request, worker);
        else if (request is ServeCustomersRequest)
            HandleServeCustomersRequest(request, worker);
        else if (request is CleanTableRequest)
            HandleCleanTableRequest(request, worker);
        else if (request is IdleRequest)
            HandleIdleRequest(request, worker);
    }

    private void HandleIdleRequest(TaskRequest request, Worker worker)
    {
        IdleRequest idleRequest = request as IdleRequest;
        IdleTask task = new IdleTask(worker, idleRequest.TargetPosition, request);
        worker.AssignTask(task);
    }

    private void HandleCleanTableRequest(TaskRequest request, Worker worker)
    {
        CleanTableRequest cleanRequest = request as CleanTableRequest;
        CleanTableTask task = new CleanTableTask(worker, cleanRequest.Table, trash, request);
        worker.AssignTask(task);
    }

    private void HandleServeCustomersRequest(TaskRequest request, Worker worker)
    {
        ServeCustomersTask task = new ServeCustomersTask(worker, request);
        worker.AssignTask(task);
    }

    private void OnTableCleaned(TableSet table, HoldDishAbility holdDishAbility)
    {
        for (int i = 0; i < pendingRequests.Count; i++)
        {
            if (pendingRequests[i] is not CleanTableRequest)
                continue;

            if (pendingRequests[i].Guid != table.GUID)
                continue;

            pendingRequests.RemoveAt(i);
            break;
        }

        for (int i = 0; i < workers.Count; i++)
        {
            if (workers[i].CurrentTask == null)
                continue;

            if (workers[i].CurrentTask is not CleanTableTask)
                continue;

            CleanTableTask cleanTableTask = workers[i].CurrentTask as CleanTableTask;

            if (cleanTableTask.Table != table)
                continue;

            if (holdDishAbility.gameObject == workers[i].gameObject)
                continue;

            workers[i].CancelTask();
            break;
        }
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
