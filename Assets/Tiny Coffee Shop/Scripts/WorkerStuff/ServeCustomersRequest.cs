using UnityEngine;

[System.Serializable]
public class ServeCustomersRequest : TaskRequest
{
    private Vector3 workerTargetPosition;
    public Vector3 WorkerTargetPosition => workerTargetPosition;

    private FoodDropZone dropZone;
    public FoodDropZone DropZone => dropZone;

    public ServeCustomersRequest(string guid, Vector3 workerTargetPosition, FoodDropZone dropZone)
    {
        this.guid = guid;
        this.workerTargetPosition = workerTargetPosition;
        this.dropZone = dropZone;
        this.priority = 40;
    }
}
