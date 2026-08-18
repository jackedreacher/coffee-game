using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Builds the bread food prefab and turns the hand made bread-zone into a
// station that hands it out. Same shape as the salad station, plus the one
// thing that is different here: the tray's plate mesh is swapped for a cutting
// board, the whole tray is seated on top of Environment_CuttingTable, and the
// stack is lifted to the board's top face -- otherwise the bread floats at the
// height the plate rim used to be
public static class BreadSetup
{
    private const string foodFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";

    private const string breadPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Bread.prefab";
    private const string saladPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Salad.prefab";
    private const string pizzaPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Pizza.prefab";
    private const string plateauPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Plateau.prefab";

    private const string breadModelPath = "Assets/Tiny Coffee Shop/FBX-food/bread.fbx";
    private const string boardModelPath = "Assets/Tiny Coffee Shop/FBX-food/cutting-board.fbx";

    private const string zoneName = "bread-zone";
    private const string surfaceName = "Environment_CuttingTable";
    private const string workerPointName = "Worker Target Point";

    // How much of the table top the board covers. Leaving a margin is what makes
    // it read as a board lying on a table rather than a new table top
    private const float boardCoverage = .55f;

    [MenuItem("Cooked Fast/Istasyon/Bread: 1 - Istasyonu Kur (yemek dahil)", priority = 100)]
    public static void Setup()
    {
        // Everything below adds components and children to a scene object, and
        // play mode throws all of that away the moment it stops
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
                "bread-zone bulunamadi.\n\n" +
                "Hierarchy'den bread-zone objesini sec ve komutu tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Istasyon: " + zone.name + zoneNote);
        report.AppendLine("  layer: " + LayerMask.LayerToName(zone.layer));
        report.AppendLine();

        SpawnableFood bread = BuildBreadPrefab(report);

        if (bread == null)
        {
            report.Insert(0, "SONUC: yemek prefabi hazirlanamadi, istasyona dokunulmadi\n\n");
            Show(report, "Bread Kurulumu -- YARIM KALDI");
            return;
        }

        report.AppendLine();
        report.Append(WireStation(zone, bread));

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = zone;

        // Read back off the station rather than trusting the write. Twice now
        // the wiring has quietly not happened and the only visible sign was a
        // salad coming out of a bread station in play mode
        report.Insert(0, Verdict(zone));

        Show(report, "Bread Kurulumu");
    }

    private static string Verdict(GameObject zone)
    {
        FoodSpawnerStation station = zone.GetComponent<FoodSpawnerStation>();

        if (station == null)
            return "SONUC: " + zone.name + " uzerinde FoodSpawnerStation yok\n\n";

        SpawnableFood given = new SerializedObject(station)
            .FindProperty("spawnableFoodPrefab").objectReferenceValue as SpawnableFood;

        if (given == null)
            return "SONUC: istasyonun Spawnable Food Prefab alani BOS\n\n";

        return "SONUC: " + zone.name + " artik " + given.GetType().Name + " veriyor" +
               (given is Bread ? "" : "  <-- Bread olmasi gerekiyordu") + "\n\n";
    }

    // Moving the table, or scaling it, leaves the board hanging in the old spot.
    // Re-running the whole setup would work too, but this touches nothing but
    // the placement, so hand tuned capacity and trigger sizes survive
    [MenuItem("Cooked Fast/Istasyon/Bread: 2 - Sadece Tahtayi Oturt", priority = 101)]
    public static void ReseatBoard()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz. Once Play'i durdur.", "Tamam");
            return;
        }

        GameObject zone = ResolveZone(out _);

        if (zone == null)
        {
            EditorUtility.DisplayDialog("Hata", "bread-zone bulunamadi", "Tamam");
            return;
        }

        Plateau plateau = zone.GetComponentInChildren<Plateau>(true);

        if (plateau == null)
        {
            EditorUtility.DisplayDialog("Hata",
                zone.name + " altinda Plateau yok. Once 'Bread: 1 - Istasyonu Kur' calistir.", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        SeatOnSurface(zone, plateau, report);

        // This command moves the board and nothing else. Saying so matters:
        // its output looks enough like the full setup's to be mistaken for it,
        // and then a station still handing out salad reads as a bug
        report.Insert(0, "Bu komut SADECE tahtayi yerlestirir, yemegi degistirmez.\n" +
                         Verdict(zone));

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Show(report, "Tahta Yerlesimi (kurulum degil)");
    }

    private static void Show(StringBuilder report, string title)
    {
        Debug.Log("[" + title + "]\n" + report);
        EditorUtility.DisplayDialog(title, report.ToString(), "Tamam");
    }

    // ---- finding things ----------------------------------------------------

    // The name wins over the selection unless the selection is that same object.
    // Backwards from the obvious, on purpose: a command that quietly builds a
    // station onto whatever happened to be highlighted is how a tray scale once
    // went out to seven rabbit prefabs
    private static GameObject ResolveZone(out string note)
    {
        GameObject selected = Selection.activeGameObject;
        bool selectedIsInScene = selected != null && selected.scene.IsValid();

        if (selectedIsInScene && Same(selected.name, zoneName))
        {
            note = "";
            return selected;
        }

        GameObject named = FindInScene(zoneName);

        if (named != null)
        {
            note = selectedIsInScene
                ? "  (isimden bulundu -- secili olan '" + selected.name + "' degil)"
                : "  (isimden bulundu)";

            return named;
        }

        if (!selectedIsInScene)
        {
            note = "";
            return null;
        }

        note = "  <-- adi bread-zone degil, secili oldugu icin kullanildi";

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

    // bread-zone, Bread Zone and bread_zone are the same object as far as anyone
    // typing a name into the hierarchy is concerned
    private static bool Same(string left, string right)
    {
        return string.Equals(Strip(left), Strip(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Strip(string text)
    {
        return text.Replace(" ", "").Replace("-", "").Replace("_", "");
    }

    // The tray we are about to add would otherwise be measured as part of the
    // table when the zone happens to be parented under it
    private static bool TryBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;

        bool any = false;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<Plateau>() != null)
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

    // ---- the food prefab ---------------------------------------------------

    private static SpawnableFood BuildBreadPrefab(StringBuilder report)
    {
        string existingPath = FindBreadPrefab();

        if (existingPath != null)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(existingPath);
            Bread ready = existing == null ? null : existing.GetComponent<Bread>();

            if (ready != null)
            {
                report.AppendLine(System.IO.Path.GetFileName(existingPath) + " zaten hazir, dokunulmadi");
                report.AppendLine("  " + existingPath);

                return ready;
            }

            // A prefab dragged straight out of the FBX folder is a mesh and
            // nothing else. Refusing it and building a second one next to it
            // leaves two bread prefabs and no way to tell which the station
            // uses, so the one already there gets upgraded in place instead --
            // same file, same GUID, so anything already pointing at it still does
            return UpgradeToBread(existingPath, report);
        }

        // Copied from the salad rather than the pizza: the pizza carries a 6.32
        // stretch on Y alone, authored for its own flat mesh, and anything else
        // inheriting it comes out distorted. The salad is the one that was tuned
        // by hand afterwards and now matches the convention
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(saladPrefabPath)
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>(pizzaPrefabPath);

        if (source == null)
        {
            report.AppendLine("Ornek alinacak yemek prefabi yok (Salad / Pizza)");
            return null;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(breadModelPath);

        if (model == null)
        {
            report.AppendLine("bread.fbx bulunamadi: " + breadModelPath);
            return null;
        }

        MeshFilter sourceFilter = model.GetComponentInChildren<MeshFilter>(true);

        if (sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            report.AppendLine("bread.fbx icinde mesh yok");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);

        try
        {
            // Detached so this becomes a prefab in its own right rather than a
            // variant that inherits every later salad edit
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            instance.name = "Bread";

            SpawnableFood old = instance.GetComponent<SpawnableFood>();

            if (old == null)
            {
                report.AppendLine(source.name + " uzerinde SpawnableFood yok");
                return null;
            }

            SerializedObject from = new SerializedObject(old);

            UnityEngine.Object filter = from.FindProperty("filter").objectReferenceValue;
            UnityEngine.Object meshRenderer = from.FindProperty("meshRenderer").objectReferenceValue;

            UnityEngine.Object.DestroyImmediate(old);

            Bread bread = instance.AddComponent<Bread>();

            MeshFilter targetFilter = filter as MeshFilter;
            MeshRenderer targetRenderer = meshRenderer as MeshRenderer;

            if (targetFilter == null)
            {
                report.AppendLine("Ornek prefabin MeshFilter alani bos");
                return null;
            }

            targetFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();

            if (targetRenderer != null && sourceRenderer != null &&
                sourceRenderer.sharedMaterials.Length > 0)
                targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

            // The rule out of the course guide, and the one every food here
            // already follows: the renderer sits at zero with no rotation, and
            // size is the only thing that differs between foods. A rotation
            // baked in at this level is what turned the salad upside down in a
            // hand once the tray started rotating
            float fit = UniformFit(sourceFilter.sharedMesh);

            targetFilter.transform.localPosition = Vector3.zero;
            targetFilter.transform.localRotation = Quaternion.identity;
            targetFilter.transform.localScale = Vector3.one * fit;

            Bounds bounds = sourceFilter.sharedMesh.bounds;
            float height = bounds.size.y * fit;

            SerializedObject to = new SerializedObject(bread);

            to.FindProperty("filter").objectReferenceValue = targetFilter;
            to.FindProperty("meshRenderer").objectReferenceValue = targetRenderer;

            // What the next loaf in the stack sits on top of
            to.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            to.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;

            // Bread has no dirty variant. Pointing this at its own mesh keeps
            // MarkAsDirty from blanking the filter and making it invisible
            to.FindProperty("dirtyMesh").objectReferenceValue = sourceFilter.sharedMesh;
            to.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, breadPrefabPath);

            report.AppendLine("Bread.prefab olusturuldu: " + breadPrefabPath);
            report.AppendLine("  mesh: " + sourceFilter.sharedMesh.name);
            report.AppendLine("  olcek: " + fit.ToString("0.0000") + " (uc eksen esit)");
            report.AppendLine("  yigin araligi: " + height.ToString("0.0000"));
            report.AppendLine("  model tabani Y: " + bounds.min.y.ToString("0.0000") +
                              (Mathf.Abs(bounds.min.y) < .001f
                                  ? "  (tam tahtaya oturur)"
                                  : "  <-- sifir degil, ekmek tahtaya gomulur veya havada kalir"));
            report.AppendLine("  Ince ayar: Plateau Hand Adjuster > Yemek prefabi > Bread");

            return saved.GetComponent<SpawnableFood>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    // By name, not by path: the asset on disk is bread.prefab and the path this
    // file would have created is Bread.prefab. Windows treats those as the same
    // file and Unity does not, which is exactly how you end up with two
    private static string FindBreadPrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { foodFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Same(System.IO.Path.GetFileNameWithoutExtension(path), "bread"))
                return path;
        }

        return null;
    }

    // Turns a plain model prefab into a food: the mesh moves onto a Renderer
    // child, the root goes neutral, and the Bread component gets its fields
    private static SpawnableFood UpgradeToBread(string path, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
        {
            report.AppendLine(path + " acilamadi");
            return null;
        }

        try
        {
            MeshFilter filter = root.GetComponentInChildren<MeshFilter>(true);

            if (filter == null || filter.sharedMesh == null)
            {
                report.AppendLine(path + " icinde mesh yok");
                return null;
            }

            Mesh mesh = filter.sharedMesh;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            Material[] materials = renderer == null ? new Material[0] : renderer.sharedMaterials;

            // Read before anything is normalised: this is the size the model was
            // already at, and overwriting it with a guess of my own is the part
            // that has gone wrong every previous time
            float scale = Mathf.Abs(filter.transform.lossyScale.x);

            if (scale <= .0001f)
                scale = 1f;

            bool meshWasOnRoot = filter.gameObject == root;

            if (meshWasOnRoot)
            {
                // The root has to stay free for the food component and for the
                // neutral transform FoodPosition.Push writes into it. Every other
                // food here keeps its mesh on a child called Renderer
                GameObject child = new GameObject("Renderer");

                child.transform.SetParent(root.transform, false);

                MeshFilter movedFilter = child.AddComponent<MeshFilter>();
                MeshRenderer movedRenderer = child.AddComponent<MeshRenderer>();

                movedFilter.sharedMesh = mesh;
                movedRenderer.sharedMaterials = materials;

                UnityEngine.Object.DestroyImmediate(filter);

                if (renderer != null)
                    UnityEngine.Object.DestroyImmediate(renderer);

                filter = movedFilter;
                renderer = movedRenderer;
            }

            // FoodPosition.Push zeroes the food's position and rotation on every
            // pickup but never its scale, so an offset left on the root is dead
            // weight while a scale left there silently doubles the model's size
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            filter.transform.localPosition = Vector3.zero;
            filter.transform.localRotation = Quaternion.identity;
            filter.transform.localScale = Vector3.one * scale;

            SpawnableFood wrongType = root.GetComponent<SpawnableFood>();

            if (wrongType != null && !(wrongType is Bread))
            {
                report.AppendLine("  " + wrongType.GetType().Name + " bileseni kaldirildi");
                UnityEngine.Object.DestroyImmediate(wrongType);
            }

            Bread bread = root.GetComponent<Bread>();

            if (bread == null)
                bread = root.AddComponent<Bread>();

            Bounds bounds = mesh.bounds;
            float height = bounds.size.y * scale;

            SerializedObject so = new SerializedObject(bread);

            so.FindProperty("filter").objectReferenceValue = filter;
            so.FindProperty("meshRenderer").objectReferenceValue = renderer;
            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyMesh").objectReferenceValue = mesh;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);

            report.AppendLine(System.IO.Path.GetFileName(path) + " yemege cevrildi");
            report.AppendLine("  " + path);
            report.AppendLine("  mesh: " + mesh.name + "  (kaynak: " + MeshSource(mesh) + ")");
            report.AppendLine("  Bread bileseni eklendi");
            report.AppendLine(meshWasOnRoot
                ? "  mesh Renderer adli cocuga tasindi (diger yemeklerle ayni yapi)"
                : "  mesh zaten cocuktaydi");
            report.AppendLine("  olcek " + scale.ToString("0.0000") + " korundu" +
                              "  (salata olcusune gore olsaydi: " +
                              UniformFit(mesh).ToString("0.0000") + ")");
            report.AppendLine("  yigin araligi: " + height.ToString("0.0000"));
            report.AppendLine("  model tabani Y: " + bounds.min.y.ToString("0.0000") +
                              (Mathf.Abs(bounds.min.y) < .001f
                                  ? "  (tam tahtaya oturur)"
                                  : "  <-- sifir degil, tahtaya gomulur veya havada kalir"));

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (saved == null)
            {
                report.AppendLine("  KAYIT OKUNAMADI: " + path);
                return null;
            }

            return saved.GetComponent<SpawnableFood>();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Which FBX the mesh actually came from. Worth printing: a prefab named
    // bread is not proof that the mesh inside it is the bread
    private static string MeshSource(Mesh mesh)
    {
        string path = AssetDatabase.GetAssetPath(mesh);

        return string.IsNullOrEmpty(path) ? "bilinmiyor" : System.IO.Path.GetFileName(path);
    }

    // Matched to the salad's footprint, because that is the one the user sized
    // by hand and is happy with. The pizza is the fallback
    private static float UniformFit(Mesh mesh)
    {
        float wanted = Footprint(saladPrefabPath);

        if (wanted <= .0001f)
            wanted = Footprint(pizzaPrefabPath);

        float have = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);

        if (wanted <= .0001f || have <= .0001f)
            return 1f;

        return wanted / have;
    }

    private static float Footprint(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        MeshFilter filter = prefab == null ? null : prefab.GetComponentInChildren<MeshFilter>(true);

        if (filter == null || filter.sharedMesh == null)
            return 0f;

        Vector3 scale = filter.transform.localScale;
        Vector3 size = filter.sharedMesh.bounds.size;

        return Mathf.Max(size.x * Mathf.Abs(scale.x), size.z * Mathf.Abs(scale.z));
    }

    // ---- the station -------------------------------------------------------

    private static string WireStation(GameObject zone, SpawnableFood bread)
    {
        // The station already in the scene is the reference for everything that
        // has no obvious right answer: how long between spawns, how high the
        // pile goes, how big the trigger is, where a worker stands
        FoodSpawnerStation reference = FindReferenceStation(zone);

        FoodSpawnerStation station = zone.GetComponent<FoodSpawnerStation>();

        if (station == null)
            station = Undo.AddComponent<FoodSpawnerStation>(zone);

        SerializedObject so = new SerializedObject(station);

        so.FindProperty("spawnableFoodPrefab").objectReferenceValue = bread;

        string report = "  yemek: Bread\n";

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

        if (plateau != null)
        {
            StringBuilder seating = new StringBuilder();

            SeatOnSurface(zone, plateau, seating);

            report += seating.ToString();
        }

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

    // The station's own tray, the one bread piles onto while waiting to be
    // taken. Nothing to do with the tray the player carries
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

        Undo.RegisterCreatedObjectUndo(instance, "Add bread plateau");

        Plateau added = instance.GetComponent<Plateau>();
        Plateau referencePlateau = PlateauOf(reference);

        int capacity = referencePlateau == null ? 5 : ReadCapacity(referencePlateau);

        SerializedObject so = new SerializedObject(added);

        so.FindProperty("maxCapacity").intValue = capacity;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Food inherits the tray's scale, so matching the reference tray's world
        // scale is what makes a loaf here come out the same size as a pizza does
        // on its own station
        if (referencePlateau != null)
        {
            instance.transform.localScale =
                Divide(referencePlateau.transform.lossyScale, zone.transform.lossyScale);

            report.AppendLine("  plateau: eklendi, Max Capacity " + capacity +
                              " ve olcek " + reference.name + "'dan kopyalandi");
        }
        else
        {
            report.AppendLine("  plateau: eklendi, Max Capacity " + capacity + " (varsayilan)");
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

    // ---- the board on the table --------------------------------------------

    // Three things in one place, because they only make sense together: the tray
    // goes on the table top, its plate becomes a cutting board, and the stack
    // moves up to the board's top face
    private static void SeatOnSurface(GameObject zone, Plateau plateau, StringBuilder report)
    {
        GameObject surface = FindInScene(surfaceName);

        if (surface == null)
        {
            report.AppendLine("  " + surfaceName + " sahnede bulunamadi -- tepsi elle yerlestirilmeli");
            return;
        }

        if (!TryBounds(surface, out Bounds table))
        {
            report.AppendLine("  " + surfaceName + " icinde renderer yok -- tepsi elle yerlestirilmeli");
            return;
        }

        Undo.RecordObject(plateau.transform, "Seat bread board");

        // Only the Y turn. A prop tilted on any other axis would take the board
        // and everything stacked on it down with it
        plateau.transform.rotation = Quaternion.Euler(0f, surface.transform.eulerAngles.y, 0f);
        plateau.transform.position = new Vector3(table.center.x, table.max.y, table.center.z);

        report.AppendLine("  tepsi " + surfaceName + " ustune oturtuldu");
        report.AppendLine("    masa ust yuzeyi Y: " + table.max.y.ToString("0.000") +
                          "  boyut " + table.size.ToString("0.00"));

        MeshFilter plate = plateau.GetComponentInChildren<MeshFilter>(true);

        if (plate == null)
        {
            report.AppendLine("    tepside MeshFilter yok, tahta takilamadi");
            return;
        }

        Vector3 boardTop = SwapInBoard(plate, table, plateau, report);

        Transform stack = StackOf(plateau);

        if (stack == null)
        {
            report.AppendLine("    Plateau'nun Positions alani bos, yigin yuksekligi ayarlanamadi");
            return;
        }

        Undo.RecordObject(stack, "Seat bread board");

        stack.position = boardTop;

        report.AppendLine("    yigin tahtanin ust yuzune alindi, Y " + boardTop.y.ToString("0.000"));
        report.AppendLine();
        report.AppendLine("  Tahta buyuklugu: " + PathUnder(plate.transform, plateau.transform) + " > Scale");
        report.AppendLine("  Ekmek buyuklugu: Bread.prefab > Renderer > Scale");
        report.AppendLine("  Ust uste binme araligi: Bread.prefab > Clean Y Offset On Plateau");
    }

    // The plate mesh is replaced rather than hidden and doubled up, so there is
    // one renderer to measure and one transform to tune. It is a scene override
    // on the Plateau instance: Plateau.prefab itself, the one the player and
    // every rabbit share, is not touched
    private static Vector3 SwapInBoard(MeshFilter plate, Bounds table, Plateau plateau, StringBuilder report)
    {
        GameObject boardModel = AssetDatabase.LoadAssetAtPath<GameObject>(boardModelPath);
        MeshFilter boardFilter = boardModel == null ? null : boardModel.GetComponentInChildren<MeshFilter>(true);

        if (boardFilter == null || boardFilter.sharedMesh == null)
        {
            report.AppendLine("    cutting-board.fbx bulunamadi, tabak oldugu gibi birakildi");

            return plate.sharedMesh == null
                ? plateau.transform.position
                : TopCentre(plate.transform, plate.sharedMesh.bounds);
        }

        Undo.RecordObject(plate, "Swap in cutting board");
        Undo.RecordObject(plate.transform, "Swap in cutting board");

        plate.sharedMesh = boardFilter.sharedMesh;

        MeshRenderer plateRenderer = plate.GetComponent<MeshRenderer>();
        MeshRenderer boardRenderer = boardFilter.GetComponent<MeshRenderer>();

        if (plateRenderer != null && boardRenderer != null && boardRenderer.sharedMaterials.Length > 0)
        {
            Undo.RecordObject(plateRenderer, "Swap in cutting board");
            plateRenderer.sharedMaterials = boardRenderer.sharedMaterials;
        }

        // The plate carried a rotation and a three way stretch authored for its
        // own mesh. Inheriting either would stand the board on its edge
        plate.transform.localPosition = Vector3.zero;
        plate.transform.localRotation = Quaternion.identity;

        Bounds local = boardFilter.sharedMesh.bounds;

        float meshWidth = Mathf.Max(local.size.x, local.size.z);
        float wantedWidth = Mathf.Min(table.size.x, table.size.z) * boardCoverage;
        float trayScale = Mathf.Max(Mathf.Abs(plate.transform.parent.lossyScale.x), .0001f);

        float fit = meshWidth <= .0001f ? 1f : wantedWidth / (meshWidth * trayScale);

        plate.transform.localScale = Vector3.one * fit;

        report.AppendLine("    tabak yerine kesme tahtasi kondu (sadece bu sahne kopyasinda," +
                          " Plateau.prefab'a dokunulmadi)");
        report.AppendLine("    tahta genisligi: " + wantedWidth.ToString("0.000") +
                          "  olcek " + fit.ToString("0.0000"));
        report.AppendLine("    tahta tabani Y: " + local.min.y.ToString("0.0000") +
                          (Mathf.Abs(local.min.y) < .001f
                              ? "  (tam masaya oturur)"
                              : "  <-- sifir degil, tahta masaya gomulur veya havada kalir"));

        return TopCentre(plate.transform, local);
    }

    // Read off the mesh and the transform rather than Renderer.bounds: a renderer
    // whose mesh and scale were both changed a line ago has not necessarily
    // recalculated, and the stack height depends on this being exact
    private static Vector3 TopCentre(Transform target, Bounds local)
    {
        return target.TransformPoint(new Vector3(local.center.x, local.max.y, local.center.z));
    }

    private static Transform StackOf(Plateau plateau)
    {
        SerializedProperty property = new SerializedObject(plateau).FindProperty("foodPositionsParent");

        return property == null ? null : property.objectReferenceValue as Transform;
    }

    private static Vector3 Divide(Vector3 wanted, Vector3 parent)
    {
        return new Vector3(
            Mathf.Abs(parent.x) < .0001f ? wanted.x : wanted.x / parent.x,
            Mathf.Abs(parent.y) < .0001f ? wanted.y : wanted.y / parent.y,
            Mathf.Abs(parent.z) < .0001f ? wanted.z : wanted.z / parent.z);
    }

    private static string PathUnder(Transform target, Transform root)
    {
        string path = target.name;

        while (target.parent != null && target.parent != root)
        {
            target = target.parent;
            path = target.name + " > " + path;
        }

        return root.name + " > " + path;
    }
}
