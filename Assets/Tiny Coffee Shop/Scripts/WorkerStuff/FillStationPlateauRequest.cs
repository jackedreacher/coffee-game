using UnityEngine;

[System.Serializable]
public class FillStationPlateauRequest : TaskRequest
{
    private SpawnableFood food;
    public SpawnableFood Food => food;

    private Vector3 dropZonePosition;
    public Vector3 DropZonePosition => dropZonePosition;

    public FillStationPlateauRequest(string guid, SpawnableFood food, Vector3 dropZonePosition)
    {
        this.guid = guid;
        this.food = food;
        this.dropZonePosition = dropZonePosition;
        this.priority = 70;
    }
}
