using UnityEngine;

public class TaskRequester : MonoBehaviour
{
    public void CreateTaskRequest(TaskRequest request)
    {
        WorkerManager.RegisterRequest(request);
    }
}
