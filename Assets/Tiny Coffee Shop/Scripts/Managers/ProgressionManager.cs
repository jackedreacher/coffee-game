using System.Collections;
using System.Collections.Generic;
using Tabsil.Sijil;
using Unity.Cinemachine;
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

    [Header(" Elements ")]
    [SerializeField] private CinemachineCamera progressionCamera;
    [SerializeField] private GameObject progressionCanvas;

    [Header(" Settings ")]
    [SerializeField] private ProgressionStep[] progressionSteps;
    private int progressionStepIndex;

    private const string progressionIndexKey = "ProgressionIndex";

    private void Awake()
    {
        LockedElement.Unlocked += OnLockedElementUnlocked;
        progressionCanvas.SetActive(false);
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
        ProgressionStep step = progressionSteps[progressionStepIndex];
        step.Display();
        StartCoroutine(StartStepCoroutine(step));
    }

    private IEnumerator StartStepCoroutine(ProgressionStep step)
    {
        CameraTarget target = new CameraTarget();

        progressionCanvas.SetActive(true);

        for (int i = 0; i < step.lockedElements.Count; i++)
        {
            target.TrackingTarget = step.lockedElements[i].transform;
            progressionCamera.Target = target;
            progressionCamera.gameObject.SetActive(true);

            yield return new WaitForSeconds(1f);
        }

        progressionCamera.gameObject.SetActive(false);
        progressionCanvas.SetActive(false);
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

        for (int i = progressionStepIndex; i < progressionSteps.Length; i++)
            progressionSteps[i].Hide();

        progressionSteps[progressionStepIndex].Display();
    }
}
