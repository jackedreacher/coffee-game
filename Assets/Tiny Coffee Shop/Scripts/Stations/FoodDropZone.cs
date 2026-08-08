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

    public FoodPosition GetFirstFullPosition()
    {
        return plateau.GetFirstFullPosition();
    }
}
