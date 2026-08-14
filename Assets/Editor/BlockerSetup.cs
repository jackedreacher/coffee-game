using System.Reflection;
using System.Text;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;

// Makes a prop actually stop things. Two separate systems decide that and a
// NavMesh Obstacle only speaks to one of them: agents. A CharacterController
// walks straight through anything without a physics collider, whatever the
// navigation setup says
public static class BlockerSetup
{
    // Unity's built-in areas: 0 walkable, 1 not walkable, 2 jump
    private const int notWalkableArea = 1;

    [MenuItem("Cooked Fast/Make Selected Solid")]
    public static void MakeSolid()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length <= 0)
        {
            EditorUtility.DisplayDialog("Hata",
                "Once Hierarchy'den bir veya birden fazla obje sec", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        foreach (GameObject target in selected)
            report.AppendLine(Solidify(target));

        report.AppendLine();
        report.AppendLine(DescribeSurfaces());
        report.AppendLine("NavMeshSurface'i yeniden bake etmeyi unutma.");

        Debug.Log("Engel kurulumu:\n" + report);
        EditorUtility.DisplayDialog("Engel Kurulumu", report.ToString(), "Tamam");
    }

    // Re-measures a box that does not match what it is meant to be wrapping,
    // whether it came from the asset pack, an older version of this tool, or a
    // hand edit that went wrong. Prints both sets of numbers so a box that was
    // right all along is visible as such rather than silently rewritten
    [MenuItem("Cooked Fast/Fit Collider To Mesh")]
    public static void FitColliderToMesh()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length <= 0)
        {
            EditorUtility.DisplayDialog("Hata", "Once Hierarchy'den bir obje sec", "Tamam");
            return;
        }

        StringBuilder text = new StringBuilder();

        foreach (GameObject target in selected)
            text.AppendLine(Refit(target));

        Debug.Log("Collider oturtma:\n" + text);
        EditorUtility.DisplayDialog("Collider Oturtma", text.ToString(), "Tamam");
    }

    private static string Refit(GameObject target)
    {
        BoxCollider box = FindSolidBox(target);

        if (box == null)
            return target.name + ": duzeltilecek BoxCollider yok (once Make Selected Solid)";

        // Meshes come off the prop, the space comes off whatever holds the box,
        // so a box on a Blocker child and a box on the prop itself both land right
        Bounds local = MeshBounds(target, box.gameObject);

        if (local.size == Vector3.zero)
            return target.name + ": mesh bulunamadi";

        string before = "eski merkez " + box.center.ToString("0.000") +
                        " boyut " + box.size.ToString("0.000");

        Undo.RecordObject(box, "Fit collider");

        box.center = local.center;
        box.size = local.size;

        return target.name + " (" + box.gameObject.name + "):\n    " + before +
               "\n    yeni  merkez " + box.center.ToString("0.000") +
               " boyut " + box.size.ToString("0.000");
    }

    // The prop's own box first, then the Blocker's. Triggers are skipped: they
    // are pickup zones and are meant to be bigger than the thing they sit on
    private static BoxCollider FindSolidBox(GameObject target)
    {
        foreach (BoxCollider candidate in target.GetComponents<BoxCollider>())
        {
            if (!candidate.isTrigger)
                return candidate;
        }

        Transform blocker = target.transform.Find(blockerName);

        if (blocker == null)
            return null;

        foreach (BoxCollider candidate in blocker.GetComponents<BoxCollider>())
        {
            if (!candidate.isTrigger)
                return candidate;
        }

        return null;
    }

    // Finds objects whose script waits on OnTrigger callbacks while nothing
    // around them is a trigger any more. A station in that state looks fine and
    // silently never fires -- which is what an earlier version of Make Selected
    // Solid did to every trigger it was pointed at
    [MenuItem("Cooked Fast/Find Broken Triggers")]
    public static void FindBrokenTriggers()
    {
        StringBuilder text = new StringBuilder();
        int broken = 0;

        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || !WantsTriggers(behaviour.GetType()))
                continue;

            if (HasTrigger(behaviour.gameObject))
                continue;

            broken++;
            text.AppendLine("- " + PathOf(behaviour.transform) +
                            "  (" + behaviour.GetType().Name + ")");
        }

        string report = broken <= 0
            ? "Bozuk trigger yok."
            : broken + " obje OnTrigger bekliyor ama trigger collider'i yok:\n\n" + text;

        Debug.Log("[Trigger Kontrolu]\n" + report);
        EditorUtility.DisplayDialog("Trigger Kontrolu", report, "Tamam");
    }

    // Walked by hand: GetMethod with NonPublic does not see private methods
    // declared on a base class, and every station in this project inherits them
    private static bool WantsTriggers(System.Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (System.Type step = type; step != null && step != typeof(MonoBehaviour); step = step.BaseType)
        {
            if (step.GetMethod("OnTriggerEnter", flags) != null ||
                step.GetMethod("OnTriggerStay", flags) != null ||
                step.GetMethod("OnTriggerExit", flags) != null)
                return true;
        }

        return false;
    }

    // A child collider fires the parent's callback, so the whole subtree counts
    private static bool HasTrigger(GameObject target)
    {
        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
        {
            if (collider.isTrigger)
                return true;
        }

        return false;
    }

    // What the bake is collecting decides whether marking one prop is enough or
    // whether the surface is sweeping up every mesh in the scene
    private static string DescribeSurfaces()
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (surfaces.Length <= 0)
            return "NavMeshSurface yok.\n";

        StringBuilder text = new StringBuilder();

        foreach (NavMeshSurface surface in surfaces)
        {
            text.AppendLine("NavMeshSurface '" + surface.name + "'");
            text.AppendLine("  toplama: " + surface.collectObjects +
                            "  geometri: " + surface.useGeometry);
            text.AppendLine("  layer maskesi: " + surface.layerMask.value +
                            "  max egim: " + surface.GetBuildSettings().agentSlope.ToString("0.0"));
        }

        return text.ToString();
    }

    // For "what is this thing in my scene". Names every component on it, calls
    // out the ones that draw a gizmo and would never appear in the game, and
    // says whether it renders at all
    [MenuItem("Cooked Fast/Report Selected Object")]
    public static void ReportSelected()
    {
        GameObject target = Selection.activeGameObject;

        if (target == null)
        {
            EditorUtility.DisplayDialog("Hata", "Once Hierarchy'den bir obje sec", "Tamam");
            return;
        }

        StringBuilder text = new StringBuilder();

        text.AppendLine(PathOf(target.transform));
        text.AppendLine("  aktif " + target.activeInHierarchy +
                        " (kendi: " + target.activeSelf + ")" +
                        "  layer " + LayerMask.LayerToName(target.layer));
        text.AppendLine("  world " + target.transform.position.ToString("0.000") +
                        "  lossyScale " + target.transform.lossyScale.ToString("0.000"));
        text.AppendLine();
        text.AppendLine("BILESENLER");

        foreach (Component component in target.GetComponents<Component>())
        {
            if (component == null)
            {
                text.AppendLine("  (eksik script)");
                continue;
            }

            text.AppendLine("  " + component.GetType().Name + DescribeComponent(component));
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        text.AppendLine();
        text.AppendLine("RENDERER sayisi (cocuklar dahil): " + renderers.Length);

        if (renderers.Length <= 0)
            text.AppendLine("  Hicbir sey cizmiyor -- goruyorsan gizmo'dur, oyunda olmaz");

        foreach (Renderer renderer in renderers)
        {
            text.AppendLine("  " + renderer.name +
                            "  enabled " + renderer.enabled +
                            "  boyut " + renderer.bounds.size.ToString("0.000") +
                            "  mat " + (renderer.sharedMaterial == null
                                ? "YOK"
                                : renderer.sharedMaterial.name));
        }

        Debug.Log("[Secili Obje]\n" + text);
    }

    // Only the ones whose gizmo is worth recognising on sight
    private static string DescribeComponent(Component component)
    {
        if (component is NavMeshObstacle obstacle)
            return "  [GIZMO] sekil " + obstacle.shape + "  carving " + obstacle.carving;

        if (component is Collider collider)
            return "  [GIZMO] trigger " + collider.isTrigger + "  enabled " + collider.enabled;

        if (component.GetType().Name == "NavMeshSurface")
            return "  [GIZMO] bake hacmini acik mavi kutu olarak cizer";

        if (component is Canvas canvas)
            return "  render modu " + canvas.renderMode;

        return "";
    }

    private static string PathOf(Transform transform)
    {
        string path = transform.name;

        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private static string Solidify(GameObject target)
    {
        string line = target.name + ": ";

        line += AddBlocker(target);

        NavMeshObstacle obstacle = target.GetComponent<NavMeshObstacle>();

        if (obstacle != null && !obstacle.carving)
        {
            // Without carving an obstacle never touches the navmesh itself: it
            // only asks nearby agents to steer, and a determined one pushes on
            Undo.RecordObject(obstacle, "Carve obstacle");
            obstacle.carving = true;
            line += ", Carve acildi";
        }

        line += MarkNotWalkable(target);

        return line;
    }

    // A collider does two jobs and this is the second one. Without it the bake
    // reads the flat top of the box as perfectly good floor and lays navmesh
    // across it, which is how customers end up walking over the counter
    private static string MarkNotWalkable(GameObject target)
    {
        NavMeshModifier modifier = target.GetComponent<NavMeshModifier>();

        if (modifier == null)
            modifier = Undo.AddComponent<NavMeshModifier>(target);
        else
            Undo.RecordObject(modifier, "Not walkable");

        if (modifier.overrideArea && modifier.area == notWalkableArea && modifier.applyToChildren)
            return ", zaten Not Walkable";

        modifier.overrideArea = true;
        modifier.area = notWalkableArea;

        // Props are a parent with the mesh hung underneath as often as not, and
        // the bake reads the children
        modifier.applyToChildren = true;

        return ", Not Walkable isaretlendi";
    }

    // The solid collider goes on a child of its own rather than next to whatever
    // is already there. Two BoxColliders on one object make Edit Collider
    // useless -- the handles of both sit on top of each other and dragging one
    // looks like it does nothing. Alone on a child it is unambiguous, and it can
    // be moved, resized or deleted without going near the station's own trigger
    private const string blockerName = "Blocker";

    private static string AddBlocker(GameObject target)
    {
        foreach (Collider own in target.GetComponents<Collider>())
        {
            if (!own.isTrigger)
                return own.GetType().Name + " zaten kati, dokunulmadi";
        }

        Transform existing = target.transform.Find(blockerName);

        if (existing != null)
        {
            // Re-running must not undo a box that was resized by hand afterwards
            if (existing.GetComponent<Collider>() != null)
                return "'" + blockerName + "' zaten var, dokunulmadi";

            // Child still there, collider deleted. Refusing here would leave no
            // way back other than deleting the child by hand first
            BoxCollider refit = FitBox(target, existing.gameObject);

            return refit == null
                ? "mesh yok, collider eklenemedi"
                : "'" + blockerName + "' collider'i yeniden eklendi";
        }

        GameObject blocker = new GameObject(blockerName);

        Undo.RegisterCreatedObjectUndo(blocker, "Add blocker");
        Undo.SetTransformParent(blocker.transform, target.transform, "Add blocker");

        blocker.transform.localPosition = Vector3.zero;
        blocker.transform.localRotation = Quaternion.identity;
        blocker.transform.localScale = Vector3.one;

        // Physics layers and the bake's Include Layers both read this, so an
        // inherited layer is the only one that behaves like the prop it guards
        blocker.layer = target.layer;

        BoxCollider box = FitBox(target, blocker);

        if (box == null)
        {
            Undo.DestroyObjectImmediate(blocker);
            return "mesh yok, collider eklenemedi";
        }

        return "'" + blockerName + "' cocugu eklendi (Edit Collider burada)";
    }

    private static BoxCollider FitBox(GameObject target, GameObject host)
    {
        Bounds local = MeshBounds(target, host);

        if (local.size == Vector3.zero)
            return null;

        BoxCollider box = Undo.AddComponent<BoxCollider>(host);

        box.center = local.center;
        box.size = local.size;

        return box;
    }

    // Sized off the meshes rather than renderer bounds, so a rotated prop gets a
    // box that fits it instead of the axis aligned box around it. Measured in
    // the host's space, which is the collider's own space -- the target's only
    // matches while the host sits at the origin with an identity transform
    private static Bounds MeshBounds(GameObject target, GameObject host)
    {
        Matrix4x4 toLocal = host.transform.worldToLocalMatrix;

        bool any = false;
        Bounds local = new Bounds();

        foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;

            Bounds piece = TransformBounds(
                filter.sharedMesh.bounds, toLocal * filter.transform.localToWorldMatrix);

            if (!any)
            {
                local = piece;
                any = true;
                continue;
            }

            local.Encapsulate(piece);
        }

        return any ? local : new Bounds();
    }

    // Rotating an axis aligned box needs every corner, not just the two the
    // Bounds struct stores
    private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        Bounds result = new Bounds(matrix.MultiplyPoint3x4(min), Vector3.zero);

        for (int i = 1; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);

            result.Encapsulate(matrix.MultiplyPoint3x4(corner));
        }

        return result;
    }
}
