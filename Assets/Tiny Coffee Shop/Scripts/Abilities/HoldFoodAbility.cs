using UnityEngine;

public class HoldFoodAbility : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;

    public Plateau Plateau => plateau;
    public bool IsPlateauEmpty => plateau.IsEmpty;
    public bool IsPlateauFull => plateau.IsFull;
    public bool IsPlateauDirty => plateau.IsDirty;

    [Header(" Timer ")]
    private const float canGrabFoodDelay = .1f;
    private float grabFoodTimer;
    private float dropFoodTimer;

    private void Start()
    {
        grabFoodTimer = canGrabFoodDelay;
        dropFoodTimer = canGrabFoodDelay;
    }

    public void HandleFoodSpawnerStation(FoodSpawnerStation station)
    {
        if (plateau.IsFull)
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
