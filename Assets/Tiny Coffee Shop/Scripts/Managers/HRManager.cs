using System.Collections.Generic;
using UnityEngine;

public class HRManager : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private UIWorkerContainer uiWorkerContainerPrefab;
    [SerializeField] private Transform workerContainersParent;
    [SerializeField] private WorkerManager workerManager;

    [Header(" Data ")]
    [SerializeField] private WorkerDataSO[] workerDatas;
    private List<UIWorkerContainer> workerContainers;
    private int[] workerLevels;

    public bool IsPanelVisible => cg.blocksRaycasts;

    private void Start()
    {
        GenerateWorkerContainers();
    }

    private void GenerateWorkerContainers()
    {
        workerContainers = new List<UIWorkerContainer>();
        workerLevels = new int[workerDatas.Length];

        for (int i = 0; i < workerDatas.Length; i++)
        {
            // -1 marks the worker as locked; Load() overwrites this later
            workerLevels[i] = -1;

            UIWorkerContainer containerInstance = Instantiate(uiWorkerContainerPrefab, workerContainersParent);
            containerInstance.Initialize(this, workerDatas[i], workerLevels[i]);

            workerContainers.Add(containerInstance);
        }
    }

    public void OnContainerUnlockButtonClicked(UIWorkerContainer uiWorkerContainer)
    {

    }

    public void OnContainerVideoUnlockButtonClicked(UIWorkerContainer uiWorkerContainer)
    {
        uiWorkerContainer.Unlock();

        int workerIndex = uiWorkerContainer.transform.GetSiblingIndex();
        workerLevels[workerIndex] = workerDatas[workerIndex].InitialLevel;

        workerManager.SpawnWorker(workerDatas[workerIndex], workerLevels[workerIndex]);

        // Save
    }

    public void OnContainerUpgradeButtonClicked(UIWorkerContainer uiWorkerContainer)
    {

    }

    public void OnContainerVideoUpgradeButtonClicked(UIWorkerContainer uiWorkerContainer)
    {

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
