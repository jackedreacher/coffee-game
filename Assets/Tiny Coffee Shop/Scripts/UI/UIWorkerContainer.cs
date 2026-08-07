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
        // While still locked, level is -1, so fall back to the data's initial
        // level — otherwise a locked worker would show no earned blobs at all
        // The math now lives in WorkerUtilities so the workers themselves can
        // reuse it when applying their stats
        int displayLevel = Mathf.Max(workerData.InitialLevel, level);

        Vector3Int statLevels = WorkerUtilities.GetStatsLevelsFromLevel(displayLevel);

        stats[0].Initialize(workerData.MaxSpeed, statLevels.x);
        stats[1].Initialize(workerData.MaxCapacity, statLevels.y);
        stats[2].Initialize(workerData.MaxRevenue, statLevels.z);

        // Hint at the row the next upgrade will fill. displayLevel, not level:
        // a locked worker sits at -1, and -1 % 3 is -1 in C#
        int statIndex = displayLevel % stats.Length;
        stats[statIndex].Blink();
    }

    public void Unlock()
    {
        lockedOverlay.SetActive(false);
        level = Mathf.Max(workerData.InitialLevel, level);

        // Must run after the overlay is hidden, otherwise UpdateButtonVisuals
        // would still take the "locked" branch
        UpdateButtonVisuals();
    }

    public void LevelUp()
    {
        int statIndex = level % stats.Length;
        stats[statIndex].Increment();

        level++;

        // The blinking hint moves on to whichever row is next in the rotation
        statIndex = level % stats.Length;
        stats[statIndex].Blink();

        UpdateButtonVisuals();
    }

    private void InitializeButtonCallbacks()
    {
        unlockButton.onClick.AddListener(() => hrManager.OnContainerUnlockButtonClicked(this));
        videoUnlockButton.onClick.AddListener(() => hrManager.OnContainerVideoUnlockButtonClicked(this));
        upgradeButton.onClick.AddListener(() => hrManager.OnContainerUpgradeButtonClicked(this));
        videoUpgradeButton.onClick.AddListener(() => hrManager.OnContainerVideoUpgradeButtonClicked(this));
    }

    public void UpdateButtonVisuals()
    {
        // Locked overlay still active means the worker hasn't been unlocked yet
        if (lockedOverlay.activeInHierarchy)
        {
            UpdateUnlockButton();
            return;
        }

        // Three stat rows share one level, so this is the combined ceiling.
        // Relies on the three maxes being equal (see WorkerDataSO).
        int maxLevel = workerData.MaxSpeed * 3;
        bool isMaxed = level >= maxLevel;

        int upgradePrice = HRManager.GetWorkerUpgradePriceFromLevel(level);

        upgradePriceText.text = isMaxed ? "max" : "<sprite=0> " + upgradePrice;
        upgradeButton.interactable = CurrencyManager.instance.HasEnoughCurrency(upgradePrice) && !isMaxed;

        // Nothing left to watch an ad for once the worker is maxed out
        videoUpgradeButton.gameObject.SetActive(!isMaxed);
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
