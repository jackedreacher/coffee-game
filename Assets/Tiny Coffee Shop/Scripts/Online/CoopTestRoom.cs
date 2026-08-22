using UnityEngine;

// A flag on the test room's floor, and nothing else.
//
// The player prefab is one prefab for the whole game, and right now that prefab
// is a grey capsule. Connecting from the main menu would therefore drop two
// grey capsules into the middle of the kitchen -- visible, walkable, and
// completely unexplained to anybody who did not build them.
//
// So the capsule asks whether it is standing in the room that was built for it
// before it shows itself. When the real character replaces the capsule in phase
// two, this component and the check that reads it both go away.
public class CoopTestRoom : MonoBehaviour
{
    public static bool Here =>
        Object.FindFirstObjectByType<CoopTestRoom>(FindObjectsInactive.Include) != null;
}
