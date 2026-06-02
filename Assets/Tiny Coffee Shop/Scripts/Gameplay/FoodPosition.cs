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
        foodInstance.transform.SetParent(transform);
        foodInstance.transform.localPosition = Vector3.zero;

        food = foodInstance;
        isEmpty = false;
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
