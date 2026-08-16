using UnityEngine;

// Its own type, and that is the whole point of it: Plateau.CanAccept and
// FoodDropZone both compare GetType(), so a counter that sells CookedMeat will
// refuse Meat. Raw meat cannot reach a customer by accident, without a single
// check written anywhere to stop it
public class CookedMeat : SpawnableFood
{

}
