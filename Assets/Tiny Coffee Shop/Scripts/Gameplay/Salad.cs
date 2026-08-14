using UnityEngine;

// Empty like Pizza and CoffeeCup: the type itself is the payload. Plateau.
// CanAccept and FoodDropZone both compare GetType(), so a salad needs its own
// class or the counter that takes pizzas would take salads too
public class Salad : SpawnableFood
{

}
