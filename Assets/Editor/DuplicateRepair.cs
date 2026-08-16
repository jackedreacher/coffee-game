using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Finds stations whose fields still point into the station they were copied
// from, and points them back at their own parts.
//
// Duplicating a station in the hierarchy is supposed to remap the references
// inside it, and usually does -- but not when the object was built by hand, not
// when a child was duplicated separately, and not when a reference was dragged
// in from the other copy by accident. However it happens, the result is the
// same and it is invisible: the second counter looks right, has its own
// collider, is picked correctly by the tap -- and then walks the player to the
// FIRST one's stand point and takes food off the FIRST one's plate.
//
// Nothing logs. The station is wired, just to the wrong station
public static class DuplicateRepair
{
    [MenuItem("Cooked Fast/Etkilesim: Kopyalanan Zone'lari Onar", priority = 210)]
    public static void Repair()
    {
        List<Transform> roots = StationRoots();

        if (roots.Count <= 0)
        {
            EditorUtility.DisplayDialog("Kopya Onarimi",
                "Sahnede Interactable tasiyan istasyon yok.", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine(roots.Count + " istasyon tarandi.");
        report.AppendLine();

        int fixedLinks = 0;

        foreach (Transform root in roots)
            fixedLinks += RepairStation(root, roots, report);

        if (fixedLinks <= 0)
        {
            report.AppendLine("Baska istasyona bakan alan bulunamadi.");
            report.AppendLine("Referanslar temiz -- sorun baska yerde.");
            report.AppendLine();
        }

        report.Append(Colliders(roots));

        Debug.Log("[Kopya]\n" + report);
        EditorUtility.DisplayDialog("Kopya Onarimi", report.ToString(), "Tamam");
    }

    // Gives each plate a collider of its own, so a tap lands on the plate
    // instead of on the counter it is standing on.
    //
    // This is the actual bug behind "the second plate acts like the first". The
    // tap picks the nearest SOLID collider and walks up from it to the nearest
    // Interactable -- and a plate with no collider is not something a ray can
    // hit. The ray goes through it into the counter, the counter turns out to
    // carry a HoldingShelf of its own, and that shelf is wired to the FIRST
    // zone's plate. Both plates then serve the first zone, correctly, from the
    // only station the ray ever found
    [MenuItem("Cooked Fast/Etkilesim: Tabaklara Tiklama Alani Ekle", priority = 211)]
    public static void PlateColliders()
    {
        List<Transform> roots = StationRoots();

        StringBuilder report = new StringBuilder();
        StringBuilder strays = new StringBuilder();

        int added = 0;

        foreach (Transform root in roots)
        {
            Transform plate = Interactable.ResolvePopTarget(root.gameObject);

            if (plate == null)
                continue;

            // A station whose own plate lives inside somebody else. Not fixable
            // by adding a collider -- there is nothing here to add it to
            if (!plate.IsChildOf(root))
            {
                strays.AppendLine("  " + root.name + " -> " + plate.name +
                                  " (kendi disinda)");
                continue;
            }

            if (HasSolidCollider(plate))
                continue;

            if (!TryRendererBounds(plate, out Bounds bounds))
                continue;

            BoxCollider box = Undo.AddComponent<BoxCollider>(plate.gameObject);

            Vector3 scale = plate.lossyScale;

            box.center = plate.InverseTransformPoint(bounds.center);

            // Taller than the plate actually is. A plate is nearly flat, and a
            // flat box is a hard thing to hit with a finger on a phone
            box.size = new Vector3(
                bounds.size.x / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                Mathf.Max(bounds.size.y, .14f) / Mathf.Max(.0001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(.0001f, Mathf.Abs(scale.z)));

            box.isTrigger = false;

            added++;

            report.AppendLine("  " + root.name + " -> " + plate.name);

            EditorSceneManager.MarkSceneDirty(plate.gameObject.scene);
        }

        report.Insert(0, added + " tabaga tiklama kutusu eklendi:\n");

        if (strays.Length > 0)
        {
            report.AppendLine();
            report.AppendLine("Kendi tabagi olmayan istasyonlar:");
            report.Append(strays);
            report.AppendLine("  Bunlar baskasinin tabagina tiklanmasini calar.");
        }

        report.AppendLine();
        report.AppendLine("Sahneyi kaydet: Ctrl+S");

        Debug.Log("[Tiklama]\n" + report);
        EditorUtility.DisplayDialog("Tiklama Alani", report.ToString(), "Tamam");
    }

    // Which of a station's plates the food actually sits on.
    //
    // These counters carry two: a plain "Plateau" and a "Plateau-gorsel" that is
    // the one standing on the worktop. Both are real Plateau components, so
    // every field that wants one accepts either, and picking the wrong one is
    // not an error -- the food is stacked, counted and served exactly as it
    // should be, on a plate nobody can see
    [MenuItem("Cooked Fast/Etkilesim: Yemekler Gorsel Tabaga Otursun", priority = 212)]
    public static void VisiblePlates()
    {
        List<Transform> roots = StationRoots();

        StringBuilder report = new StringBuilder();

        int moved = 0;

        foreach (Transform root in roots)
        {
            List<Plateau> plateaus = OwnPlateaus(root, roots);

            // One plate is no choice, and no plate is somebody else's problem
            if (plateaus.Count < 2)
                continue;

            Plateau chosen = Preferred(plateaus);

            if (chosen == null)
            {
                report.AppendLine("  " + root.name + ": " + plateaus.Count +
                                  " tabak var, hangisi gorunur secilemedi");
                continue;
            }

            StringBuilder lines = new StringBuilder();

            foreach (FoodDropZone zone in root.GetComponentsInChildren<FoodDropZone>(true))
                Point(zone, "plateau", chosen, lines);

            foreach (HoldingShelf shelf in root.GetComponentsInChildren<HoldingShelf>(true))
                Point(shelf, "plateau", chosen, lines);

            // The tap bounce belongs on the same plate. Bouncing the invisible
            // one is a tap that registers and shows nothing
            foreach (Interactable interactable in root.GetComponentsInChildren<Interactable>(true))
                Point(interactable, "popTarget", chosen.transform, lines);

            if (lines.Length <= 0)
                continue;

            moved++;

            report.AppendLine("  " + root.name + " -> " + chosen.name);
            report.Append(lines);

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        report.Insert(0, moved + " istasyon gorunur tabaga baglandi.\n\n");

        report.AppendLine();
        report.AppendLine("Sahneyi kaydet: Ctrl+S");

        Debug.Log("[Tabak]\n" + report);
        EditorUtility.DisplayDialog("Gorunur Tabak", report.ToString(), "Tamam");
    }

    private static void Point(Component owner, string field, Object value, StringBuilder lines)
    {
        SerializedObject so = new SerializedObject(owner);
        SerializedProperty property = so.FindProperty(field);

        if (property == null || property.objectReferenceValue == value)
            return;

        string was = property.objectReferenceValue == null
            ? "bos"
            : property.objectReferenceValue.name;

        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();

        lines.AppendLine("    " + owner.GetType().Name + "." + field + ": " + was + " -> " + value.name);
    }

    // Named first, because somebody named it on purpose. Visible second, which
    // is the same question asked of the geometry instead of the label
    private static Plateau Preferred(List<Plateau> plateaus)
    {
        foreach (Plateau plateau in plateaus)
        {
            string name = plateau.name.ToLowerInvariant();

            if (name.Contains("gorsel") || name.Contains("görsel"))
                return plateau;
        }

        Plateau best = null;
        float bestSize = 0f;

        foreach (Plateau plateau in plateaus)
        {
            if (!TryRendererBounds(plateau.transform, out Bounds bounds))
                continue;

            float size = bounds.size.sqrMagnitude;

            if (size <= bestSize)
                continue;

            best = plateau;
            bestSize = size;
        }

        return best;
    }

    // Stops at nested stations. A counter that has two drop zones under it would
    // otherwise collect all four of their plates and hand one station's plate to
    // the other
    private static List<Plateau> OwnPlateaus(Transform root, List<Transform> roots)
    {
        List<Plateau> found = new List<Plateau>();

        Collect(root, root, roots, found);

        return found;
    }

    private static void Collect(Transform at, Transform root, List<Transform> roots,
        List<Plateau> found)
    {
        if (at != root && roots.Contains(at))
            return;

        if (at.TryGetComponent(out Plateau plateau))
            found.Add(plateau);

        for (int i = 0; i < at.childCount; i++)
            Collect(at.GetChild(i), root, roots, found);
    }

    private static bool HasSolidCollider(Transform target)
    {
        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.isTrigger)
                return true;
        }

        return false;
    }

    private static bool TryRendererBounds(Transform target, out Bounds bounds)
    {
        bounds = default;

        bool any = false;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return any;
    }

    // The tap unit. Interactable is what a tap resolves to, so it is what
    // "this station" means as far as any of this is concerned
    private static List<Transform> StationRoots()
    {
        List<Transform> roots = new List<Transform>();

        foreach (Interactable interactable in Object.FindObjectsByType<Interactable>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            roots.Add(interactable.transform);

        return roots;
    }

    private static int RepairStation(Transform root, List<Transform> roots, StringBuilder report)
    {
        int fixedLinks = 0;

        StringBuilder mine = new StringBuilder();

        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            SerializedObject so = new SerializedObject(behaviour);
            SerializedProperty property = so.GetIterator();

            bool changed = false;

            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                Object value = property.objectReferenceValue;

                if (value == null || EditorUtility.IsPersistent(value))
                    continue;

                Transform target = TransformOf(value);

                // Already ours, or not a scene object at all
                if (target == null || target.IsChildOf(root))
                    continue;

                Transform owner = OwnerOf(target, roots);

                // Points somewhere outside every station -- the exit point, the
                // cash file, the customer manager. Those are meant to be shared
                if (owner == null || owner == root)
                    continue;

                string path = PathUnder(target, owner);

                Transform twin = Twin(root, owner, target);

                // No part of ours at the same place. Could be a deliberate link
                // between two stations, so it is reported and left alone
                if (twin == null)
                {
                    mine.AppendLine("    ? " + behaviour.GetType().Name + "." + property.name +
                                    " -> " + owner.name + "/" + path + "  (bizde karsiligi yok)");
                    continue;
                }

                Object replacement = value is GameObject
                    ? (Object)twin.gameObject
                    : twin.GetComponent(value.GetType());

                if (replacement == null)
                {
                    mine.AppendLine("    ? " + behaviour.GetType().Name + "." + property.name +
                                    " -> " + owner.name + "/" + path + "  (" +
                                    value.GetType().Name + " yok)");
                    continue;
                }

                property.objectReferenceValue = replacement;

                changed = true;
                fixedLinks++;

                mine.AppendLine("    " + behaviour.GetType().Name + "." + property.name +
                                ": " + owner.name + " -> kendi " + path);
            }

            if (!changed)
                continue;

            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(behaviour.gameObject.scene);
        }

        if (mine.Length <= 0)
            return fixedLinks;

        report.AppendLine("  " + root.name);
        report.Append(mine);

        return fixedLinks;
    }

    // The other realistic cause, and worth saying either way: a copy whose
    // collider never moved sits inside the original's, so the ray hits one box
    // and the player is sent to whichever station owns it
    private static string Colliders(List<Transform> roots)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("Cakisan colliderlar:");

        int clashes = 0;

        for (int i = 0; i < roots.Count; i++)
        {
            for (int j = i + 1; j < roots.Count; j++)
            {
                if (!TryBounds(roots[i], out Bounds a) || !TryBounds(roots[j], out Bounds b))
                    continue;

                if (!a.Intersects(b))
                    continue;

                clashes++;

                report.AppendLine("  " + roots[i].name + "  <->  " + roots[j].name);
            }
        }

        if (clashes <= 0)
            report.AppendLine("  yok");
        else
            report.AppendLine("  Ust uste binen kutulardan hangisine tiklandigi belli olmaz.");

        return report.ToString();
    }

    private static bool TryBounds(Transform root, out Bounds bounds)
    {
        bounds = default;

        bool any = false;

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            // The oversized walk-into boxes are meant to overlap things
            if (collider.isTrigger)
                continue;

            if (!any)
            {
                bounds = collider.bounds;
                any = true;
                continue;
            }

            bounds.Encapsulate(collider.bounds);
        }

        return any;
    }

    private static Transform TransformOf(Object value)
    {
        if (value is GameObject gameObject)
            return gameObject.transform;

        return value is Component component ? component.transform : null;
    }

    // Innermost wins, so a station nested inside another is still its own
    private static Transform OwnerOf(Transform target, List<Transform> roots)
    {
        Transform best = null;
        int bestDepth = -1;

        foreach (Transform root in roots)
        {
            if (!target.IsChildOf(root))
                continue;

            int depth = Depth(root);

            if (depth <= bestDepth)
                continue;

            best = root;
            bestDepth = depth;
        }

        return best;
    }

    private static int Depth(Transform target)
    {
        int depth = 0;

        for (Transform walk = target; walk != null; walk = walk.parent)
            depth++;

        return depth;
    }

    // The same part, on our copy.
    //
    // By child index before by name, and that ordering is the whole of it. A
    // duplicated station keeps its structure exactly, but not its names: Unity
    // renames a clashing child to "Plateau (1)", so the names stop matching at
    // the one place it matters. Worse, this station has TWO children called
    // Plateau -- a name lookup would return whichever came first, which is a
    // coin toss dressed up as an answer.
    //
    // The index walk is checked against the name anyway, ignoring any " (1)"
    // Unity added, so a structure that has really diverged falls through to the
    // name search rather than confidently picking the wrong thing
    private static Transform Twin(Transform root, Transform owner, Transform target)
    {
        List<int> indices = new List<int>();

        for (Transform walk = target; walk != null && walk != owner; walk = walk.parent)
            indices.Insert(0, walk.GetSiblingIndex());

        Transform found = root;

        foreach (int index in indices)
        {
            if (found == null || index >= found.childCount)
            {
                found = null;
                break;
            }

            found = found.GetChild(index);
        }

        if (found != null && Normalize(found.name) == Normalize(target.name))
            return found;

        string path = PathUnder(target, owner);

        if (string.IsNullOrEmpty(path))
            return root;

        Transform byName = root.Find(path);

        if (byName != null)
            return byName;

        // Last try: the same path with Unity's copy suffixes taken off both
        // sides, so "Plateau" finds "Plateau (1)"
        string wanted = NormalizePath(path);

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && NormalizePath(PathUnder(child, root)) == wanted)
                return child;
        }

        return null;
    }

    // Segment by segment. A suffix can be anywhere along the path -- it is the
    // renamed child that carries it, not the leaf
    private static string NormalizePath(string path)
    {
        string[] parts = path.Split('/');

        for (int i = 0; i < parts.Length; i++)
            parts[i] = Normalize(parts[i]);

        return string.Join("/", parts);
    }

    // "Plateau (1)" and "Plateau" are the same part of two copies
    private static string Normalize(string name)
    {
        int open = name.LastIndexOf(" (");

        if (open < 0 || !name.EndsWith(")"))
            return name;

        for (int i = open + 2; i < name.Length - 1; i++)
        {
            if (!char.IsDigit(name[i]))
                return name;
        }

        return name.Substring(0, open);
    }

    private static string PathUnder(Transform target, Transform root)
    {
        if (target == root)
            return "";

        string path = target.name;

        for (Transform walk = target.parent; walk != null && walk != root; walk = walk.parent)
            path = walk.name + "/" + path;

        return path;
    }
}
