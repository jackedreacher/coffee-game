using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPlayerUpgradeContainer : MonoBehaviour
{
    [Header(" Elements ")]
    private UpgradeDeskStation station;

    [Header(" Visuals ")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image icon;
    [SerializeField] private Transform blobsParent;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button videoUpgradeButton;
    [SerializeField] private TextMeshProUGUI upgradePriceText;

    [Header(" Data ")]
    private BaseCharacterStatsSO baseStats;
    private int statIndex;
    private int level;

    // The player's cards ship with five blobs each
    private const int maxLevel = 5;

    public void Initialize(UpgradeDeskStation station, BaseCharacterStatsSO baseStats, int statIndex, int statLevel)
    {
        this.station = station;
        this.baseStats = baseStats;
        this.statIndex = statIndex;

        // Guard against a save from a build with more levels than the art has
        level = Mathf.Clamp(statLevel, 0, maxLevel);

        nameText.text = GetStatName(statIndex);
        icon.sprite = GetStatIcon(statIndex);

        InitializeBlobs();
        UpdateButtonVisuals();
        InitializeButtonCallbacks();
    }

    private string GetStatName(int statIndex)
    {
        switch (statIndex)
        {
            case 0: return "Speed";
            case 1: return "Capacity";
            case 2: return "Revenue";
            default: return "";
        }
    }

    private Sprite GetStatIcon(int statIndex)
    {
        switch (statIndex)
        {
            case 0: return baseStats.SpeedIcon;
            case 1: return baseStats.CapacityIcon;
            case 2: return baseStats.RevenueIcon;
            default: return null;
        }
    }

    private void InitializeBlobs()
    {
        // Level 0 leaves every blob dark; each level fills one more
        int blobCount = Mathf.Min(level, blobsParent.childCount);

        for (int i = 0; i < blobCount; i++)
        {
            if (blobsParent.GetChild(i).TryGetComponent(out UIUpgradeBlob blob))
                blob.Activate();
        }
    }

    private void UpdateButtonVisuals()
    {
        bool isMaxed = level >= maxLevel;
        int upgradePrice = station.GetUpgradePrice(level);

        upgradePriceText.text = isMaxed ? "max" : "<sprite=0> " + upgradePrice;

        upgradeButton.interactable =
            CurrencyManager.instance.HasEnoughCurrency(upgradePrice) && !isMaxed;

        // Nothing left to watch an ad for once the stat is maxed
        videoUpgradeButton.gameObject.SetActive(!isMaxed);
        videoUpgradeButton.interactable = !isMaxed;
    }

    private void InitializeButtonCallbacks()
    {
        upgradeButton.onClick.AddListener(() => station.OnContainerUpgradeButtonClicked(this));
        videoUpgradeButton.onClick.AddListener(() => station.OnContainerVideoUpgradeButtonClicked(this));
    }
}
