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

    [Header(" Data ")]
    private HRManager hrManager;
    private WorkerDataSO workerData;

    [Header(" Settings ")]
    // Negative means the worker hasn't been unlocked yet
    private int level;

    public void Initialize(HRManager hrManager, WorkerDataSO workerData, int workerLevel)
    {
        this.hrManager = hrManager;
        this.workerData = workerData;

        profileImage.sprite = workerData.ProfilePicture;
        nameText.text = workerData.Name;

        InitializeButtonCallbacks();
        level = workerLevel;
        UpdateButtonVisuals();
    }

    public void Unlock()
    {
        lockedOverlay.SetActive(false);
        level = Mathf.Max(workerData.InitialLevel, level);
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
