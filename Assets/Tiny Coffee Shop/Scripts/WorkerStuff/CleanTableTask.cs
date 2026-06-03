public class CleanTableTask : WorkerTask
{
    public CleanTableTask(Worker worker, TableSet table, Trash trash, TaskRequest request) : base(worker, request)
    {
        subTasks.Add(new MoveToSubTask(table.WorkerTargetPosition));
        subTasks.Add(new WaitForConditionSubTask(() => !table.IsDirty));
        subTasks.Add(new MoveToSubTask(trash.WorkerTargetPosition));
        subTasks.Add(new WaitForConditionSubTask(() => !worker.IsPlateauDirty && worker.IsPlateauEmpty));
    }
}
