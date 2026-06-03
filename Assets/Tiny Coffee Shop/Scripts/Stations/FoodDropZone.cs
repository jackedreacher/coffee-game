using UnityEngine;

public class FoodDropZone : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;
    [SerializeField] private Transform workerTargetPoint;

    public bool IsFull => plateau.IsFull;
    public int FoodCount => plateau.GetFoodCount();
    public Vector3 WorkerTargetPosition => workerTargetPoint.position;

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
