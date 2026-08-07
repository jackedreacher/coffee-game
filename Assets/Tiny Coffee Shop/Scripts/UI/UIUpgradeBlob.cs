using UnityEngine;
using UnityEngine.UI;

public class UIUpgradeBlob : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Image blobImage;

    [Header(" Settings ")]
    [SerializeField] private Color activeColor = new Color(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private Color blinkColor = new Color(1f, 0.95f, 0.4f, 1f);

    public void Activate()
    {
        // Must cancel on the image's object, not ours: Blink() tweens that one,
        // so cancelling the wrong object leaves the blob blinking forever
        LeanTween.cancel(blobImage.gameObject);

        blobImage.color = activeColor;
    }

    // Marks the blob the next upgrade will fill
    public void Blink()
    {
        LeanTween.cancel(blobImage.gameObject);
        blobImage.color = Color.white;

        LeanTween.color(blobImage.rectTransform, blinkColor, 1f)
            .setLoopPingPong(-1);
    }
}
