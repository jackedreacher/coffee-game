using UnityEngine;

public class HoldFoodAbility : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;

    public Plateau Plateau => plateau;
    public bool IsPlateauEmpty => plateau.IsEmpty;
    public bool IsPlateauFull => plateau.IsFull;
    public bool IsPlateauDirty => plateau.IsDirty;
    public bool IsPlateauActive => plateau.gameObject.activeInHierarchy;

    [Header(" Timer ")]
    private const float canGrabFoodDelay = .1f;
    private float grabFoodTimer;
    private float dropFoodTimer;

    private void Start()
    {
        grabFoodTimer = canGrabFoodDelay;
        dropFoodTimer = canGrabFoodDelay;
    }

    public void SetPlateauCapacity(int capacity)
    {
        plateau.UpdateMaxCapacity(capacity);
    }

    public SpawnableFood PeekFood()
    {
        return plateau.Peek();
    }

    // Hands the top item over and hides the tray once it runs dry, the same
    // bookkeeping HandleFoodDropZone does when dropping onto a counter
    public SpawnableFood PopFood()
    {
        SpawnableFood food = plateau.Pop();

        if (food != null && plateau.IsEmpty)
            plateau.gameObject.SetActive(false);

        return food;
    }

    public void HandleFoodSpawnerStation(FoodSpawnerStation station)
    {
        if (plateau.IsFull)
            return;

        // The activeInHierarchy guard matters: while the plateau is off its
        // FoodPositions have not run Awake, so the emptiness check lies
        if (plateau.gameObject.activeInHierarchy &&
            !plateau.CanAccept(station.SpawnableFoodPrefab))
            return;

        if (grabFoodTimer < canGrabFoodDelay)
        {
            grabFoodTimer += Time.deltaTime;
            return;
        }

        grabFoodTimer = 0;

        SpawnableFood foodToGrab = station.Pop();

        if (foodToGrab == null)
            return;

        plateau.gameObject.SetActive(true);
        plateau.Push(foodToGrab);
    }

    public void HandleFoodDropZone(FoodDropZone dropZone)
    {
        if (!plateau.gameObject.activeSelf)
            return;

        if (plateau.IsDirty)
            return;

        if (dropZone.IsFull)
            return;

        // A coffee cashier must not take pizzas. Peek rather than Pop: refusing
        // has to leave the plateau exactly as it was
        if (!dropZone.CanAcceptFood(plateau.Peek()))
            return;

        if (dropFoodTimer < canGrabFoodDelay)
        {
            dropFoodTimer += Time.deltaTime;
            return;
        }

        dropFoodTimer = 0;

        SpawnableFood food = plateau.Pop();

        if (food == null)
            return;

        dropZone.Push(food);

        if (plateau.IsEmpty)
            plateau.gameObject.SetActive(false);
    }
}
