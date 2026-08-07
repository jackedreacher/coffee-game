using System.Collections.Generic;
using Tabsil.Sijil;
using UnityEngine;

public class HRManager : MonoBehaviour, IWantToBeSaved
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

    private const string workerLevelKey = "worker_level_";

    public bool IsPanelVisible => cg.blocksRaycasts;

    private void GenerateWorkerContainers()
    {
        workerContainers = new List<UIWorkerContainer>();

        for (int i = 0; i < workerDatas.Length; i++)
        {
            UIWorkerContainer containerInstance = Instantiate(uiWorkerContainerPrefab, workerContainersParent);
            containerInstance.Initialize(this, workerDatas[i], workerLevels[i]);

            workerContainers.Add(containerInstance);
        }
    }

    public void OnContainerUnlockButtonClicked(UIWorkerContainer uiWorkerContainer)
    {
        // Deliberately not reusing the video version: that one will grow its own
        // ad-watching logic, so keeping the two paths separate stays clearer
        uiWorkerContainer.Unlock();

        int workerIndex = uiWorkerContainer.transform.GetSiblingIndex();
        workerLevels[workerIndex] = workerDatas[workerIndex].InitialLevel;

        // Safe without a currency check: the button is only interactable
        // when the player can afford it (see UIWorkerContainer.UpdateUnlockButton)
        CurrencyManager.instance.AddCurrency(-workerDatas[workerIndex].UnlockPrice);

        workerManager.SpawnWorker(workerDatas[workerIndex], workerLevels[workerIndex]);

        Save();
    }

    public void OnContainerVideoUnlockButtonClicked(UIWorkerContainer uiWorkerContainer)
    {
        uiWorkerContainer.Unlock();

        int workerIndex = uiWorkerContainer.transform.GetSiblingIndex();
        workerLevels[workerIndex] = workerDatas[workerIndex].InitialLevel;

        workerManager.SpawnWorker(workerDatas[workerIndex], workerLevels[workerIndex]);

        Save();
    }

    public void OnContainerUpgradeButtonClicked(UIWorkerContainer uiWorkerContainer)
    {

    }

    public void OnContainerVideoUpgradeButtonClicked(UIWorkerContainer uiWorkerContainer)
    {
        int workerIndex = uiWorkerContainer.transform.GetSiblingIndex();
        workerLevels[workerIndex]++;

        uiWorkerContainer.LevelUp();
        workerManager.LevelUpWorker(uiWorkerContainer.WorkerName);

        Save();
    }

    public void Display()
    {
        cg.Show();
    }

    public void Hide()
    {
        cg.Hide();
    }

    public void Save()
    {
        // A negative level means the worker is still locked, so the level
        // alone carries both the unlocked state and the upgrade progress
        for (int i = 0; i < workerLevels.Length; i++)
            Sijil.Save(this, workerLevelKey + i, workerLevels[i]);
    }

    public void Load()
    {
        workerLevels = new int[workerDatas.Length];

        for (int i = 0; i < workerLevels.Length; i++)
        {
            if (Sijil.TryLoad(this, workerLevelKey + i, out object _workerLevel))
                workerLevels[i] = (int)_workerLevel;
            else
                workerLevels[i] = -1;
        }

        GenerateWorkerContainers();
        workerManager.Initialize(workerDatas, workerLevels);
    }
}
