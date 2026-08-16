using UnityEngine;

// Empty like Cheese and Bread: the type itself is the payload. Plateau.CanAccept
// and FoodDropZone both compare GetType(), so meat needs its own class or a
// cheese counter would take meat too
public class Meat : SpawnableFood
{

}
