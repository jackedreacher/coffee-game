using UnityEngine;

public class FoodPosition : MonoBehaviour
{
    [Header(" Elements ")]
    private SpawnableFood food;

    [Header(" Settings ")]
    private bool isEmpty = true;

    public bool IsEmpty => isEmpty;
    public bool IsFoodDirty => food != null && food.IsDirty;
    public bool IsFoodVisible => food != null && food.IsVisible;
    public float FoodYOffset => food != null
        ? (food.IsDirty ? food.DirtyYOffsetOnPlateau : food.CleanYOffsetOnPlateau)
        : 0f;

    private void Awake()
    {
        isEmpty = true;
    }

    public void Push(SpawnableFood foodInstance)
    {
        // Explicitly NOT world-preserving. The plateau hangs off a hand bone
        // now, so a world-preserving reparent bakes whatever pose the arm
        // happened to be in into the food's local rotation, and the bone's scale
        // into its local scale. Same pizza, different angle and size on every
        // single pickup, and a stack that never lines up
        foodInstance.transform.SetParent(transform, false);
        foodInstance.transform.localPosition = Vector3.zero;
        foodInstance.transform.localRotation = Quaternion.identity;

        food = foodInstance;
        isEmpty = false;
    }

    // Look at the food without taking it, so callers can inspect its type first
    public SpawnableFood Peek()
    {
        return food;
    }

    public SpawnableFood Pop()
    {
        isEmpty = true;

        SpawnableFood foodToReturn = food;
        food = null;

        return foodToReturn;
    }

    public void DisplayFood()
    {
        food?.Display();
    }

    public void HideFood()
    {
        food?.Hide();
    }

    public void MarkAsDirty()
    {
        if (food != null)
            food.MarkAsDirty();
    }
}
