using UnityEngine;

// Somewhere to put something down.
//
// Without it the kitchen deadlocks: the moment a burger is started the hand is
// full, and raw meat is not a burger layer, so the oven cannot be loaded. The
// only way out was the bin. This is the way out that does not throw the work
// away -- park the half built burger, go and cook, come back for it.
//
// Driven by tapping rather than by standing in a trigger, and that is the whole
// reason it works: a trigger would hand the item over and take it straight back
// on the next frame, forever. One tap is one move, and which way it goes is
// decided by whether the hand is full
public class HoldingShelf : MonoBehaviour
{
    [Header(" Elements ")]
    [Tooltip("Uzerine konan yemegin durdugu tepsi -- tabak gorseli bunun icinde")]
    [SerializeField] private Plateau plateau;

    [Tooltip("Oyuncunun gelip duracagi nokta. Bos ise rafin kendisi")]
    [SerializeField] private Transform standPoint;

    public Vector3 StandPosition => standPoint == null ? transform.position : standPoint.position;

    public bool IsEmpty => plateau == null || plateau.IsEmpty;
    public bool IsFull => plateau != null && plateau.IsFull;

    public SpawnableFood Peek()
    {
        return plateau == null ? null : plateau.Peek();
    }

    // Full hand puts down, empty hand picks up -- unless what is waiting here
    // belongs in what the hand is carrying, and then taking wins.
    //
    // That exception is the whole shelf. Park bread, go and cook, come back: the
    // hand is full of half a burger and so is the shelf, and the plain rule
    // would try to put down onto an occupied plate and refuse. Forever
    public bool Swap(HoldFoodAbility hand)
    {
        if (hand == null || plateau == null)
            return false;

        if (hand.Merges(plateau.Peek()))
            return PickUp(hand);

        if (!(hand.IsPlateauActive && !hand.IsPlateauEmpty))
            return PickUp(hand);

        // Full hand, so put down -- and if there is no room to put down, the
        // reason there is no room is that something is here worth trading for.
        //
        // Without this the tap did nothing at all: a hand holding a burger and
        // a plate holding fries refused each other, said "no room or wrong
        // type", and left the only way out through the bin
        return PutDown(hand) || Trade(hand);
    }

    // Both emptied before either is asked whether it will accept.
    //
    // Asking first is asking the wrong question: a full container answers no to
    // everything, so "will the hand take these fries" is answered by the burger
    // already in it. Emptying both and then asking is the only order in which
    // the question means what it is meant to mean
    private bool Trade(HoldFoodAbility hand)
    {
        SpawnableFood waiting = plateau.Peek();
        SpawnableFood held = hand.PeekFood();

        if (waiting == null || held == null)
            return false;

        // Burnt food does not get parked and forgotten about. The point of
        // burning it is the walk to the bin
        if (held.IsBurnt)
            return false;

        // Quiet, because this pop is speculative -- see PopFood. The trade may
        // still be refused below and put everything back, and a sound cannot be
        // put back
        SpawnableFood given = hand.PopFood(quiet: true);

        if (given == null)
            return false;

        SpawnableFood taken = plateau.Pop();

        if (taken == null)
        {
            hand.TryPush(given, quiet: true);
            return false;
        }

        if (hand.CanTake(taken) && plateau.CanAccept(given))
        {
            hand.TryPush(taken, quiet: true);
            plateau.Push(given);

            HoldFoodAbility.Swapped(taken);

            return true;
        }

        // Refused after all. Both go back exactly where they were -- nothing
        // here may destroy an item by half completing
        plateau.Push(taken);
        hand.TryPush(given, quiet: true);

        return false;
    }

    private bool PutDown(HoldFoodAbility hand)
    {
        if (plateau.IsFull)
            return false;

        SpawnableFood food = hand.PeekFood();

        // Parking it here would make the shelf a way to keep burnt meat around
        // and forget about it, and the point of burning it is that the player
        // has to walk to the bin
        if (food != null && food.IsBurnt)
            return false;

        if (food == null)
            return false;

        // Peek and check before popping: refusing has to leave the hand exactly
        // as it was, the same way every other hand-off here does it
        if (!plateau.CanAccept(food))
            return false;

        SpawnableFood taken = hand.PopFood();

        if (taken == null)
            return false;

        plateau.Push(taken);

        return true;
    }

    private bool PickUp(HoldFoodAbility hand)
    {
        SpawnableFood food = plateau.Peek();

        if (food == null || !hand.CanTake(food))
            return false;

        SpawnableFood taken = plateau.Pop();

        if (taken == null)
            return false;

        return hand.TryPush(taken);
    }
}
