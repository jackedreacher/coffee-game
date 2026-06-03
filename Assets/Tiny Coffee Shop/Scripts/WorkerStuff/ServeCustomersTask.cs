public class ServeCustomersTask : WorkerTask
{
    public ServeCustomersTask(Worker worker, TaskRequest request) : base(worker, request)
    {
        ServeCustomersRequest serveRequest = request as ServeCustomersRequest;

        subTasks.Add(new MoveToSubTask(serveRequest.WorkerTargetPosition));
        subTasks.Add(new WaitForConditionSubTask(() => serveRequest.DropZone.FoodCount < 3));
    }
}
