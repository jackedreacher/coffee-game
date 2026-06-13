using TMPro;
using Tabsil.Sijil;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GuidGenerator))]
public class LockedElement : MonoBehaviour, IWantToBeSaved
{
    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Image fillImage;
    [SerializeField] private Transform anim;
    [SerializeField] private GameObject unlockedElements;

    [Header(" Components ")]
    private GuidGenerator guidGenerator;

    [Header(" Settings ")]
    [SerializeField] private int initialPrice;
    private int currentPrice;
    private bool loaded;

    private const string currentPriceKey = "LockedElementCurrentPrice";

    private void Awake()
    {
        guidGenerator = GetComponent<GuidGenerator>();
        currentPrice = initialPrice;
        priceText.text = currentPrice.ToString();

        if (unlockedElements != null)
            unlockedElements.SetActive(false);
    }

    private void Start()
    {
        if (!loaded)
            Load();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PayAbility _))
            return;

        ScaleUp();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.TryGetComponent(out PayAbility payAbility))
            return;

        if (currentPrice <= 0)
            return;

        if (payAbility.IsMoving)
            return;

        float wholeAnimationDuration = 1f;
        float animationDuration = wholeAnimationDuration * ((float)currentPrice / initialPrice);

        int frameDuration = (int)(animationDuration * Application.targetFrameRate);
        frameDuration = Mathf.Max(frameDuration, 1);

        int cashPerFrame = Mathf.CeilToInt((float)currentPrice / frameDuration);

        payAbility.Pay(cashPerFrame, this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PayAbility _))
            return;

        ScaleDown();
    }

    private void ScaleUp()
    {
        LeanTween.scale(anim.gameObject, Vector3.one * 1.2f, 0.2f)
            .setEase(LeanTweenType.easeOutCubic);
    }

    private void ScaleDown()
    {
        LeanTween.scale(anim.gameObject, Vector3.one, 0.2f)
            .setEase(LeanTweenType.easeOutCubic);
    }

    public void CollectCash(int amount)
    {
        currentPrice -= amount;
        UpdateVisuals();

        if (currentPrice <= 0)
            Unlock();

        Save();
    }

    private void UpdateVisuals()
    {
        priceText.text = currentPrice.ToString();
        float fillAmount = 1f - ((float)currentPrice / initialPrice);
        fillImage.fillAmount = fillAmount;
    }

    private void Unlock()
    {
        currentPrice = 0;
        anim.gameObject.SetActive(false);

        if (unlockedElements != null)
            unlockedElements.SetActive(true);

        Save();
    }

    public void Save()
    {
        if (guidGenerator == null)
            guidGenerator = GetComponent<GuidGenerator>();

        string guid = guidGenerator.GUID;

        if (currentPrice > 0)
            Sijil.Save(this, guid + currentPriceKey, currentPrice);
        else
            Sijil.Save(this, guid, true);
    }

    public void Load()
    {
        loaded = true;

        if (guidGenerator == null)
            guidGenerator = GetComponent<GuidGenerator>();

        string guid = guidGenerator.GUID;

        if (Sijil.TryLoad(this, guid, out object _unlocked))
        {
            Unlock();
            return;
        }

        if (Sijil.TryLoad(this, guid + currentPriceKey, out object _currentPrice))
        {
            currentPrice = (int)_currentPrice;
            UpdateVisuals();
        }
    }
}
