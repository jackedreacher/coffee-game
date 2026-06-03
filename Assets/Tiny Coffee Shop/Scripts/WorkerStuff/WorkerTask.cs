using System.Collections.Generic;

public abstract class WorkerTask
{
    protected List<SubTask> subTasks = new List<SubTask>();
    protected int currentSubTaskIndex;
    protected Worker worker;
    protected TaskRequest request;

    public WorkerTask(Worker worker, TaskRequest request)
    {
        this.worker = worker;
        this.request = request;
    }

    public void Start()
    {
        currentSubTaskIndex = 0;
        subTasks[currentSubTaskIndex].Start(worker);
    }

    public void Update()
    {
        if (currentSubTaskIndex >= subTasks.Count)
            return;

        SubTask current = subTasks[currentSubTaskIndex];
        current.Update(worker);

        if (!current.IsComplete)
            return;

        currentSubTaskIndex++;

        if (currentSubTaskIndex < subTasks.Count)
            subTasks[currentSubTaskIndex].Start(worker);
        else
            Complete();
    }

    private void Complete()
    {
        if (request != null && request.Sender != null)
            request.Sender.ClearRequest(request);

        worker.CompleteTask();
    }
}
