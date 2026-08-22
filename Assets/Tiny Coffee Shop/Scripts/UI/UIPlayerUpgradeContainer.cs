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

    // The station indexes statLevels by this. Deliberately NOT the sibling
    // index: any extra child under the parent would silently shift every card
    public int StatIndex => statIndex;

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
            case 0: return GameLocalization.Get("speed", "Speed");
            case 1: return GameLocalization.Get("capacity", "Capacity");
            case 2: return GameLocalization.Get("revenue", "Revenue");
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

    public void LevelUp()
    {
        // Fill the blob the level is about to consume, then move on
        if (level < blobsParent.childCount &&
            blobsParent.GetChild(level).TryGetComponent(out UIUpgradeBlob blob))
            blob.Activate();

        level++;

        UpdateButtonVisuals();
    }

    // Public so the station can refresh affordability when the wallet changes
    public void UpdateButtonVisuals()
    {
        bool isMaxed = level >= maxLevel;
        int upgradePrice = station.GetUpgradePrice(level);

        upgradePriceText.text = isMaxed
            ? GameLocalization.Get("max", "MAX")
            : "<sprite=0> " + upgradePrice;

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
