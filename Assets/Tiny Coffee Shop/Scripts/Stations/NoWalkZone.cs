using System.Collections.Generic;
using UnityEngine;

// Floor the player is not allowed to be sent to.
//
// The customer side of a counter is the one that matters. Tapping a customer
// walks the player to the SERVING spot, which is the right thing -- but a tap
// that lands slightly off the customer is read as a tap on empty ground, and
// the empty ground behind the counter is exactly where the customers are
// standing. The player walks round to it and ends up among them, on the one
// side of the counter the serving trigger is not on.
//
// A region, not an obstacle. Nothing collides with it and nothing is drawn for
// it: it only ever answers whether a point is inside
[RequireComponent(typeof(Collider))]
public class NoWalkZone : MonoBehaviour
{
    private static readonly List<NoWalkZone> zones = new List<NoWalkZone>();

    private Collider area;

    private void OnEnable()
    {
        if (area == null)
            area = GetComponent<Collider>();

        // A zone that cannot answer is worse than no zone: it would be listed,
        // asked, and quietly say no to everything
        if (area == null)
        {
            Debug.LogWarning(name + ": NoWalkZone var ama Collider yok, alan calismiyor", this);
            return;
        }

        area.isTrigger = true;
        zones.Add(this);
    }

    private void OnDisable()
    {
        zones.Remove(this);
    }

    // ClosestPoint answers the point itself when the point is inside, which is
    // the one test that respects rotation and whatever shape the collider is
    public static bool Blocks(Vector3 point)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            NoWalkZone zone = zones[i];

            if (zone == null || zone.area == null || !zone.area.enabled)
                continue;

            if ((zone.area.ClosestPoint(point) - point).sqrMagnitude < .0001f)
                return true;
        }

        return false;
    }

    // Drawn always rather than only when selected. An invisible rule about
    // where the player may walk is a rule nobody can check against the layout
    private void OnDrawGizmos()
    {
        Collider shown = area != null ? area : GetComponent<Collider>();

        if (shown == null)
            return;

        Bounds box = shown.bounds;

        Gizmos.color = new Color(1f, .35f, .3f, .12f);
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = new Color(1f, .35f, .3f, .8f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
