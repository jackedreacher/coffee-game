using UnityEngine;

public class MoveToSubTask : SubTask
{
    private Vector3 destination;

    public MoveToSubTask(Vector3 destination)
    {
        this.destination = destination;
    }

    public override void Start(Worker worker)
    {
        worker.GoTo(destination);
    }

    public override void Update(Worker worker)
    {
        if (worker.HasReachedDestination)
            IsComplete = true;
    }
}
