using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Builds the salad food prefab and turns a selected object into a station that
// hands it out. Everything is copied from the pizza that already works rather
// than invented: same prefab shape, same station wiring, same spacing
public static class SaladSetup
{
    private const string pizzaPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Pizza.prefab";
    private const string saladPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Salad.prefab";
    private const string saladModelPath = "Assets/Tiny Coffee Shop/FBX-food/salad.fbx";
    private const string plateauPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Plateau.prefab";

    private const string workerPointName = "Worker Target Point";

    [MenuItem("Cooked Fast/Arac/Setup Salad Zone")]
    public static void Setup()
    {
        // Everything below adds components and children to a scene object, and
        // play mode throws all of that away the moment it stops. Silently doing
        // the work into a scene that is about to be discarded is worse than
        // refusing
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz.\n\n" +
                "Sahneye eklenen her sey Play durunca silinir.\n" +
                "Once Play'i durdur, sonra tekrar calistir.",
                "Tamam");
            return;
        }

        GameObject zone = Selection.activeGameObject;

        if (zone == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Once Hierarchy'den Salad-zone objesini sec", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        SpawnableFood salad = BuildSaladPrefab(report);

        if (salad == null)
        {
            Show(report);
            return;
        }

        report.AppendLine();
        report.AppendLine(WireStation(zone, salad));

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Show(report);
    }

    private static void Show(StringBuilder report)
    {
        Debug.Log("Salad kurulumu:\n" + report);
        EditorUtility.DisplayDialog("Salad Kurulumu", report.ToString(), "Tamam");
    }

    // Rebuilds the salad from an object already placed in the scene, because the
    // first build copied the pizza's model transform wholesale -- and the pizza
    // carries a 6.32 stretch on Y alone, authored for its own flat mesh. Applied
    // to anything else that is not a size, it is a distortion
    [MenuItem("Cooked Fast/Arac/Rebuild Salad From Selection")]
    public static void RebuildFromSelection()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz. Once Play'i durdur.", "Tamam");
            return;
        }

        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Once sahnedeki dogru gorunen 'salad' objesini sec", "Tamam");
            return;
        }

        MeshFilter sourceFilter = selected.GetComponentInChildren<MeshFilter>(true);

        if (sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Hata", selected.name + " icinde mesh yok", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        SpawnableFood salad = BuildSaladPrefab(report);

        if (salad == null)
        {
            Show(report);
            return;
        }

        report.AppendLine();
        report.AppendLine(Reskin(salad, sourceFilter, selected));

        Show(report);
    }

    private static string Reskin(SpawnableFood salad, MeshFilter sourceFilter, GameObject selected)
    {
        string path = AssetDatabase.GetAssetPath(salad);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
            return "Salad.prefab acilamadi";

        try
        {
            MeshFilter target = root.GetComponentInChildren<MeshFilter>(true);

            if (target == null)
                return "Salad.prefab icinde mesh dugumu yok";

            target.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer targetRenderer = target.GetComponent<MeshRenderer>();
            MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();

            if (targetRenderer != null && sourceRenderer != null &&
                sourceRenderer.sharedMaterials.Length > 0)
                targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

            // Uniform, always. Whatever went wrong with the size, it is a size --
            // a mesh authored square has no business coming out taller than wide
            float fit = UniformFit(sourceFilter.sharedMesh);

            target.transform.localRotation = selected.transform.localRotation;
            target.transform.localScale = Vector3.one * fit;

            // Underside on the plate rather than the pivot on the plate: a
            // model whose origin sits at its middle would sink halfway in
            Bounds bounds = sourceFilter.sharedMesh.bounds;

            target.transform.localPosition = -new Vector3(
                bounds.center.x * fit, bounds.min.y * fit, bounds.center.z * fit);

            float height = bounds.size.y * fit;

            SerializedObject so = new SerializedObject(root.GetComponent<SpawnableFood>());

            // What the next one in the stack sits on top of
            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyMesh").objectReferenceValue = sourceFilter.sharedMesh;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);

            return
                "Salad.prefab '" + selected.name + "' modelinden kuruldu\n" +
                "  mesh: " + sourceFilter.sharedMesh.name + "\n" +
                "  olcek: " + fit.ToString("0.0000") + " (uc eksen esit)\n" +
                "  yukseklik: " + height.ToString("0.0000") + " -> yigin araligi\n" +
                "  Ince ayar: Plateau Hand Adjuster > Yemek prefabi";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Matched to the pizza's footprint, which is the one food known to read at
    // the right size on a plateau
    private static float UniformFit(Mesh mesh)
    {
        GameObject pizza = AssetDatabase.LoadAssetAtPath<GameObject>(pizzaPrefabPath);
        MeshFilter pizzaFilter = pizza == null ? null : pizza.GetComponentInChildren<MeshFilter>(true);

        if (pizzaFilter == null || pizzaFilter.sharedMesh == null)
            return 1f;

        Vector3 pizzaScale = pizzaFilter.transform.localScale;
        Vector3 pizzaSize = pizzaFilter.sharedMesh.bounds.size;

        float wanted = Mathf.Max(pizzaSize.x * pizzaScale.x, pizzaSize.z * pizzaScale.z);
        float have = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);

        return have > .0001f ? wanted / have : 1f;
    }

    // ---- the food prefab ---------------------------------------------------

    private static SpawnableFood BuildSaladPrefab(StringBuilder report)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(saladPrefabPath);

        if (existing != null)
        {
            report.AppendLine("Salad.prefab zaten var, dokunulmadi");
            return existing.GetComponent<SpawnableFood>();
        }

        GameObject pizzaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pizzaPrefabPath);

        if (pizzaPrefab == null)
        {
            report.AppendLine("Pizza.prefab bulunamadi: " + pizzaPrefabPath);
            return null;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(saladModelPath);

        if (model == null)
        {
            report.AppendLine("salad.fbx bulunamadi: " + saladModelPath);
            return null;
        }

        MeshFilter sourceFilter = model.GetComponentInChildren<MeshFilter>(true);

        if (sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            report.AppendLine("salad.fbx icinde mesh yok");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(pizzaPrefab);

        try
        {
            // Detached from Pizza.prefab so this becomes a prefab in its own
            // right rather than a variant that inherits every later pizza edit.
            // The nested Renderer instance inside is left connected
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            instance.name = "Salad";

            Pizza pizza = instance.GetComponent<Pizza>();

            if (pizza == null)
            {
                report.AppendLine("Pizza.prefab uzerinde Pizza bileseni yok");
                return null;
            }

            SerializedObject from = new SerializedObject(pizza);

            Object filter = from.FindProperty("filter").objectReferenceValue;
            Object meshRenderer = from.FindProperty("meshRenderer").objectReferenceValue;
            float cleanOffset = from.FindProperty("cleanYOffsetOnPlateau").floatValue;
            float dirtyOffset = from.FindProperty("dirtyYOffsetOnPlateau").floatValue;

            Object.DestroyImmediate(pizza);

            Salad salad = instance.AddComponent<Salad>();

            SerializedObject to = new SerializedObject(salad);

            to.FindProperty("filter").objectReferenceValue = filter;
            to.FindProperty("meshRenderer").objectReferenceValue = meshRenderer;
            to.FindProperty("cleanYOffsetOnPlateau").floatValue = cleanOffset;
            to.FindProperty("dirtyYOffsetOnPlateau").floatValue = dirtyOffset;

            // A salad has no dirty variant. Pointing this at its own mesh keeps
            // MarkAsDirty from blanking the filter and making it invisible
            to.FindProperty("dirtyMesh").objectReferenceValue = sourceFilter.sharedMesh;
            to.ApplyModifiedPropertiesWithoutUndo();

            MeshFilter targetFilter = filter as MeshFilter;
            MeshRenderer targetRenderer = meshRenderer as MeshRenderer;

            if (targetFilter != null)
                targetFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();

            if (targetRenderer != null && sourceRenderer != null &&
                sourceRenderer.sharedMaterials.Length > 0)
                targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, saladPrefabPath);

            // Both sizes, because the scale carried over from the pizza and the
            // two models are not authored at the same size. Tuning this is a
            // judgement call, not something to guess at here
            report.AppendLine("Salad.prefab olusturuldu: " + saladPrefabPath);
            report.AppendLine("  mesh: " + sourceFilter.sharedMesh.name);
            report.AppendLine("  pizza olcusu : " + PrefabSize(pizzaPrefab).ToString("0.000"));
            report.AppendLine("  salata olcusu: " + PrefabSize(saved).ToString("0.000"));
            report.AppendLine("  Boyut tutmuyorsa Salad.prefab icindeki Renderer'in Scale'ini ayarla");

            return saved.GetComponent<SpawnableFood>();
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static Vector3 PrefabSize(GameObject prefab)
    {
        MeshFilter filter = prefab == null ? null : prefab.GetComponentInChildren<MeshFilter>(true);

        if (filter == null || filter.sharedMesh == null)
            return Vector3.zero;

        Vector3 size = filter.sharedMesh.bounds.size;
        Vector3 scale = filter.transform.lossyScale;

        return new Vector3(size.x * scale.x, size.y * scale.y, size.z * scale.z);
    }

    // ---- the station -------------------------------------------------------

    private static string WireStation(GameObject zone, SpawnableFood salad)
    {
        // The pizza station in the scene is the reference for everything that
        // has no obvious right answer: how long between spawns, where the tray
        // sits, how big the trigger is, where a worker stands
        FoodSpawnerStation reference = FindReferenceStation(zone);

        FoodSpawnerStation station = zone.GetComponent<FoodSpawnerStation>();

        if (station == null)
            station = Undo.AddComponent<FoodSpawnerStation>(zone);

        SerializedObject so = new SerializedObject(station);

        so.FindProperty("spawnableFoodPrefab").objectReferenceValue = salad;

        if (reference != null)
        {
            SerializedObject referenceSo = new SerializedObject(reference);

            so.FindProperty("spawnDelay").floatValue =
                referenceSo.FindProperty("spawnDelay").floatValue;
        }

        string report = "Istasyon: " + zone.name + "\n";

        report += "  yemek: Salad\n";
        report += reference == null
            ? "  ornek istasyon yok, varsayilanlar kullanildi\n"
            : "  ornek alinan istasyon: " + reference.name + "\n";

        Plateau plateau = EnsurePlateau(zone, reference, ref report);

        if (plateau != null)
            so.FindProperty("plateau").objectReferenceValue = plateau;

        Transform workerPoint = EnsureWorkerPoint(zone, reference, ref report);

        if (workerPoint != null)
            so.FindProperty("workerTargetPoint").objectReferenceValue = workerPoint;

        so.ApplyModifiedProperties();

        report += EnsureTrigger(zone, reference);

        return report;
    }

    private static FoodSpawnerStation FindReferenceStation(GameObject zone)
    {
        foreach (FoodSpawnerStation candidate in Object.FindObjectsByType<FoodSpawnerStation>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject != zone)
                return candidate;
        }

        return null;
    }

    // The station's own tray, the one food piles onto while waiting to be taken.
    // Nothing to do with the tray the player carries
    private static Plateau EnsurePlateau(GameObject zone, FoodSpawnerStation reference, ref string report)
    {
        Plateau existing = zone.GetComponentInChildren<Plateau>(true);

        if (existing != null)
        {
            // Left alone, but the number that decides how high the pile goes is
            // worth printing next to the station it was copied from. Silently
            // running on the prefab default of seven is how a plate ends up
            // carrying a tower
            report += "  plateau: zaten var (" + existing.name + ")" +
                      "  Max Capacity " + ReadCapacity(existing) +
                      ReferenceCapacityNote(reference) + "\n";

            return existing;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plateauPrefabPath);

        if (prefab == null)
        {
            report += "  plateau: Plateau.prefab bulunamadi\n";
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, zone.transform);

        Undo.RegisterCreatedObjectUndo(instance, "Add salad plateau");

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        Plateau referencePlateau = reference == null
            ? null
            : new SerializedObject(reference).FindProperty("plateau").objectReferenceValue as Plateau;

        if (referencePlateau != null)
        {
            // Local to its own station, so the same numbers land in the same
            // place on this one however the two are rotated in the world
            instance.transform.localPosition =
                reference.transform.InverseTransformPoint(referencePlateau.transform.position);
            instance.transform.localRotation =
                Quaternion.Inverse(reference.transform.rotation) * referencePlateau.transform.rotation;
            instance.transform.localScale = referencePlateau.transform.localScale;

            // Capacity is as much a part of "same as the pizza station" as the
            // placement is, and the prefab default of seven is not it
            int capacity = ReadCapacity(referencePlateau);

            SerializedObject so = new SerializedObject(instance.GetComponent<Plateau>());

            so.FindProperty("maxCapacity").intValue = capacity;
            so.ApplyModifiedPropertiesWithoutUndo();

            report += "  plateau: eklendi, konum ve Max Capacity (" + capacity + ") " +
                      reference.name + "'dan kopyalandi\n";
        }
        else
        {
            report += "  plateau: eklendi, konum sifir -- elle ayarla\n";
        }

        return instance.GetComponent<Plateau>();
    }

    private static int ReadCapacity(Plateau plateau)
    {
        return new SerializedObject(plateau).FindProperty("maxCapacity").intValue;
    }

    private static string ReferenceCapacityNote(FoodSpawnerStation reference)
    {
        Plateau referencePlateau = reference == null
            ? null
            : new SerializedObject(reference).FindProperty("plateau").objectReferenceValue as Plateau;

        return referencePlateau == null
            ? ""
            : "  (" + reference.name + ": " + ReadCapacity(referencePlateau) + ")";
    }

    private static Transform EnsureWorkerPoint(GameObject zone, FoodSpawnerStation reference, ref string report)
    {
        Transform existing = zone.transform.Find(workerPointName);

        if (existing != null)
        {
            report += "  worker noktasi: zaten var\n";
            return existing;
        }

        GameObject point = new GameObject(workerPointName);

        Undo.RegisterCreatedObjectUndo(point, "Add worker point");
        Undo.SetTransformParent(point.transform, zone.transform, "Add worker point");

        point.transform.localPosition = Vector3.zero;
        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;

        Transform referencePoint = reference == null ? null : reference.transform.Find(workerPointName);

        if (referencePoint != null)
        {
            point.transform.localPosition =
                reference.transform.InverseTransformPoint(referencePoint.position);

            report += "  worker noktasi: eklendi, konum kopyalandi\n";
        }
        else
        {
            report += "  worker noktasi: eklendi, konum sifir -- elle ayarla\n";
        }

        return point.transform;
    }

    // PlayerDetector reads the station off a trigger it walks into, so without
    // one the station exists and is never reachable
    private static string EnsureTrigger(GameObject zone, FoodSpawnerStation reference)
    {
        foreach (Collider candidate in zone.GetComponents<Collider>())
        {
            if (candidate.isTrigger)
                return "  trigger: zaten var (" + candidate.GetType().Name + ")\n";
        }

        BoxCollider box = Undo.AddComponent<BoxCollider>(zone);

        box.isTrigger = true;

        BoxCollider referenceBox = reference == null ? null : FindTriggerBox(reference.gameObject);

        if (referenceBox != null)
        {
            box.center = referenceBox.center;
            box.size = referenceBox.size;

            return "  trigger: eklendi, boyut " + reference.name + "'dan kopyalandi\n";
        }

        box.size = Vector3.one * 2f;

        return "  trigger: eklendi, boyut 2x2x2 -- elle ayarla\n";
    }

    private static BoxCollider FindTriggerBox(GameObject target)
    {
        foreach (BoxCollider candidate in target.GetComponents<BoxCollider>())
        {
            if (candidate.isTrigger)
                return candidate;
        }

        return null;
    }
}
