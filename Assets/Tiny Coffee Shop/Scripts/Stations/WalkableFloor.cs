using UnityEngine;

// Floor the player may be sent to by tapping it.
//
// The other half of NoWalkZone, and the half that scales. Naming where the
// player may NOT go means listing every mistake and always missing one --
// that is how they ended up behind the scenery twice. Naming the floor they
// MAY walk on is a list the level already has: these are the meshes the
// kitchen is built out of, already in the right shape, already moving when
// the art moves.
//
// Marked on the collider or anywhere above it, so a floor split into twenty
// tiles is one component on their parent rather than twenty on the tiles.
//
// A no-walk zone still wins over this. One big slab covering both the kitchen
// and the customer side gets marked once and then has the wrong end cut out,
// which is a smaller job than splitting the mesh
public class WalkableFloor : MonoBehaviour
{
    [Tooltip("Bilgi icin. Sahnede ne oldugunu hatirlatmaktan baska isi yok")]
    [SerializeField] private string note = "Oyuncu buraya tiklayarak gidebilir";

    public static bool Covers(Collider hit)
    {
        return hit != null && hit.GetComponentInParent<WalkableFloor>() != null;
    }

    // Drawn always, not only when selected. Where the player may walk is a rule
    // about the level, and a rule about the level that cannot be seen while
    // building the level is a rule nobody can build against
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(.25f, .85f, .45f, .10f);

        foreach (Renderer surface in GetComponentsInChildren<Renderer>(false))
            Gizmos.DrawCube(surface.bounds.center, surface.bounds.size);

        Gizmos.color = new Color(.25f, .85f, .45f, .55f);

        foreach (Renderer surface in GetComponentsInChildren<Renderer>(false))
            Gizmos.DrawWireCube(surface.bounds.center, surface.bounds.size);
    }
}
