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
        private List<LockedElement> unlockedElements;

        public bool Contains(LockedElement element) => lockedElements.Contains(element);

        public void UnlockElement(LockedElement element)
        {
            if (unlockedElements == null)
                unlockedElements = new List<LockedElement>();

            unlockedElements.Add(element);
        }

        public bool IsComplete => unlockedElements != null && unlockedElements.Count >= lockedElements.Count;

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

    private void Awake()
    {
        LockedElement.Unlocked += OnLockedElementUnlocked;
    }

    private void OnDestroy()
    {
        LockedElement.Unlocked -= OnLockedElementUnlocked;
    }

    private void Start()
    {
        Load();
    }

    private void OnLockedElementUnlocked(LockedElement element)
    {
        if (!progressionSteps[progressionStepIndex].Contains(element))
        {
            Debug.LogError("Current progression step does not contain this locked element");
            return;
        }

        var step = progressionSteps[progressionStepIndex];
        step.UnlockElement(element);
        progressionSteps[progressionStepIndex] = step;

        if (!progressionSteps[progressionStepIndex].IsComplete)
            return;

        progressionStepIndex++;
        Save();

        if (progressionStepIndex >= progressionSteps.Length)
            return;

        StartNextStep();
    }

    private void StartNextStep()
    {
        progressionSteps[progressionStepIndex].Display();
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

        // Hide unstarted steps only — already-unlocked steps stay visible
        for (int i = progressionStepIndex; i < progressionSteps.Length; i++)
            progressionSteps[i].Hide();

        progressionSteps[progressionStepIndex].Display();
    }
}
