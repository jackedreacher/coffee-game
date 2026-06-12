using System;
using NaughtyAttributes;
using UnityEngine;

public class CashFile : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private GameObject cashPrefab;

    [Header(" Settings ")]
    [SerializeField] private Vector2Int gridSize;
    [SerializeField] private Vector3 gridSpacing;

    private Vector3[] basePositions;
    private int index;

    private void Awake()
    {
        StoreBasePositions();
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

    public void GenerateCash(int amount)
    {
        if (basePositions == null)
            StoreBasePositions();

        for (int i = 0; i < amount; i++)
        {
            Vector3 targetPosition = GetTargetGridPosition(index + i);
            Instantiate(cashPrefab, targetPosition, Quaternion.identity, transform);
        }

        index += amount;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController _))
            return;

        AnimateCashToPlayer(other.transform);
        index = 0;
    }

    private void AnimateCashToPlayer(Transform playerTransform)
    {
        if (transform.childCount <= 0)
            return;

        float duration = 2f;
        float delayStep = duration / transform.childCount;
        delayStep = Mathf.Min(delayStep, 0.01f);

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform cash = transform.GetChild(i);
            float delay = (transform.childCount - 1 - i) * delayStep;
            delay = Mathf.Min(delay, duration);
            ArcAnimator.Animate(cash, playerTransform, 1f, delay, 3f, HandleCashMovedAlongArc);
        }
    }

    private void HandleCashMovedAlongArc(GameObject cash)
    {
        CurrencyManager.instance.AddCurrency(2);
        Destroy(cash);
    }

    [Button]
    private void GenerateOneCash()
    {
        GenerateCash(1);
    }
}
