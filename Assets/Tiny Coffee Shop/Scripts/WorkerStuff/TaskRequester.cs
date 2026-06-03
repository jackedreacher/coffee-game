using System.Collections.Generic;
using UnityEngine;

public class TaskRequester : MonoBehaviour
{
    [SerializeReference] private List<TaskRequest> requests = new List<TaskRequest>();

    public void CreateTaskRequest(TaskRequest request)
    {
        request.Sender = this;

        foreach (TaskRequest r in requests)
        {
            if (r.Guid == request.Guid && r.GetType() == request.GetType())
                return;
        }

        requests.Add(request);
        WorkerManager.RegisterRequest(request);
    }

    public void ClearRequest(TaskRequest request)
    {
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            if (requests[i].Guid == request.Guid && requests[i].GetType() == request.GetType())
            {
                requests.RemoveAt(i);
                break;
            }
        }
    }
}
