using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIWorkerContainer : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Image profileImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject lockedOverlay;

    [Header(" Buttons ")]
    [SerializeField] private Button unlockButton;
    [SerializeField] private Button videoUnlockButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button videoUpgradeButton;

    [Header(" Texts ")]
    [SerializeField] private TextMeshProUGUI unlockPriceText;
    [SerializeField] private TextMeshProUGUI upgradePriceText;

    [Header(" Stat Visuals ")]
    [SerializeField] private UIWorkerStat[] stats;

    [Header(" Data ")]
    private HRManager hrManager;
    private WorkerDataSO workerData;

    [Header(" Settings ")]
    // Negative means the worker hasn't been unlocked yet
    private int level;

    public string WorkerName => workerData.Name;

    public void Initialize(HRManager hrManager, WorkerDataSO workerData, int workerLevel)
    {
        this.hrManager = hrManager;
        this.workerData = workerData;

        profileImage.sprite = workerData.ProfilePicture;
        nameText.text = workerData.Name;

        InitializeButtonCallbacks();
        level = workerLevel;
        InitializeStats();

        if (level < 0)
        {
            UpdateUnlockButton();
            return;
        }

        Unlock();
    }

    private void InitializeStats()
    {
        Vector3Int statLevels = GetStatLevelsFromLevel(level);

        stats[0].Initialize(workerData.MaxSpeed, statLevels.x);
        stats[1].Initialize(workerData.MaxCapacity, statLevels.y);
        stats[2].Initialize(workerData.MaxRevenue, statLevels.z);
    }

    // One shared level feeds three stat rows in column order:
    // lvl1 -> speed, lvl2 -> capacity, lvl3 -> revenue, lvl4 -> speed, ...
    // so each row gains a blob every 3 levels, offset by its position.
    private Vector3Int GetStatLevelsFromLevel(int level)
    {
        int speedLevel = Mathf.CeilToInt(Mathf.Max(0f, level) / 3f);
        int capacityLevel = Mathf.CeilToInt(Mathf.Max(0f, level - 1) / 3f);
        int revenueLevel = Mathf.CeilToInt(Mathf.Max(0f, level - 2) / 3f);

        return new Vector3Int(speedLevel, capacityLevel, revenueLevel);
    }

    public void Unlock()
    {
        lockedOverlay.SetActive(false);
        level = Mathf.Max(workerData.InitialLevel, level);
    }

    public void LevelUp()
    {
        int statIndex = level % stats.Length;
        stats[statIndex].Increment();

        level++;

        UpdateButtonVisuals();
    }

    private void InitializeButtonCallbacks()
    {
        unlockButton.onClick.AddListener(() => hrManager.OnContainerUnlockButtonClicked(this));
        videoUnlockButton.onClick.AddListener(() => hrManager.OnContainerVideoUnlockButtonClicked(this));
        upgradeButton.onClick.AddListener(() => hrManager.OnContainerUpgradeButtonClicked(this));
        videoUpgradeButton.onClick.AddListener(() => hrManager.OnContainerVideoUpgradeButtonClicked(this));
    }

    private void UpdateButtonVisuals()
    {
        // Locked overlay still active means the worker hasn't been unlocked yet
        if (lockedOverlay.activeInHierarchy)
        {
            UpdateUnlockButton();
            return;
        }

        int upgradePrice = 100;
        upgradePriceText.text = "<sprite=0> " + upgradePrice;
        upgradeButton.interactable = CurrencyManager.instance.HasEnoughCurrency(upgradePrice);
    }

    private void UpdateUnlockButton()
    {
        // The first worker is free
        int containerIndex = transform.GetSiblingIndex();

        if (containerIndex <= 0)
        {
            unlockPriceText.text = "FREE";
            unlockButton.interactable = true;
            return;
        }

        unlockPriceText.text = "<sprite=0> " + workerData.UnlockPrice;
        unlockButton.interactable = CurrencyManager.instance.HasEnoughCurrency(workerData.UnlockPrice);
    }
}
