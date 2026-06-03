using UnityEngine;

public class IdleTask : WorkerTask
{
    public IdleTask(Worker worker, Vector3 targetPosition, TaskRequest request) : base(worker, request)
    {
        subTasks.Add(new MoveToSubTask(targetPosition));
        subTasks.Add(new WaitForConditionSubTask(() => false));
    }
}
