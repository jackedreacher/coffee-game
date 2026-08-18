using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Turns cheese.prefab into a food and cheese-zone into a wheel station: a ring
// of wedges that loses one on every pickup and comes back whole once the last
// one is gone. Nothing here touches Plateau -- the wheel is the station's own
public static class CheeseSetup
{
    private const string foodFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";

    private const string zoneName = "cheese-zone";
    private const string wheelName = "Wheel";
    private const string workerPointName = "Worker Target Point";

    private const int defaultPieces = 6;

    [MenuItem("Cooked Fast/Istasyon/Cheese: 1 - Istasyonu Kur (yemek dahil)", priority = 110)]
    public static void Setup()
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

        GameObject zone = ResolveZone(out string zoneNote);

        if (zone == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "cheese-zone bulunamadi.\n\n" +
                "Hierarchy'den cheese-zone objesini sec ve komutu tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Istasyon: " + zone.name + zoneNote);
        report.AppendLine("  layer: " + LayerMask.LayerToName(zone.layer));
        report.AppendLine();

        SpawnableFood cheese = BuildCheeseFood(report, out float pieceWidth);

        if (cheese == null)
        {
            report.Insert(0, "SONUC: yemek prefabi hazirlanamadi, istasyona dokunulmadi\n\n");
            Show(report, "Cheese Kurulumu -- YARIM KALDI");
            return;
        }

        report.AppendLine();
        report.Append(WireStation(zone, cheese, pieceWidth));

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = zone;

        report.Insert(0, Verdict(zone));

        Show(report, "Cheese Kurulumu");
    }

    // The wheel only exists while the game runs, so in edit mode there is nothing
    // to aim at and every radius has to be judged from memory between play
    // sessions. This lays out the same ring with the same arithmetic, in edit
    // mode, where the numbers it is judged against can actually be saved
    [MenuItem("Cooked Fast/Istasyon/Cheese: 2 - Onizleme Cemberini Yenile", priority = 111)]
    public static void RefreshPreview()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da gerek yok -- cember zaten duruyor.\n" +
                "Radius'u degistirdiginde aninda yerlesir.",
                "Tamam");
            return;
        }

        GameObject zone = ResolveZone(out _);
        CheeseWheelStation station = zone == null ? null : zone.GetComponent<CheeseWheelStation>();

        if (station == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "cheese-zone uzerinde CheeseWheelStation yok.\n" +
                "Once 'Cheese: 1 - Istasyonu Kur' calistir.", "Tamam");
            return;
        }

        SerializedObject so = new SerializedObject(station);

        Transform wheel = so.FindProperty("wheelCentre").objectReferenceValue as Transform;

        // The field holds the component, not the GameObject -- instantiating
        // needs the root the component sits on
        SpawnableFood food = so.FindProperty("spawnableFoodPrefab").objectReferenceValue as SpawnableFood;
        GameObject prefab = food == null ? null : food.gameObject;

        if (wheel == null || prefab == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Wheel Centre ya da yemek prefabi bos. Once kurulumu calistir.", "Tamam");
            return;
        }

        int removed = ClearPreview(wheel);

        int count = so.FindProperty("pieceCount").intValue;
        float radius = so.FindProperty("radius").floatValue;
        bool faceOutwards = so.FindProperty("faceOutwards").boolValue;
        float rise = so.FindProperty("risePerPiece").floatValue;

        for (int i = 0; i < count; i++)
        {
            GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wheel);

            Undo.RegisterCreatedObjectUndo(piece, "Cheese preview");

            piece.name = previewPrefix + i;

            CheeseWheelStation.Place(piece.transform, i, count, radius, faceOutwards, rise);
        }

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = wheel.gameObject;

        Debug.Log("[Cheese onizleme] " + count + " parca kondu (eski " + removed + " silindi)" +
                  "  yaricap " + radius.ToString("0.0000") +
                  "\nRadius'u degistir, bu komutu tekrar calistir." +
                  "\nBitince 'Cheese: 3 - Onizlemeyi Kaldir' -- oyuna girmemeli.");
    }

    [MenuItem("Cooked Fast/Istasyon/Cheese: 3 - Onizlemeyi Kaldir", priority = 112)]
    public static void RemovePreview()
    {
        GameObject zone = ResolveZone(out _);
        CheeseWheelStation station = zone == null ? null : zone.GetComponent<CheeseWheelStation>();

        if (station == null)
            return;

        Transform wheel = new SerializedObject(station)
            .FindProperty("wheelCentre").objectReferenceValue as Transform;

        if (wheel == null)
            return;

        int removed = ClearPreview(wheel);

        EditorSceneManager.MarkSceneDirty(zone.scene);

        Debug.Log("[Cheese onizleme] " + removed + " parca kaldirildi");
    }

    private const string previewPrefix = "PREVIEW cheese ";

    // Collected before anything is destroyed: killing a preview takes its
    // children with it, and entries already read out turn into missing
    // references the moment they are touched
    private static int ClearPreview(Transform wheel)
    {
        System.Collections.Generic.List<GameObject> doomed =
            new System.Collections.Generic.List<GameObject>();

        foreach (Transform candidate in wheel.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != null && candidate.name.StartsWith(previewPrefix))
                doomed.Add(candidate.gameObject);
        }

        int removed = 0;

        foreach (GameObject target in doomed)
        {
            if (target == null)
                continue;

            Undo.DestroyObjectImmediate(target);
            removed++;
        }

        return removed;
    }

    private static string Verdict(GameObject zone)
    {
        CheeseWheelStation station = zone.GetComponent<CheeseWheelStation>();

        if (station == null)
            return "SONUC: " + zone.name + " uzerinde CheeseWheelStation yok\n\n";

        SerializedObject so = new SerializedObject(station);

        SpawnableFood given = so.FindProperty("spawnableFoodPrefab").objectReferenceValue as SpawnableFood;
        UnityEngine.Object centre = so.FindProperty("wheelCentre").objectReferenceValue;

        if (given == null)
            return "SONUC: istasyonun Spawnable Food Prefab alani BOS\n\n";

        if (centre == null)
            return "SONUC: Wheel Centre alani BOS, cember kurulamaz\n\n";

        return "SONUC: " + zone.name + " artik " + given.GetType().Name + " veriyor, " +
               so.FindProperty("pieceCount").intValue + " parcali cember" +
               (given is Cheese ? "" : "  <-- Cheese olmasi gerekiyordu") + "\n\n";
    }

    private static void Show(StringBuilder report, string title)
    {
        Debug.Log("[" + title + "]\n" + report);
        EditorUtility.DisplayDialog(title, report.ToString(), "Tamam");
    }

    // ---- finding things ----------------------------------------------------

    private static GameObject ResolveZone(out string note)
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

        note = "  <-- adi cheese-zone degil, secili oldugu icin kullanildi";

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

    private static string FindCheesePrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { foodFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Same(System.IO.Path.GetFileNameWithoutExtension(path), "cheese"))
                return path;
        }

        return null;
    }

    // ---- the food prefab ---------------------------------------------------

    // Rebuilt rather than edited in place. cheese.prefab is a variant of
    // cheese.fbx -- its mesh belongs to the model prefab, so moving that mesh
    // onto a child means removing a component the base owns and hoping the
    // override sticks. Writing a plain prefab over the same path is one step,
    // and the path keeps its GUID so every reference to it still resolves
    private static SpawnableFood BuildCheeseFood(StringBuilder report, out float pieceWidth)
    {
        pieceWidth = 0f;

        string path = FindCheesePrefab();

        if (path == null)
        {
            report.AppendLine("cheese.prefab bulunamadi: " + foodFolder);
            return null;
        }

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (existing == null)
        {
            report.AppendLine(path + " okunamadi");
            return null;
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

        // The size already chosen by hand, kept. Guessing a new one here is the
        // step that has gone wrong every previous time
        float scale = Mathf.Abs(filter.transform.lossyScale.x);

        if (scale <= .0001f)
            scale = 1f;

        Bounds bounds = mesh.bounds;

        pieceWidth = Mathf.Max(bounds.size.x, bounds.size.z) * scale;

        Cheese already = existing.GetComponent<Cheese>();

        if (already != null)
        {
            report.AppendLine(System.IO.Path.GetFileName(path) + " zaten hazir, dokunulmadi");
            report.AppendLine("  " + path);

            return already;
        }

        GameObject built = new GameObject("cheese");

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
            visual.transform.localScale = Vector3.one * scale;

            Cheese cheese = built.AddComponent<Cheese>();

            float height = bounds.size.y * scale;

            SerializedObject so = new SerializedObject(cheese);

            so.FindProperty("filter").objectReferenceValue = newFilter;
            so.FindProperty("meshRenderer").objectReferenceValue = newRenderer;
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
            report.AppendLine("  Cheese bileseni eklendi, mesh Renderer cocuguna kondu");
            report.AppendLine("  olcek " + scale.ToString("0.0000") + " korundu");
            report.AppendLine("  parca genisligi: " + pieceWidth.ToString("0.0000"));
            report.AppendLine("  yigin araligi: " + height.ToString("0.0000"));
            report.AppendLine("  NOT: cheese.fbx variant bagi koptu, artik duz prefab" +
                              " (Cup/Pizza/Salad ile ayni)");

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            return reloaded == null ? null : reloaded.GetComponent<SpawnableFood>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(built);
        }
    }

    private static string MeshSource(Mesh mesh)
    {
        string path = AssetDatabase.GetAssetPath(mesh);

        return string.IsNullOrEmpty(path) ? "bilinmiyor" : System.IO.Path.GetFileName(path);
    }

    // ---- the station -------------------------------------------------------

    private static string WireStation(GameObject zone, SpawnableFood cheese, float pieceWidth)
    {
        FoodSpawnerStation reference = FindReferenceStation(zone);

        // A plain FoodSpawnerStation left on the zone would fight this one for
        // the same trigger, and PlayerDetector takes whichever it finds first
        FoodSpawnerStation plain = zone.GetComponent<FoodSpawnerStation>();
        string report = "";

        if (plain != null && !(plain is CheeseWheelStation))
        {
            Undo.DestroyObjectImmediate(plain);
            report += "  eski FoodSpawnerStation kaldirildi (cember istasyonu onun yerine geciyor)\n";
        }

        CheeseWheelStation station = zone.GetComponent<CheeseWheelStation>();
        bool fresh = station == null;

        if (fresh)
            station = Undo.AddComponent<CheeseWheelStation>(zone);

        SerializedObject so = new SerializedObject(station);

        so.FindProperty("spawnableFoodPrefab").objectReferenceValue = cheese;

        report += "  yemek: " + cheese.GetType().Name + "\n";

        if (reference == null)
        {
            so.FindProperty("spawnDelay").floatValue = 2f;
            report += "  ornek istasyon yok, yenileme suresi 2sn -- elle ayarla\n";
        }
        else
        {
            so.FindProperty("spawnDelay").floatValue =
                new SerializedObject(reference).FindProperty("spawnDelay").floatValue;

            report += "  yenileme suresi " + reference.name + "'dan kopyalandi\n";
        }

        Transform wheel = EnsureChild(zone, wheelName, ref report);

        so.FindProperty("wheelCentre").objectReferenceValue = wheel;

        // Only on the first run. Re-running the command must not undo a ring the
        // user has since sized by hand -- the field defaults are not a signal,
        // since a freshly added component already carries them
        SerializedProperty pieces = so.FindProperty("pieceCount");
        SerializedProperty radius = so.FindProperty("radius");

        if (fresh)
        {
            pieces.intValue = defaultPieces;

            // Zero, because the mesh is a wedge and a wedge is already a slice of
            // the wheel. Spacing them around a ring like so many separate discs
            // is what left the gaps -- they belong on top of each other, turned
            radius.floatValue = 0f;

            report += "  cember: " + defaultPieces + " parca, yaricap 0" +
                      "  (kama mesh'i icin dogru deger; bosluk kalirsa buyut)\n";
            report += "    parca genisligi karsilastirma icin: " +
                      pieceWidth.ToString("0.0000") + "\n";
        }
        else
        {
            report += "  cember: " + pieces.intValue + " parca, yaricap " +
                      radius.floatValue.ToString("0.0000") + " (elle ayarlanmis, korundu)\n";
        }

        Transform workerPoint = EnsureChild(zone, workerPointName, ref report);

        so.FindProperty("workerTargetPoint").objectReferenceValue = workerPoint;

        so.ApplyModifiedProperties();

        report += EnsureTrigger(zone, reference);

        report += "\n";
        report += "  Cember buyuklugu : cheese-zone > Cheese Wheel Station > Radius\n";
        report += "  Parca sayisi     : ayni yer > Piece Count\n";
        report += "  Cemberin yeri    : cheese-zone > " + wheelName + " (Transform)\n";
        report += "  Peynir buyuklugu : Plateau Hand Adjuster > Yemek prefabi > cheese\n";

        return report;
    }

    private static FoodSpawnerStation FindReferenceStation(GameObject zone)
    {
        foreach (FoodSpawnerStation candidate in UnityEngine.Object.FindObjectsByType<FoodSpawnerStation>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject != zone)
                return candidate;
        }

        return null;
    }

    private static Transform EnsureChild(GameObject zone, string name, ref string report)
    {
        Transform existing = zone.transform.Find(name);

        if (existing != null)
        {
            report += "  " + name + ": zaten var\n";
            return existing;
        }

        GameObject child = new GameObject(name);

        Undo.RegisterCreatedObjectUndo(child, "Add " + name);
        Undo.SetTransformParent(child.transform, zone.transform, "Add " + name);

        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        report += "  " + name + ": eklendi, konum sifir -- elle yerlestir\n";

        return child.transform;
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
