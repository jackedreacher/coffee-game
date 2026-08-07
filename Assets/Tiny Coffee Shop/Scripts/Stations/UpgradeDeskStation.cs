using System.Collections.Generic;
using Tabsil.Sijil;
using UnityEngine;

public class UpgradeDeskStation : MonoBehaviour, IWantToBeSaved
{
    [Header(" Elements ")]
    [SerializeField] private CanvasGroup upgradePanel;
    [SerializeField] private UIPlayerUpgradeContainer upgradeContainerPrefab;
    [SerializeField] private Transform upgradeContainersParent;

    [Header(" Data ")]
    [SerializeField] private BaseCharacterStatsSO playerBaseStats;
    private int[] statLevels;
    private List<UIPlayerUpgradeContainer> upgradeContainers = new List<UIPlayerUpgradeContainer>();

    [Header(" Settings ")]
    private bool hasEntered;
    private bool isLoaded;

    private const string playerStatLevelKey = "player_stat_level_";

    // Base stats always carry exactly speed, capacity and revenue
    private const int statCount = 3;

    private void Awake()
    {
        upgradePanel.Hide();
    }

    private void Start()
    {
        // The desk starts inside a LockedElement, so it can be inactive when
        // Sijil runs its pass and Load() never fires. Catch up here
        if (!isLoaded)
            Load();
    }

    private void OnTriggerStay(Collider other)
    {
        if (upgradePanel.alpha > 0)
            return;

        if (hasEntered)
            return;

        if (!other.TryGetComponent(out PlayerController playerController))
            return;

        if (playerController.IsMoving())
            return;

        hasEntered = true;
        Display();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController _))
            return;

        hasEntered = false;
    }

    private void Display()
    {
        upgradePanel.Show();
    }

    // Public because the panel's close button calls it
    public void Hide()
    {
        upgradePanel.Hide();
    }

    private void GenerateUpgradeContainers()
    {
        for (int i = 0; i < statLevels.Length; i++)
        {
            int statLevel = statLevels[i];

            UIPlayerUpgradeContainer containerInstance =
                Instantiate(upgradeContainerPrefab, upgradeContainersParent);

            containerInstance.Initialize(this, playerBaseStats, i, statLevel);

            upgradeContainers.Add(containerInstance);
        }
    }

    // Every stat shares the same pricing, so the level is all we need.
    // Kept on the station rather than the card: it is game balance, not UI
    public int GetUpgradePrice(int statLevel)
    {
        return statLevel * 50;
    }

    public void OnContainerUpgradeButtonClicked(UIPlayerUpgradeContainer container)
    {
        // Charging and levelling up land in the next lesson
    }

    public void OnContainerVideoUpgradeButtonClicked(UIPlayerUpgradeContainer container)
    {
        // Charging and levelling up land in the next lesson
    }

    public void Save()
    {
        for (int i = 0; i < statLevels.Length; i++)
            Sijil.Save(this, playerStatLevelKey + i, statLevels[i]);
    }

    public void Load()
    {
        isLoaded = true;

        statLevels = new int[statCount];

        for (int i = 0; i < statLevels.Length; i++)
        {
            if (Sijil.TryLoad(this, playerStatLevelKey + i, out object _statLevel))
                statLevels[i] = (int)_statLevel;
        }

        GenerateUpgradeContainers();
    }
}
