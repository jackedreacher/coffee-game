using UnityEngine;

[System.Serializable]
public class IdleRequest : TaskRequest
{
    private Vector3 targetPosition;
    public Vector3 TargetPosition => targetPosition;

    public IdleRequest(string guid, Vector3 targetPosition)
    {
        this.guid = guid;
        this.targetPosition = targetPosition;
        this.priority = -1;
    }
}
