using System.Collections.Generic;
using UnityEngine;

public class UIWorkerStat : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private UIUpgradeBlob upgradeBlobPrefab;
    [SerializeField] private Transform blobsParent;
    private List<UIUpgradeBlob> blobs = new List<UIUpgradeBlob>();

    [Header(" Settings ")]
    private int blinkingBlobIndex;

    public void Initialize(int maxBlobCount, int activeBlobCount)
    {
        for (int i = 0; i < maxBlobCount; i++)
        {
            UIUpgradeBlob blobInstance = Instantiate(upgradeBlobPrefab, blobsParent);
            blobs.Add(blobInstance);

            if (activeBlobCount > i)
                blobInstance.Activate();
        }

        // The blob right after the last active one is the "next upgrade" hint
        blinkingBlobIndex = activeBlobCount;
    }

    public void Blink()
    {
        // Nothing left to hint at once every blob in this row is filled
        if (blinkingBlobIndex > blobs.Count - 1)
            return;

        blobs[blinkingBlobIndex].Blink();
    }

    public void Increment()
    {
        blobs[blinkingBlobIndex].Activate();
        blinkingBlobIndex++;
    }
}
