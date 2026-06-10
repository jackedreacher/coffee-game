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

    private Vector3 GetTargetGridPosition(int index)
    {
        int elevationIndex = index / basePositions.Length;
        float y = elevationIndex * gridSpacing.y;
        int basePositionIndex = index % basePositions.Length;
        return basePositions[basePositionIndex] + Vector3.up * y;
    }

    public void GenerateCash(int amount)
    {
        if (basePositions == null)
            StoreBasePositions();

        for (int i = 0; i < amount; i++)
        {
            Vector3 targetPosition = GetTargetGridPosition(transform.childCount);
            Instantiate(cashPrefab, targetPosition, Quaternion.identity, transform);
        }
    }

    [Button]
    private void GenerateOneCash()
    {
        GenerateCash(1);
    }
}
