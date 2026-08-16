using UnityEngine;

// Empty like Salad and Pizza: the type itself is the payload. Plateau.CanAccept
// and FoodDropZone both compare GetType(), so bread needs its own class or the
// counter that takes salads would take bread too
public class Bread : SpawnableFood
{

}
