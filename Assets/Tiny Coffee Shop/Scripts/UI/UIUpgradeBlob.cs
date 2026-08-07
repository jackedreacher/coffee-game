using UnityEngine;
using UnityEngine.UI;

public class UIUpgradeBlob : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Image blobImage;

    [Header(" Settings ")]
    [SerializeField] private Color activeColor = new Color(1f, 0.82f, 0.25f, 1f);

    public void Activate()
    {
        blobImage.color = activeColor;
    }
}
