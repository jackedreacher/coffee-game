using UnityEngine;

public class FoodDropZone : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;

    public bool IsFull => plateau.IsFull;

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
