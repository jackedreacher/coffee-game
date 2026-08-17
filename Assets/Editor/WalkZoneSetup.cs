#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Keeps the player off the customers' side of the counter.
//
// A tap that misses the customer is read as a tap on the floor, and the floor
// the customers stand on is floor the player was walking round to. Widening the
// target makes the miss rarer; the no-walk zone makes the miss harmless.
//
// Everything here is built to fail SAFE. A zone in the wrong place does not
// make the game harder, it makes it unplayable -- the player cannot reach their
// own counter and nothing on screen says why. So the box is clipped off the
// serving spot before it is built, it covers the customers and nothing beyond
// them by default, and the navmesh carve is a separate command nobody runs by
// accident
public static class WalkZoneSetup
{
    private const string zoneName = "No Walk Zone";
    private const string blockName = "Nav Block";

    // How far past the outermost customer the zone reaches. Enough to cover
    // someone standing slightly off their spot, and no further: the floor past
    // the queue is often the floor the player crosses to get anywhere
    private const float margin = 1.1f;

    // Kept clear around the serving spot, whatever the boxes say
    private const float serveGap = 1.2f;

    // Tall enough that a ground point is inside whatever the floor is doing,
    // low enough not to reach the camera
    private const float zoneHeight = 4f;

    // The carve starts this far past the last customer. Everything between the
    // counter and here stays walkable, because customers walk it
    private const float carveClearance = 2.5f;
    private const float carveDepth = 18f;

    // Both measured on screen, where the player is actually aiming
    private const float tapScreenRadius = 130f;   // was 90
    private const float tapRadius = 2.2f;         // was 1.5

    [MenuItem("Cooked Fast/Etkilesim: Tezgah Arkasini Kapat", priority = 213)]
    public static void Setup()
    {
        if (!Ready(out FoodServingCustomerManager[] counters, out string report))
        {
            Finish("Tezgah Arkasi", report);
            return;
        }

        foreach (FoodServingCustomerManager counter in counters)
        {
            report += "- " + counter.name + "\n";
            BuildZone(counter, ref report);
        }

        WidenTaps(ref report);

        report += "\nKutular Sahne goruntusunde kirmizi cizilir. Genisletmek icin\n" +
                  "No Walk Zone > Box Collider > Size elle buyutulebilir.\n" +
                  "Ters giderse: Cooked Fast > Etkilesim: Tezgah Arkasini Ac";

        Finish("Tezgah Arkasi", report);
    }

    // Separate and opt-in. Carving cuts navmesh out from under the floor, and a
    // carve over the wrong floor is a player who cannot path anywhere at all --
    // a worse failure than the one it is fixing, and one that survives a restart
    [MenuItem("Cooked Fast/Etkilesim: Tezgah Arkasi NavMesh Oy", priority = 215)]
    public static void Carve()
    {
        if (!Ready(out FoodServingCustomerManager[] counters, out string report))
        {
            Finish("NavMesh Oyma", report);
            return;
        }

        foreach (FoodServingCustomerManager counter in counters)
        {
            report += "- " + counter.name + "\n";
            BuildCarve(counter, ref report);
        }

        report += "\nGeri almak icin: Cooked Fast > Etkilesim: Tezgah Arkasini Ac";

        Finish("NavMesh Oyma", report);
    }

    private static bool Ready(out FoodServingCustomerManager[] counters, out string report)
    {
        counters = null;
        report = "";

        if (EditorApplication.isPlaying)
        {
            report = "Play modundayken calismaz. Once durdur.";
            return false;
        }

        counters = Object.FindObjectsByType<FoodServingCustomerManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (counters.Length <= 0)
        {
            report = "Sahnede hic FoodServingCustomerManager yok.";
            return false;
        }

        return true;
    }

    private static void Finish(string title, string report)
    {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(title + "\n" + report);
        EditorUtility.DisplayDialog(title, report, "Tamam");
    }

    private static void BuildZone(FoodServingCustomerManager counter, ref string report)
    {
        Bounds area = counter.CustomerArea(margin);

        if (area.size.sqrMagnitude < .01f)
        {
            report += "  kuyruk noktasi yok, atlandi\n";
            return;
        }

        // Before anything is built, not warned about after. A box over the
        // serving spot refuses every tap near the counter, which is the game
        Vector3 serve = ServePoint(counter);

        if (!ClearOf(area, serve, serveGap, out Bounds clipped))
        {
            report += "  ALAN KURULMADI: servis noktasi kuyrugun tam ortasinda,\n" +
                      "  kutu onu disarida birakacak sekilde kucultulemedi\n";

            Remove(counter, zoneName);
            return;
        }

        if (clipped.size != area.size)
            report += "  kutu servis noktasindan uzaklastirildi\n";

        GameObject zone = Child(counter.transform, zoneName);

        Place(zone, clipped);

        BoxCollider box = Box(zone);
        box.isTrigger = true;
        box.size = new Vector3(clipped.size.x, zoneHeight, clipped.size.z);

        if (!zone.TryGetComponent(out NoWalkZone _))
            Undo.AddComponent<NoWalkZone>(zone);

        report += "  tiklama alani " + clipped.size.x.ToString("0.0") +
                  " x " + clipped.size.z.ToString("0.0") + "\n";
    }

    private static void BuildCarve(FoodServingCustomerManager counter, ref string report)
    {
        Vector3 direction = counter.QueueDirection;
        Vector3 near = counter.BackRow + direction * carveClearance;
        Vector3 far = near + direction * carveDepth;

        Bounds queue = counter.CustomerArea(margin);

        Bounds carve = new Bounds(near, Vector3.zero);
        carve.Encapsulate(far);

        // Widened ACROSS the queue only, never along it. Growing it along the
        // queue would walk the near edge back into the customers, which is the
        // one thing the clearance exists to prevent
        Vector3 size = carve.size;

        if (Mathf.Abs(direction.z) >= Mathf.Abs(direction.x))
            size.x = Mathf.Max(size.x, queue.size.x);
        else
            size.z = Mathf.Max(size.z, queue.size.z);

        size.y = zoneHeight;
        carve.size = size;

        // Checked, not assumed, and checked BEFORE anything is built. Everyone
        // in this game paths with a navmesh agent, and navmesh cut out from
        // under any of them strands them where they stand
        string trapped = Trapped(carve, counter);

        if (trapped != null)
        {
            report += "  OYULMADI: " + trapped + " oyulacak alanin icinde\n";

            // A carve from a run when it WAS safe is worse than none: the
            // layout has moved since and it is now cutting the wrong floor
            Remove(counter, blockName);
            return;
        }

        GameObject block = Child(counter.transform, blockName);

        Place(block, carve);

        BoxCollider box = Box(block);
        box.isTrigger = true;
        box.size = carve.size;

        if (!block.TryGetComponent(out NavMeshObstacle obstacle))
            obstacle = Undo.AddComponent<NavMeshObstacle>(block);

        Undo.RecordObject(obstacle, "Carve Nav Block");

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = carve.size;

        // Carving, not just pushing agents aside. Without it the obstacle only
        // makes agents steer around the spot -- a path THROUGH it is still
        // built, and the player still ends up walking there
        obstacle.carving = true;

        // The zone never moves, so there is nothing to keep re-cutting for
        obstacle.carveOnlyStationary = true;

        if (!block.TryGetComponent(out NoWalkZone _))
            Undo.AddComponent<NoWalkZone>(block);

        report += "  oyuldu, kuyrugun " + carveClearance.ToString("0.0") +
                  " birim arkasindan itibaren\n";
    }

    // Pulled back off the one spot the player has to be able to stand on.
    //
    // Whichever face is nearest gets pulled past it -- the smallest change that
    // puts the spot outside. Answers false when there is no box left afterwards,
    // because a zone that cannot exist is better than one that traps the player
    private static bool ClearOf(Bounds area, Vector3 point, float gap, out Bounds clipped)
    {
        clipped = area;

        Vector3 min = area.min;
        Vector3 max = area.max;

        bool inside = point.x > min.x && point.x < max.x &&
                      point.z > min.z && point.z < max.z;

        if (!inside)
            return true;

        float fromMinX = point.x - min.x;
        float fromMaxX = max.x - point.x;
        float fromMinZ = point.z - min.z;
        float fromMaxZ = max.z - point.z;

        float nearest = Mathf.Min(
            Mathf.Min(fromMinX, fromMaxX),
            Mathf.Min(fromMinZ, fromMaxZ));

        if (Mathf.Approximately(nearest, fromMinX))
            min.x = point.x + gap;
        else if (Mathf.Approximately(nearest, fromMaxX))
            max.x = point.x - gap;
        else if (Mathf.Approximately(nearest, fromMinZ))
            min.z = point.z + gap;
        else
            max.z = point.z - gap;

        if (max.x - min.x < .5f || max.z - min.z < .5f)
            return false;

        clipped = new Bounds();
        clipped.SetMinMax(min, max);

        return true;
    }

    // Read off the Interactable rather than through ServePosition, which
    // resolves its stand point in Awake and answers the counter's own position
    // while the game is not running
    private static Vector3 ServePoint(FoodServingCustomerManager counter)
    {
        return counter.TryGetComponent(out Interactable point)
            ? point.StandPosition
            : counter.transform.position;
    }

    // Names what is inside, or null when the carve is safe.
    //
    // Every point a customer has to reach, from every counter in the scene and
    // from the tap handler that sends them home -- plus the player's own
    // serving spot. One counter's carve over another counter's doorway is
    // still a stranded customer
    private static string Trapped(Bounds carve, FoodServingCustomerManager counter)
    {
        Bounds flat = carve;
        flat.size = new Vector3(carve.size.x, 1000f, carve.size.z);

        if (flat.Contains(counter.BackRow))
            return "son sira musteri yeri";

        foreach ((string what, Vector3 where) in Waypoints())
        {
            if (flat.Contains(where))
                return what;
        }

        return null;
    }

    private static List<(string, Vector3)> Waypoints()
    {
        List<(string, Vector3)> points = new List<(string, Vector3)>();

        FoodServingCustomerManager[] counters =
            Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (FoodServingCustomerManager counter in counters)
        {
            if (counter == null)
                continue;

            points.Add((counter.name + " servis noktasi", ServePoint(counter)));

            if (counter.SpawnPoint != null)
                points.Add((counter.name + " giris noktasi", counter.SpawnPoint.position));

            if (counter.ExitPoint != null)
                points.Add((counter.name + " cikis noktasi", counter.ExitPoint.position));
        }

        // Where a served customer walks off to. A different field on a
        // different component, and stranding them there hangs the queue just
        // as thoroughly as stranding them at the door
        TapToServe[] taps = Object.FindObjectsByType<TapToServe>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (TapToServe tap in taps)
        {
            if (tap == null)
                continue;

            SerializedProperty exit = new SerializedObject(tap).FindProperty("customerExitPoint");

            if (exit != null && exit.objectReferenceValue is Transform door)
                points.Add((tap.name + " musteri cikisi", door.position));

            // The player themselves. Carving the floor they are standing on
            // takes their agent off the navmesh, and it never gets back on
            points.Add((tap.name + " oyuncu", tap.transform.position));
        }

        return points;
    }

    private static void Remove(FoodServingCustomerManager counter, string name)
    {
        Transform stale = counter.transform.Find(name);

        if (stale != null)
            Undo.DestroyObjectImmediate(stale.gameObject);
    }

    private static GameObject Child(Transform parent, string name)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
            return existing.gameObject;

        GameObject made = new GameObject(name);

        Undo.RegisterCreatedObjectUndo(made, "Create " + name);
        Undo.SetTransformParent(made.transform, parent, "Parent " + name);

        return made;
    }

    private static void Place(GameObject target, Bounds area)
    {
        Undo.RecordObject(target.transform, "Place " + target.name);

        // Unrotated on purpose. The boxes are axis aligned because the bounds
        // they come from are, and a rotated transform under an axis aligned
        // size is two different ideas of the same box
        target.transform.position = area.center;
        target.transform.rotation = Quaternion.identity;
        target.transform.localScale = Vector3.one;
    }

    private static BoxCollider Box(GameObject target)
    {
        if (!target.TryGetComponent(out BoxCollider box))
            box = Undo.AddComponent<BoxCollider>(target);

        Undo.RecordObject(box, "Size " + target.name);

        box.center = Vector3.zero;

        return box;
    }

    private static void WidenTaps(ref string report)
    {
        TapToServe[] all = Object.FindObjectsByType<TapToServe>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (all.Length <= 0)
        {
            report += "- UYARI: TapToServe bulunamadi, tiklama alani buyutulemedi\n";
            return;
        }

        foreach (TapToServe tap in all)
        {
            SerializedObject so = new SerializedObject(tap);

            // Written here rather than changed in the script, because the value
            // is already saved in the scene: a new default in C# never reaches
            // a component that has been serialised once
            so.FindProperty("tapScreenRadius").floatValue = tapScreenRadius;
            so.FindProperty("tapRadius").floatValue = tapRadius;

            so.ApplyModifiedProperties();

            report += "- " + tap.name + ": tiklama alani " + tapScreenRadius + " piksel\n";
        }
    }
}
#endif
