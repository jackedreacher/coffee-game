using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// The ordinary food station, the salad's shape: a plateau the station tops up
// on a timer and the player takes from. Third time of writing this by hand, so
// it is written once here -- a new food is one menu item and one line.
//
// The two stations that are not ordinary keep their own files: bread has to be
// seated on the cutting table with its plate swapped for a board, and cheese
// has no plateau at all
public static class FoodZoneSetup
{
    private const string foodFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";
    private const string plateauPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Plateau.prefab";
    private const string workerPointName = "Worker Target Point";

    [MenuItem("Cooked Fast/Meat: 1 - Istasyonu Kur (yemek dahil)", priority = 120)]
    public static void SetupMeat()
    {
        Setup<Meat>("meat", "meat-zone");
    }

    // A food dragged out of the FBX folder arrives at whatever size looked right
    // standing on a counter, which is nothing like the size it needs on a tray.
    // Eyeballing that against a plate the food is bigger than is slow, and the
    // answer is already in the project: match the one that fits
    [MenuItem("Cooked Fast/Yemek: Boyutu Salataya Esitle", priority = 130)]
    public static void MatchSize()
    {
        StringBuilder report = new StringBuilder();

        float wanted = Footprint("salad");

        if (wanted <= .0001f)
            wanted = Footprint("Pizza");

        if (wanted <= .0001f)
        {
            EditorUtility.DisplayDialog("Hata",
                "Olcu alinacak yemek yok (salad / Pizza)", "Tamam");
            return;
        }

        report.AppendLine("Hedef genislik: " + wanted.ToString("0.0000") + " (salatadan)");
        report.AppendLine();

        int touched = 0;

        foreach (UnityEngine.Object selected in Selection.objects)
        {
            GameObject prefab = selected as GameObject;
            string path = prefab == null ? null : AssetDatabase.GetAssetPath(prefab);

            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                continue;

            if (prefab.GetComponent<SpawnableFood>() == null)
            {
                report.AppendLine(prefab.name + ": SpawnableFood yok, atlandi");
                continue;
            }

            report.AppendLine(Resize(path, wanted));
            touched++;
        }

        if (touched <= 0)
        {
            EditorUtility.DisplayDialog("Hata",
                "Project penceresinden bir yemek prefabi sec.\n\n" +
                "Ornek: Assets/Tiny Coffee Shop/Prefabs/GamePlay/meat.prefab",
                "Tamam");
            return;
        }

        Show(report, "Yemek Boyutu");
    }

    // The ratio between the axes is kept. Meat is 1.88 wide and 2.14 tall on
    // purpose; making it smaller must not also make it a different shape
    private static string Resize(string path, float wanted)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
            return System.IO.Path.GetFileName(path) + ": acilamadi";

        try
        {
            MeshFilter filter = root.GetComponentInChildren<MeshFilter>(true);

            if (filter == null || filter.sharedMesh == null)
                return root.name + ": mesh yok";

            Vector3 scale = filter.transform.localScale;
            Bounds bounds = filter.sharedMesh.bounds;

            float have = Mathf.Max(bounds.size.x * Mathf.Abs(scale.x),
                                   bounds.size.z * Mathf.Abs(scale.z));

            if (have <= .0001f)
                return root.name + ": olculemedi";

            float factor = wanted / have;

            Vector3 next = scale * factor;

            filter.transform.localScale = next;

            SpawnableFood food = root.GetComponent<SpawnableFood>();

            if (food == null)
                return root.name + ": SpawnableFood yok";

            float height = bounds.size.y * Mathf.Abs(next.y);

            SerializedObject so = new SerializedObject(food);

            // The stack gap is a height, so it has to move with the size or the
            // next one along floats where the old size used to end
            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);

            if (!saved)
                return root.name + ": KAYIT BASARISIZ";

            return root.name + ": " + scale.ToString("0.000") + " -> " + next.ToString("0.000") +
                   "  (x" + factor.ToString("0.000") + ")\n" +
                   "  yigin araligi -> " + height.ToString("0.0000");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static float Footprint(string foodName)
    {
        string path = FindPrefab(foodName);
        GameObject prefab = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        MeshFilter filter = prefab == null ? null : prefab.GetComponentInChildren<MeshFilter>(true);

        if (filter == null || filter.sharedMesh == null)
            return 0f;

        Vector3 scale = filter.transform.localScale;
        Vector3 size = filter.sharedMesh.bounds.size;

        return Mathf.Max(size.x * Mathf.Abs(scale.x), size.z * Mathf.Abs(scale.z));
    }

    // ---- the command --------------------------------------------------------

    private static void Setup<T>(string foodName, string zoneName) where T : SpawnableFood
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz.\n\n" +
                "Sahneye eklenen her sey Play durunca silinir.\n" +
                "Once Play'i durdur, sonra tekrar calistir.",
                "Tamam");
            return;
        }

        GameObject zone = ResolveZone(zoneName, out string zoneNote);

        if (zone == null)
        {
            EditorUtility.DisplayDialog("Hata",
                zoneName + " bulunamadi.\n\n" +
                "Hierarchy'den " + zoneName + " objesini sec ve komutu tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Istasyon: " + zone.name + zoneNote);
        report.AppendLine("  layer: " + LayerMask.LayerToName(zone.layer));
        report.AppendLine();

        SpawnableFood food = BuildFood<T>(foodName, report);

        if (food == null)
        {
            report.Insert(0, "SONUC: yemek prefabi hazirlanamadi, istasyona dokunulmadi\n\n");
            Show(report, foodName + " Kurulumu -- YARIM KALDI");
            return;
        }

        report.AppendLine();
        report.Append(WireStation(zone, food));

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = zone;

        report.Insert(0, Verdict(zone));

        Show(report, foodName + " Kurulumu");
    }

    // Read back off the station rather than trusting the write. A station that
    // silently kept handing out the food it was duplicated from is the failure
    // this line exists to make impossible to miss
    private static string Verdict(GameObject zone)
    {
        FoodSpawnerStation station = zone.GetComponent<FoodSpawnerStation>();

        if (station == null)
            return "SONUC: " + zone.name + " uzerinde FoodSpawnerStation yok\n\n";

        SerializedObject so = new SerializedObject(station);

        SpawnableFood given = so.FindProperty("spawnableFoodPrefab").objectReferenceValue as SpawnableFood;
        UnityEngine.Object tray = so.FindProperty("plateau").objectReferenceValue;

        if (given == null)
            return "SONUC: istasyonun Spawnable Food Prefab alani BOS\n\n";

        if (tray == null)
            return "SONUC: Plateau alani BOS, yemek birikecek yer yok\n\n";

        return "SONUC: " + zone.name + " artik " + given.GetType().Name + " veriyor\n\n";
    }

    private static void Show(StringBuilder report, string title)
    {
        Debug.Log("[" + title + "]\n" + report);
        EditorUtility.DisplayDialog(title, report.ToString(), "Tamam");
    }

    // ---- finding things ----------------------------------------------------

    // The name wins over the selection unless the selection is that same object.
    // Backwards from the obvious on purpose: a command that quietly builds onto
    // whatever happened to be highlighted is how a tray scale once went out to
    // seven rabbit prefabs
    private static GameObject ResolveZone(string zoneName, out string note)
    {
        GameObject selected = Selection.activeGameObject;
        bool inScene = selected != null && selected.scene.IsValid();

        if (inScene && Same(selected.name, zoneName))
        {
            note = "";
            return selected;
        }

        GameObject named = FindInScene(zoneName);

        if (named != null)
        {
            note = inScene
                ? "  (isimden bulundu -- secili olan '" + selected.name + "' degil)"
                : "  (isimden bulundu)";

            return named;
        }

        if (!inScene)
        {
            note = "";
            return null;
        }

        note = "  <-- adi " + zoneName + " degil, secili oldugu icin kullanildi";

        return selected;
    }

    private static GameObject FindInScene(string name)
    {
        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Same(candidate.name, name))
                return candidate.gameObject;
        }

        return null;
    }

    private static bool Same(string left, string right)
    {
        return string.Equals(Strip(left), Strip(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Strip(string text)
    {
        return text.Replace(" ", "").Replace("-", "").Replace("_", "");
    }

    // By name, not by path: the asset on disk may be meat.prefab while the path
    // a command would write is Meat.prefab. Windows treats those as one file and
    // Unity does not, which is exactly how you end up with two
    private static string FindPrefab(string foodName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { foodFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Same(System.IO.Path.GetFileNameWithoutExtension(path), foodName))
                return path;
        }

        return null;
    }

    // ---- the food prefab ---------------------------------------------------

    // Rebuilt rather than edited in place, because the prefabs arrive in two
    // shapes: a plain object with the mesh on its root, and a variant of the FBX
    // whose mesh belongs to the model prefab. Building a fresh one from the mesh,
    // the materials and the scale covers both in one path, and writing it over
    // the same path keeps the GUID so existing references still resolve
    private static SpawnableFood BuildFood<T>(string foodName, StringBuilder report)
        where T : SpawnableFood
    {
        string path = FindPrefab(foodName);

        if (path == null)
        {
            report.AppendLine(foodName + ".prefab bulunamadi: " + foodFolder);
            return null;
        }

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (existing == null)
        {
            report.AppendLine(path + " okunamadi");
            return null;
        }

        T already = existing.GetComponent<T>();

        if (already != null)
        {
            report.AppendLine(System.IO.Path.GetFileName(path) + " zaten hazir, dokunulmadi");
            report.AppendLine("  " + path);

            return already;
        }

        MeshFilter filter = existing.GetComponentInChildren<MeshFilter>(true);

        if (filter == null || filter.sharedMesh == null)
        {
            report.AppendLine(path + " icinde mesh yok");
            return null;
        }

        Mesh mesh = filter.sharedMesh;
        MeshRenderer sourceRenderer = filter.GetComponent<MeshRenderer>();

        Material[] materials = sourceRenderer == null
            ? new Material[0]
            : sourceRenderer.sharedMaterials;

        // All three axes, not one of them copied over the other two. The pizza
        // in this project carries a 6.32 stretch on Y alone, so a food whose
        // shape depends on an uneven scale is normal here, not a mistake to fix
        Vector3 scale = Abs(filter.transform.lossyScale);

        if (scale.x <= .0001f || scale.y <= .0001f || scale.z <= .0001f)
            scale = Vector3.one;

        GameObject built = new GameObject(existing.name);

        try
        {
            GameObject visual = new GameObject("Renderer");

            visual.transform.SetParent(built.transform, false);

            MeshFilter newFilter = visual.AddComponent<MeshFilter>();
            MeshRenderer newRenderer = visual.AddComponent<MeshRenderer>();

            newFilter.sharedMesh = mesh;

            if (materials.Length > 0)
                newRenderer.sharedMaterials = materials;

            // Course rule, and what every other food here follows: the model node
            // sits at zero with no rotation and size is the only thing that
            // differs. FoodPosition.Push zeroes position and rotation on every
            // pickup anyway, so anything else stored there is a lie
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = scale;

            T food = built.AddComponent<T>();

            Bounds bounds = mesh.bounds;
            float height = bounds.size.y * scale.y;

            SerializedObject so = new SerializedObject(food);

            so.FindProperty("filter").objectReferenceValue = newFilter;
            so.FindProperty("meshRenderer").objectReferenceValue = newRenderer;

            // What the next one in the stack sits on top of
            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;

            // No dirty variant. Pointing this at its own mesh keeps MarkAsDirty
            // from blanking the filter and making it invisible
            so.FindProperty("dirtyMesh").objectReferenceValue = mesh;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(built, path, out bool saved);

            if (!saved)
            {
                report.AppendLine("KAYIT BASARISIZ: " + path);
                return null;
            }

            report.AppendLine(System.IO.Path.GetFileName(path) + " yemege cevrildi");
            report.AppendLine("  " + path);
            report.AppendLine("  mesh: " + mesh.name + "  (kaynak: " + MeshSource(mesh) + ")");
            report.AppendLine("  " + typeof(T).Name + " bileseni eklendi, mesh Renderer cocuguna kondu");
            report.AppendLine("  olcek " + scale.ToString("0.0000") + " korundu" +
                              (Uneven(scale) ? "  (esit degil, oldugu gibi birakildi)" : ""));
            report.AppendLine("  yigin araligi: " + height.ToString("0.0000"));
            report.AppendLine("  model tabani Y: " + bounds.min.y.ToString("0.0000") +
                              (Mathf.Abs(bounds.min.y) < .001f
                                  ? "  (tam tabaga oturur)"
                                  : "  <-- sifir degil, tabaga gomulur veya havada kalir"));

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            return reloaded == null ? null : reloaded.GetComponent<SpawnableFood>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(built);
        }
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static bool Uneven(Vector3 scale)
    {
        return !Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z);
    }

    private static string MeshSource(Mesh mesh)
    {
        string path = AssetDatabase.GetAssetPath(mesh);

        return string.IsNullOrEmpty(path) ? "bilinmiyor" : System.IO.Path.GetFileName(path);
    }

    // ---- the station -------------------------------------------------------

    private static string WireStation(GameObject zone, SpawnableFood food)
    {
        FoodSpawnerStation reference = FindReferenceStation(zone);

        FoodSpawnerStation station = zone.GetComponent<FoodSpawnerStation>();

        if (station == null)
            station = Undo.AddComponent<FoodSpawnerStation>(zone);

        SerializedObject so = new SerializedObject(station);

        so.FindProperty("spawnableFoodPrefab").objectReferenceValue = food;

        string report = "  yemek: " + food.GetType().Name + "\n";

        if (reference == null)
        {
            so.FindProperty("spawnDelay").floatValue = 1f;
            report += "  ornek istasyon yok, spawnDelay 1 -- elle ayarla\n";
        }
        else
        {
            so.FindProperty("spawnDelay").floatValue =
                new SerializedObject(reference).FindProperty("spawnDelay").floatValue;

            report += "  ornek alinan istasyon: " + reference.name + "\n";
        }

        StringBuilder placement = new StringBuilder();

        Plateau plateau = EnsurePlateau(zone, reference, placement);

        if (plateau != null)
            so.FindProperty("plateau").objectReferenceValue = plateau;

        Transform workerPoint = EnsureWorkerPoint(zone, reference, placement);

        if (workerPoint != null)
            so.FindProperty("workerTargetPoint").objectReferenceValue = workerPoint;

        so.ApplyModifiedProperties();

        report += placement.ToString();
        report += EnsureTrigger(zone, reference);

        report += "\n";
        report += "  Tepsinin yeri  : " + zone.name + " > Plateau (Transform)\n";
        report += "  Kac tane birikecek: ayni yer > Plateau > Max Capacity\n";
        report += "  Yemek buyuklugu: Plateau Hand Adjuster > Yemek prefabi > " + food.name + "\n";

        return report;
    }

    // A station with no plateau is no use as a reference for one: the cheese
    // wheel keeps its pieces itself and has nothing to copy a tray from
    private static FoodSpawnerStation FindReferenceStation(GameObject zone)
    {
        FoodSpawnerStation fallback = null;

        foreach (FoodSpawnerStation candidate in UnityEngine.Object.FindObjectsByType<FoodSpawnerStation>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject == zone)
                continue;

            if (PlateauOf(candidate) != null)
                return candidate;

            if (fallback == null)
                fallback = candidate;
        }

        return fallback;
    }

    private static Plateau EnsurePlateau(GameObject zone, FoodSpawnerStation reference, StringBuilder report)
    {
        Plateau existing = zone.GetComponentInChildren<Plateau>(true);

        if (existing != null)
        {
            report.AppendLine("  plateau: zaten var (" + existing.name +
                              "), Max Capacity " + ReadCapacity(existing));

            return existing;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plateauPrefabPath);

        if (prefab == null)
        {
            report.AppendLine("  plateau: Plateau.prefab bulunamadi");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, zone.transform);

        Undo.RegisterCreatedObjectUndo(instance, "Add station plateau");

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        Plateau added = instance.GetComponent<Plateau>();
        Plateau referencePlateau = PlateauOf(reference);

        int capacity = referencePlateau == null ? 5 : ReadCapacity(referencePlateau);

        SerializedObject so = new SerializedObject(added);

        so.FindProperty("maxCapacity").intValue = capacity;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (referencePlateau != null)
        {
            // Local to its own station, so the same numbers land in the same
            // place on this one however the two are turned in the world
            instance.transform.localPosition =
                reference.transform.InverseTransformPoint(referencePlateau.transform.position);
            instance.transform.localRotation =
                Quaternion.Inverse(reference.transform.rotation) * referencePlateau.transform.rotation;
            instance.transform.localScale = referencePlateau.transform.localScale;

            report.AppendLine("  plateau: eklendi, konum ve Max Capacity (" + capacity + ") " +
                              reference.name + "'dan kopyalandi");
        }
        else
        {
            report.AppendLine("  plateau: eklendi, konum sifir -- elle yerlestir");
        }

        return added;
    }

    private static Plateau PlateauOf(FoodSpawnerStation station)
    {
        return station == null
            ? null
            : new SerializedObject(station).FindProperty("plateau").objectReferenceValue as Plateau;
    }

    private static int ReadCapacity(Plateau plateau)
    {
        return new SerializedObject(plateau).FindProperty("maxCapacity").intValue;
    }

    private static Transform EnsureWorkerPoint(GameObject zone, FoodSpawnerStation reference, StringBuilder report)
    {
        Transform existing = zone.transform.Find(workerPointName);

        if (existing != null)
        {
            report.AppendLine("  worker noktasi: zaten var");
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

            report.AppendLine("  worker noktasi: eklendi, konum kopyalandi");
        }
        else
        {
            report.AppendLine("  worker noktasi: eklendi, konum sifir -- elle ayarla");
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

        // Size copied, centre not: the reference's centre is an offset from its
        // own pivot, and this zone was dropped where the player should stand
        BoxCollider referenceBox = reference == null ? null : FindTriggerBox(reference.gameObject);

        if (referenceBox != null)
        {
            box.center = Vector3.zero;
            box.size = referenceBox.size;

            return "  trigger: eklendi, boyut " + referenceBox.size.ToString("0.00") +
                   " (" + reference.name + "'dan)\n";
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
