using UnityEngine;

// Something a shot can act on from across the kitchen.
//
// The shot does not carry an effect of its own. What a bullet MEANS is the
// target's business: an idle fryer reads it as "switch on", a pan of cinders
// reads it as "get rid of this". That keeps the weapon from growing a list of
// station types inside it -- adding a shootable station is implementing this
// interface on it, and the revolver never learns the station exists.
//
// It also keeps the two answers apart. CanTakeShot is asked while the player is
// still deciding, so a tap on a station with nothing to do falls through to the
// ordinary walk-up-and-use behaviour rather than being swallowed by a shot that
// does nothing.
public interface IShootable
{
    // Is there anything a shot could usefully do here RIGHT NOW.
    bool CanTakeShot { get; }

    // Where to aim -- the oil, the pan, the burning thing. Not the object's
    // origin, which on a floor-standing station is down at its feet.
    Vector3 ShotAimPoint { get; }

    // Do it. Returns a short line for the log saying what happened, or null if
    // it turned out there was nothing to do after all -- which the caller
    // treats as a miss rather than as a spent shot.
    string TakeShot();
}
