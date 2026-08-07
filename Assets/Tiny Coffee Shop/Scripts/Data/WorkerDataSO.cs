using UnityEngine;

[CreateAssetMenu(fileName = "WorkerDataSO", menuName = "Scriptable Objects/WorkerDataSO")]
public class WorkerDataSO : ScriptableObject
{
    [Header(" Settings ")]
    [SerializeField] private new string name;
    [SerializeField] private int unlockPrice;
    [SerializeField] private int initialLevel;
    [SerializeField] private Sprite profilePicture;

    [Header(" Stats ")]
    // Keep all three equal for now — the blob/level math assumes a shared max
    [SerializeField][Range(2, 8)] private int maxSpeed;
    [SerializeField][Range(2, 8)] private int maxCapacity;
    [SerializeField][Range(2, 8)] private int maxRevenue;

    [Header(" Gameplay ")]
    [SerializeField] private Worker prefab;

    #region Properties
    public string Name => name;
    public int UnlockPrice => unlockPrice;
    public int InitialLevel => initialLevel;
    public Sprite ProfilePicture => profilePicture;

    public int MaxSpeed => maxSpeed;
    public int MaxCapacity => maxCapacity;
    public int MaxRevenue => maxRevenue;

    public Worker Prefab => prefab;
    #endregion
}
