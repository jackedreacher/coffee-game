using UnityEngine;

public class FoodSpawnerStation : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private SpawnableFood spawnableFoodPrefab;
    [SerializeField] private Plateau plateau;
    [SerializeField] private Transform workerTargetPoint;

    [Header(" Settings ")]
    [SerializeField] private float spawnDelay;

    public SpawnableFood SpawnableFoodPrefab => spawnableFoodPrefab;
    public System.Type FoodType => spawnableFoodPrefab.GetType();
    public Vector3 WorkerTargetPosition => workerTargetPoint.position;
    private float spawnTimer;

    void Update()
    {
        HandleSpawnTimer();
    }

    private void HandleSpawnTimer()
    {
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnDelay)
        {
            TrySpawnFood();
            spawnTimer = 0;
        }
    }

    // Virtual so a station can keep the timer and change what a tick means.
    // The cheese wheel refills all at once instead of one piece at a time
    protected virtual void TrySpawnFood()
    {
        if (plateau.IsFull)
            return;

        SpawnFood();
    }

    private void SpawnFood()
    {
        SpawnableFood foodInstance = Instantiate(spawnableFoodPrefab, transform);
        plateau.Push(foodInstance);
    }

    public virtual SpawnableFood Pop()
    {
        SpawnableFood food = plateau.Pop();

        if (food == null)
            return null;

        return food;
    }
}
