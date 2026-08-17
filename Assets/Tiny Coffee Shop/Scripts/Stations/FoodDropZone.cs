using UnityEngine;

public class FoodDropZone : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;
    [SerializeField] private Transform workerTargetPoint;

    [Header(" Data ")]
    // Set by the serving station in Awake, so the two can never disagree.
    // Serialized only to make it readable in the inspector while debugging
    [SerializeField] private SpawnableFood acceptedFood;

    // OrderCounter reads this to learn what each of its zones sells
    public SpawnableFood AcceptedFood => acceptedFood;

    public bool IsFull => plateau.IsFull;
    public int FoodCount => plateau.GetFoodCount();
    public Vector3 WorkerTargetPosition => workerTargetPoint.position;

    public void SetAcceptedFood(SpawnableFood food)
    {
        acceptedFood = food;
    }

    // Compared by concrete type: a CoffeeCup zone must refuse a Pizza and back.
    // A null food means the plateau is empty, which nobody can drop
    public bool CanAcceptFood(SpawnableFood food)
    {
        if (food == null || acceptedFood == null)
            return false;

        // Right type, still refused. Burnt food matches the type it burnt from,
        // so without this it parks on the shelf and comes back out as stock
        if (food.IsBurnt)
            return false;

        return food.GetType() == acceptedFood.GetType();
    }

    public void Push(SpawnableFood food)
    {
        plateau.Push(food);
    }

    public SpawnableFood Pop()
    {
        return plateau.Pop();
    }

    // What is sitting here, without taking it. Every hand-off in this project
    // looks before it takes, so that refusing leaves the counter exactly as it
    // was rather than half emptied
    public SpawnableFood Peek()
    {
        return plateau.Peek();
    }

    public FoodPosition GetFirstFullPosition()
    {
        return plateau.GetFirstFullPosition();
    }
}
