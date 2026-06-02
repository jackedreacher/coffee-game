using System.Collections.Generic;
using UnityEngine;

public class Plateau : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Transform foodPositionsParent;

    [Header(" Settings ")]
    [SerializeField] private int maxCapacity;
    private bool isFull;
    private bool isEmpty;
    private bool isDirty;
    private float positionsYOffset;
    private SpawnableFood lastFoodPushed;

    public bool IsFull => isFull;
    public bool IsEmpty => isEmpty;
    public bool IsDirty => isDirty;

    private void Start()
    {
        isFull = false;
        isEmpty = true;
    }

    public void Push(SpawnableFood foodInstance)
    {
        lastFoodPushed = foodInstance;

        if (foodInstance.IsDirty)
            isDirty = true;

        FoodPosition foodPosition = GetFirstEmptyFoodPosition();
        foodPosition.Push(foodInstance);

        RearrangeFoodPositionsPerFood();

        isEmpty = false;

        if (GetFirstEmptyFoodPosition() == null)
        {
            if (foodPositionsParent.childCount < maxCapacity || isDirty)
                CreateNewFoodPosition();
            else
                isFull = true;
        }

        int occupiedPositions = 0;
        for (int i = 0; i < foodPositionsParent.childCount; i++)
        {
            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition fp))
                continue;

            if (!fp.IsEmpty)
                occupiedPositions++;

            if (occupiedPositions >= maxCapacity)
            {
                isFull = true;
                break;
            }
        }
    }

    public SpawnableFood Pop()
    {
        FoodPosition foodPosition = GetLastFullPosition();

        if (foodPosition == null)
            return null;

        isFull = false;

        SpawnableFood food = foodPosition.Pop();

        if (GetFirstFullPosition() == null)
            isEmpty = true;

        return food;
    }

    private FoodPosition GetFirstEmptyFoodPosition()
    {
        for (int i = 0; i < foodPositionsParent.childCount; i++)
        {
            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition foodPosition))
                continue;

            if (foodPosition.IsEmpty)
                return foodPosition;
        }

        return null;
    }

    private FoodPosition GetLastFullPosition()
    {
        for (int i = foodPositionsParent.childCount - 1; i >= 0; i--)
        {
            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition foodPosition))
                continue;

            if (!foodPosition.IsEmpty)
                return foodPosition;
        }

        return null;
    }

    public FoodPosition GetFirstFullPosition()
    {
        for (int i = 0; i < foodPositionsParent.childCount; i++)
        {
            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition foodPosition))
                continue;

            if (!foodPosition.IsEmpty)
                return foodPosition;
        }

        return null;
    }

    private void RearrangeFoodPositions(SpawnableFood foodInstance)
    {
        positionsYOffset = foodInstance.IsDirty
            ? foodInstance.DirtyYOffsetOnPlateau
            : foodInstance.CleanYOffsetOnPlateau;

        for (int i = 0; i < foodPositionsParent.childCount; i++)
            foodPositionsParent.GetChild(i).localPosition = Vector3.up * i * positionsYOffset;
    }

    // Her cup kendi offset'ini kullanır; gizli (hidden) cup'lar yığına dahil edilmez
    private void RearrangeFoodPositionsPerFood()
    {
        float yPos = 0f;

        for (int i = 0; i < foodPositionsParent.childCount; i++)
        {
            Transform child = foodPositionsParent.GetChild(i);

            if (!child.TryGetComponent(out FoodPosition foodPosition) || foodPosition.IsEmpty)
            {
                child.localPosition = Vector3.up * yPos;
                continue;
            }

            if (!foodPosition.IsFoodVisible)
            {
                // Gizli cup: görünmez, yığına yer açma
                child.localPosition = Vector3.zero;
                continue;
            }

            child.localPosition = Vector3.up * yPos;
            yPos += foodPosition.FoodYOffset;
        }
    }

    // Tepeden başlayarak bir sonraki görünür cup'ı gizle
    public void HideNextFood()
    {
        for (int i = foodPositionsParent.childCount - 1; i >= 0; i--)
        {
            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition foodPosition))
                continue;

            if (foodPosition.IsEmpty)
                continue;

            if (!foodPosition.IsFoodVisible)
                continue;

            foodPosition.HideFood();
            RearrangeFoodPositionsPerFood();
            break;
        }
    }

    // count: kaç cup dirty yapılacak — zaten dirty olanları atla, clean olanlardan say
    public SpawnableFood[] PopAll()
    {
        List<SpawnableFood> foods = new List<SpawnableFood>();

        for (int i = 0; i < foodPositionsParent.childCount; i++)
        {
            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition foodPosition))
                continue;

            if (foodPosition.IsEmpty)
                continue;

            foods.Add(foodPosition.Pop());
        }

        isFull = false;
        isEmpty = true;
        isDirty = false;

        return foods.ToArray();
    }

    public void MarkAsDirty(int count)
    {
        int marked = 0;

        for (int i = 0; i < foodPositionsParent.childCount; i++)
        {
            if (marked >= count)
                break;

            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition foodPosition))
                continue;

            if (foodPosition.IsEmpty)
                continue;

            if (foodPosition.IsFoodDirty)  // zaten dirty → atla, sayma
                continue;

            foodPosition.DisplayFood();
            foodPosition.MarkAsDirty();
            marked++;
        }

        RearrangeFoodPositionsPerFood();
    }

    private void CreateNewFoodPosition()
    {
        FoodPosition foodPositionInstance = new GameObject("FoodPosition " + foodPositionsParent.childCount)
            .AddComponent<FoodPosition>();

        foodPositionInstance.transform.SetParent(foodPositionsParent);

        int bottomChildIndex = foodPositionInstance.transform.GetSiblingIndex() - 1;
        foodPositionInstance.transform.localPosition =
            foodPositionsParent.GetChild(bottomChildIndex).localPosition + Vector3.up * positionsYOffset;
        foodPositionInstance.transform.localRotation = Quaternion.identity;

        isFull = false;
    }
}
