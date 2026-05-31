using UnityEngine;

public class FoodSpawnerStation : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private SpawnableFood spawnableFoodPrefab;
    [SerializeField] private Plateau plateau;

    [Header(" Settings ")]
    [SerializeField] private float spawnDelay;
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

    private void TrySpawnFood()
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

    public SpawnableFood Pop()
    {
        SpawnableFood food = plateau.Pop();

        if (food == null)
            return null;

        return food;
    }
}
