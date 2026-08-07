using UnityEngine;

[CreateAssetMenu(fileName = "WorkerDataSO", menuName = "Scriptable Objects/WorkerDataSO")]
public class WorkerDataSO : ScriptableObject
{
    [Header(" Settings ")]
    [SerializeField] private new string name;
    [SerializeField] private int unlockPrice;
    [SerializeField] private int initialLevel;
    [SerializeField] private Sprite profilePicture;

    [Header(" Gameplay ")]
    [SerializeField] private Worker prefab;

    #region Properties
    public string Name => name;
    public int UnlockPrice => unlockPrice;
    public int InitialLevel => initialLevel;
    public Sprite ProfilePicture => profilePicture;
    public Worker Prefab => prefab;
    #endregion
}
