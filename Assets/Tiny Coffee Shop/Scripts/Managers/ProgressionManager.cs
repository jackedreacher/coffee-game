using System.Collections.Generic;
using Tabsil.Sijil;
using UnityEngine;

public class ProgressionManager : MonoBehaviour, IWantToBeSaved
{
    [System.Serializable]
    public struct ProgressionStep
    {
        public string name;
        public List<LockedElement> lockedElements;

        public void Hide()
        {
            for (int i = 0; i < lockedElements.Count; i++)
                lockedElements[i].gameObject.SetActive(false);
        }

        public void Display()
        {
            for (int i = 0; i < lockedElements.Count; i++)
                lockedElements[i].gameObject.SetActive(true);
        }
    }

    [SerializeField] private ProgressionStep[] progressionSteps;
    private int progressionStepIndex;

    private const string progressionIndexKey = "ProgressionIndex";

    private void Start()
    {
        Load();
    }

    public void Save()
    {
        Sijil.Save(this, progressionIndexKey, progressionStepIndex);
    }

    public void Load()
    {
        if (Sijil.TryLoad(this, progressionIndexKey, out object _progressionIndex))
            progressionStepIndex = (int)_progressionIndex;

        if (progressionStepIndex >= progressionSteps.Length)
            return;

        for (int i = 0; i < progressionSteps.Length; i++)
            progressionSteps[i].Hide();

        ProgressionStep step = progressionSteps[progressionStepIndex];
        step.Display();
    }
}
