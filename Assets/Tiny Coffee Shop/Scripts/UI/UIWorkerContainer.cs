using UnityEngine;

public class UIWorkerContainer : MonoBehaviour
{
    [Header(" Data ")]
    private HRManager hrManager;
    private WorkerDataSO data;
    private int workerLevel;

    public void Initialize(HRManager hrManager, WorkerDataSO data, int workerLevel)
    {
        this.hrManager = hrManager;
        this.data = data;
        this.workerLevel = workerLevel;
    }
}
