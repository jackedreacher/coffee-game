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

    // Where the stack starts, taken from wherever the first slot was placed by
    // hand. Rearranging used to write a bare Vector3.up * offset over the slots,
    // so the first pickup snapped the food back to the plateau's own origin and
    // no amount of tuning in the editor ever survived into play mode.
    //
    // Rotation and scale travel with it: the first item sits on the authored
    // slot while every one after it sits on a slot this class builds, and a
    // built slot that ignores them lays the second item at a different angle
    // from the first
    private Vector3 baseFoodOffset;
    private Quaternion baseFoodRotation = Quaternion.identity;
    private Vector3 baseFoodScale = Vector3.one;

    public bool IsFull => isFull;
    public bool IsEmpty => isEmpty;
    public bool IsDirty => isDirty;

    // Read only. The carrier raises its own tray while a recipe is half built
    // and has to be able to put the old number back afterwards
    public int MaxCapacity => maxCapacity;

    // Everything on the tray without taking any of it. Peek only ever answered
    // with the top one, which cannot tell "holding bread" from "holding bread
    // and a patty"
    public SpawnableFood[] PeekAll()
    {
        List<SpawnableFood> foods = new List<SpawnableFood>();

        for (int i = 0; i < foodPositionsParent.childCount; i++)
        {
            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition foodPosition))
                continue;

            if (foodPosition.IsEmpty)
                continue;

            foods.Add(foodPosition.Peek());
        }

        return foods.ToArray();
    }

    private void Awake()
    {
        isFull = false;
        isEmpty = true;

        if (foodPositionsParent.childCount <= 0)
            return;

        Transform first = foodPositionsParent.GetChild(0);

        baseFoodOffset = first.localPosition;
        baseFoodRotation = first.localRotation;
        baseFoodScale = first.localScale;
    }

    // Deliberately not touching isFull here: this runs while setting up the
    // stats, so the plateau is empty anyway
    public void UpdateMaxCapacity(int capacity)
    {
        maxCapacity = capacity;
    }

    public void Push(SpawnableFood foodInstance)
    {
        lastFoodPushed = foodInstance;

        if (foodInstance.IsDirty)
            isDirty = true;

        if (GetFirstEmptyFoodPosition() == null && foodPositionsParent.childCount < maxCapacity)
            CreateNewFoodPosition();

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

    // An empty plateau takes anything; after that it is one food type only,
    // otherwise the stack mixes cups and pizzas and the spacing goes wrong
    public bool CanAccept(SpawnableFood food)
    {
        FoodPosition firstFullPosition = GetFirstFullPosition();

        if (firstFullPosition == null)
            return true;

        return firstFullPosition.Peek().GetType() == food.GetType();
    }

    // Mirrors Pop: whoever asks gets the food Pop would hand out next
    public SpawnableFood Peek()
    {
        FoodPosition foodPosition = GetLastFullPosition();

        return foodPosition == null ? null : foodPosition.Peek();
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
            foodPositionsParent.GetChild(i).localPosition =
                baseFoodOffset + StackStep(i * positionsYOffset);
    }

    // Direction from the world, distance from the tray.
    //
    // Vector3.up on its own meant the tray's own up, which points sideways the
    // moment the tray is turned to sit in a hand -- so the stack grew off to one
    // side. Converting the whole vector through world space instead fixed the
    // direction and broke the distance: food inherits the tray's scale, so the
    // gap has to scale with it or a small tray stacks its food metres apart.
    // InverseTransformDirection ignores scale, which is exactly the half wanted
    private Vector3 StackStep(float gap)
    {
        if (gap <= 0f)
            return Vector3.zero;

        return foodPositionsParent.InverseTransformDirection(Vector3.up).normalized * gap;
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
                child.localPosition = baseFoodOffset + StackStep(yPos);
                continue;
            }

            if (!foodPosition.IsFoodVisible)
            {
                // Gizli cup: görünmez, yığına yer açma
                child.localPosition = baseFoodOffset;
                continue;
            }

            child.localPosition = baseFoodOffset + StackStep(yPos);
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

    public int GetFoodCount()
    {
        int count = 0;

        for (int i = 0; i < foodPositionsParent.childCount; i++)
        {
            if (!foodPositionsParent.GetChild(i).TryGetComponent(out FoodPosition foodPosition))
                continue;

            if (!foodPosition.IsEmpty)
                count++;
        }

        return count;
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

        // Same trap as FoodPosition.Push: world-preserving would give the new
        // slot a local scale of one-over-the-plateau's, so the second item in
        // the stack comes out a different size from the first
        foodPositionInstance.transform.SetParent(foodPositionsParent, false);

        // The slot below carries the hand placed base offset already, so building
        // on top of it keeps the whole stack over the same spot on the plate
        int bottomChildIndex = foodPositionInstance.transform.GetSiblingIndex() - 1;
        Vector3 bottom = bottomChildIndex >= 0
            ? foodPositionsParent.GetChild(bottomChildIndex).localPosition
            : baseFoodOffset;

        foodPositionInstance.transform.localPosition = bottom + StackStep(positionsYOffset);
        foodPositionInstance.transform.localRotation = baseFoodRotation;
        foodPositionInstance.transform.localScale = baseFoodScale;

        isFull = false;
    }
}
