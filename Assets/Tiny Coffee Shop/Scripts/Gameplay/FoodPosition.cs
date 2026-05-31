using UnityEngine;

public class FoodPosition : MonoBehaviour
{
    [Header(" Elements ")]
    private SpawnableFood food;

    [Header(" Settings ")]
    private bool isEmpty = true;

    public bool IsEmpty => isEmpty;

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
}
