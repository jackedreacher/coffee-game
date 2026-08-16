using UnityEngine;

// Empty like Bread and Salad: the type itself is the payload. Plateau.CanAccept
// and FoodDropZone both compare GetType(), so cheese needs its own class or a
// bread counter would take cheese too
public class Cheese : SpawnableFood
{

}
