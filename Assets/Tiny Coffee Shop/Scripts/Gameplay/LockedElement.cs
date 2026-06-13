using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LockedElement : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Image fillImage;
    [SerializeField] private Transform anim;

    [Header(" Settings ")]
    [SerializeField] private int initialPrice;
    private int currentPrice;

    private void Awake()
    {
        currentPrice = initialPrice;
        priceText.text = currentPrice.ToString();
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
    }

    private void UpdateVisuals()
    {
        priceText.text = currentPrice.ToString();
        float fillAmount = 1f - ((float)currentPrice / initialPrice);
        fillImage.fillAmount = fillAmount;
    }

    private void Unlock()
    {
        Debug.Log("Unlocked");
    }
}
