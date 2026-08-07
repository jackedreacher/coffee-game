using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPlayerUpgradeContainer : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Image iconImage;

    [Header(" Data ")]
    private UpgradeDeskStation upgradeDeskStation;
    private int statIndex;
    private int statLevel;

    private static readonly string[] statNames = { "Speed", "Capacity", "Revenue" };

    public void Initialize(UpgradeDeskStation upgradeDeskStation, BaseCharacterStatsSO baseStats, int statIndex, int statLevel)
    {
        this.upgradeDeskStation = upgradeDeskStation;
        this.statIndex = statIndex;
        this.statLevel = statLevel;

        if (titleText != null)
            titleText.text = statNames[statIndex];

        if (iconImage != null)
            iconImage.sprite = baseStats.GetStatIcon(statIndex);

        // Blobs and the upgrade button get configured in the next lesson
    }
}
