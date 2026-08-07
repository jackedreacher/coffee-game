using System;
using NaughtyAttributes;
using Tabsil.Sijil;
using UnityEngine;

[RequireComponent(typeof(GuidGenerator))]
public class CashFile : MonoBehaviour, IWantToBeSaved
{
    [Header(" Components ")]
    private GuidGenerator guidGenerator;

    [Header(" Elements ")]
    [SerializeField] private GameObject cashPrefab;

    [Header(" Settings ")]
    [SerializeField] private Vector2Int gridSize;
    [SerializeField] private Vector3 gridSpacing;

    private Vector3[] basePositions;
    private int index;
    private bool loaded;

    private void Awake()
    {
        guidGenerator = GetComponent<GuidGenerator>();
        StoreBasePositions();
    }

    private void Start()
    {
        if (!loaded)
            Load();
    }

    private void StoreBasePositions()
    {
        basePositions = new Vector3[gridSize.x * gridSize.y];

        Vector3 startPosition = transform.position
            - Vector3.right * gridSpacing.x * gridSize.x / 2
            - Vector3.forward * gridSpacing.z * gridSize.y / 2;

        startPosition += gridSpacing / 2;

        for (int z = 0; z < gridSize.y; z++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Vector3 targetPosition = startPosition
                    + Vector3.right * x * gridSpacing.x
                    + Vector3.forward * z * gridSpacing.z;

                int i = x + z * gridSize.x;
                basePositions[i] = targetPosition;
            }
        }
    }

    private Vector3 GetTargetGridPosition(int targetIndex)
    {
        int elevationIndex = targetIndex / basePositions.Length;
        float y = elevationIndex * gridSpacing.y;
        int basePositionIndex = targetIndex % basePositions.Length;
        return basePositions[basePositionIndex] + Vector3.up * y;
    }

    public void GenerateCash(int amount, bool save = true)
    {
        if (basePositions == null)
            StoreBasePositions();

        for (int i = 0; i < amount; i++)
        {
            Vector3 targetPosition = GetTargetGridPosition(index + i);
            GameObject cash = Instantiate(cashPrefab, targetPosition, Quaternion.identity, transform);
            if (cash.TryGetComponent(out Collider col))
                col.enabled = false;
        }

        index += amount;

        if (save)
            Save();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController _))
            return;

        AnimateCashToPlayer(other.transform);
        index = 0;
        Save();
    }

    private void AnimateCashToPlayer(Transform playerTransform)
    {
        if (transform.childCount <= 0)
            return;

        // Cached: unparenting below shrinks childCount as we go, and the
        // staggering has to stay based on the original count
        int cashCount = transform.childCount;

        float duration = 2f;
        float delayStep = duration / cashCount;
        delayStep = Mathf.Min(delayStep, 0.01f);

        for (int i = cashCount - 1; i >= 0; i--)
        {
            Transform cash = transform.GetChild(i);
            float delay = (cashCount - 1 - i) * delayStep;
            delay = Mathf.Min(delay, duration);

            // Unparent before animating: otherwise cash generated while these
            // are still flying gets mixed into the pile's children, and the
            // next collection grabs bills that are already being destroyed
            cash.parent = null;

            ArcAnimator.Animate(cash, playerTransform, 1f, delay, 3f, HandleCashMovedAlongArc);
        }
    }

    private void HandleCashMovedAlongArc(GameObject cash)
    {
        // ArcAnimator passes null when the transform vanished mid-flight
        if (cash == null)
            return;

        CurrencyManager.instance.AddCurrency(2);
        Destroy(cash);
    }

    public void Save()
    {
        if (guidGenerator == null)
            guidGenerator = GetComponent<GuidGenerator>();

        Sijil.Save(this, guidGenerator.GUID, index);
    }

    public void Load()
    {
        loaded = true;

        if (guidGenerator == null)
            guidGenerator = GetComponent<GuidGenerator>();

        if (!Sijil.TryLoad(this, guidGenerator.GUID, out object _index))
            return;

        GenerateCash((int)_index, false);
    }

    [Button]
    private void GenerateOneCash()
    {
        GenerateCash(1);
    }
}
