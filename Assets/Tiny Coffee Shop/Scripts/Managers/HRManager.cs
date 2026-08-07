using System.Collections.Generic;
using UnityEngine;

public class HRManager : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private UIWorkerContainer uiWorkerContainerPrefab;
    [SerializeField] private Transform workerContainersParent;

    [Header(" Data ")]
    [SerializeField] private WorkerDataSO[] workerDatas;
    private List<UIWorkerContainer> workerContainers;

    public bool IsPanelVisible => cg.blocksRaycasts;

    private void Start()
    {
        GenerateWorkerContainers();
    }

    private void GenerateWorkerContainers()
    {
        workerContainers = new List<UIWorkerContainer>();

        for (int i = 0; i < workerDatas.Length; i++)
        {
            UIWorkerContainer containerInstance = Instantiate(uiWorkerContainerPrefab, workerContainersParent);

            int workerLevel = 0;
            containerInstance.Initialize(this, workerDatas[i], workerLevel);

            workerContainers.Add(containerInstance);
        }
    }

    public void Display()
    {
        cg.Show();
    }

    public void Hide()
    {
        cg.Hide();
    }
}
