using UnityEngine;

public class FillStationPlateauTask : WorkerTask
{
    public FillStationPlateauTask(Worker worker, Vector3 foodSpawnerTargetPosition, Vector3 dropZonePosition, FillStationPlateauRequest request)
        : base(worker, request)
    {
        subTasks.Add(new MoveToSubTask(foodSpawnerTargetPosition));
        subTasks.Add(new WaitForConditionSubTask(() => worker.IsPlateauFull && !worker.IsPlateauDirty));
        subTasks.Add(new MoveToSubTask(dropZonePosition));
        subTasks.Add(new WaitForConditionSubTask(() => worker.IsPlateauEmpty));
    }
}
