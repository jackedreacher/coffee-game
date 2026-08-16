using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

// Wires the oven: a station that takes raw meat, cooks it on a timer and gives
// back a different type, with a radial ring above it showing how far along it
// is. The ring is built here rather than by hand because a world space canvas
// has four numbers that all have to agree -- render mode, rect size, scale and
// facing -- and getting one wrong puts the ring flat on the floor
public static class OvenSetup
{
    private const string foodFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";
    private const string cookedPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/cooked-meat.prefab";
    private const string cookedModelPath = "Assets/Tiny Coffee Shop/FBX-food/meat-cooked.fbx";

    private const string zoneName = "oven-zone";
    private const string ovenName = "Environment_Oven";
    private const string panName = "Environment_Pan";

    private const string cookPointName = "Cook Point";
    private const string workerPointName = "Worker Target Point";
    private const string timerName = "Cook Timer";

    // Diameter in world units. Matches CookingStation's own default
    private const float defaultTimerSize = .35f;

    [MenuItem("Cooked Fast/Oven: 1 - Istasyonu Kur (pismis et dahil)", priority = 140)]
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
                "Ne " + zoneName + " ne de " + ovenName + " bulundu.\n\n" +
                "Hierarchy'den ocagi sec ve komutu tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Ocak: " + zone.name + zoneNote);
        report.AppendLine("  layer: " + LayerMask.LayerToName(zone.layer));
        report.AppendLine();

        SpawnableFood raw = FindFood("meat");

        if (raw == null)
        {
            report.Insert(0, "SONUC: meat.prefab yemek degil, once 'Meat: 1 - Istasyonu Kur' calistir\n\n");
            Show(report, "Oven Kurulumu -- YARIM KALDI");
            return;
        }

        SpawnableFood cooked = BuildCookedMeat(raw, report);

        if (cooked == null)
        {
            report.Insert(0, "SONUC: pismis et prefabi hazirlanamadi, ocaga dokunulmadi\n\n");
            Show(report, "Oven Kurulumu -- YARIM KALDI");
            return;
        }

        report.AppendLine();
        report.Append(MarkRaw(raw));
        report.AppendLine();
        report.Append(WireStation(zone, raw, cooked));

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = zone;

        report.Insert(0, Verdict(zone));

        Show(report, "Oven Kurulumu");
    }

    private static string Verdict(GameObject zone)
    {
        CookingStation station = zone.GetComponent<CookingStation>();

        if (station == null)
            return "SONUC: " + zone.name + " uzerinde CookingStation yok\n\n";

        SerializedObject so = new SerializedObject(station);

        SpawnableFood raw = so.FindProperty("rawFoodPrefab").objectReferenceValue as SpawnableFood;
        SpawnableFood cooked = so.FindProperty("cookedFoodPrefab").objectReferenceValue as SpawnableFood;
        UnityEngine.Object point = so.FindProperty("cookPoint").objectReferenceValue;
        UnityEngine.Object fill = so.FindProperty("timerFill").objectReferenceValue;

        if (raw == null || cooked == null)
            return "SONUC: ham ya da pismis yemek alani BOS\n\n";

        if (point == null)
            return "SONUC: Cook Point BOS, et konacak yer yok\n\n";

        return "SONUC: " + zone.name + " " + raw.GetType().Name + " alip " +
               cooked.GetType().Name + " veriyor" +
               (fill == null ? "  (sayac halkasi yok)" : "") + "\n\n";
    }

    private static void Show(StringBuilder report, string title)
    {
        Debug.Log("[" + title + "]\n" + report);
        EditorUtility.DisplayDialog(title, report.ToString(), "Tamam");
    }

    // ---- finding things ----------------------------------------------------

    // The hand made zone wins if there is one, and the oven prop itself is the
    // fallback -- it is the thing the player walks up to either way
    private static GameObject ResolveZone(out string note)
    {
        GameObject selected = Selection.activeGameObject;
        bool inScene = selected != null && selected.scene.IsValid();

        if (inScene && (Same(selected.name, zoneName) || Same(selected.name, ovenName)))
        {
            note = "";
            return selected;
        }

        GameObject named = FindInScene(zoneName);

        if (named != null)
        {
            note = "  (isimden bulundu)";
            return named;
        }

        named = FindInScene(ovenName);

        if (named != null)
        {
            note = "  (" + zoneName + " yok, " + ovenName + " kullanildi)";
            return named;
        }

        if (!inScene)
        {
            note = "";
            return null;
        }

        note = "  <-- secili oldugu icin kullanildi";

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

    private static SpawnableFood FindFood(string foodName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { foodFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!Same(System.IO.Path.GetFileNameWithoutExtension(path), foodName))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            return prefab == null ? null : prefab.GetComponent<SpawnableFood>();
        }

        return null;
    }

    private static bool TryBounds(GameObject target, out Bounds bounds)
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

    // ---- the cooked food ---------------------------------------------------

    // Sized off the raw one rather than off the salad: the two are the same food
    // at two moments, and a steak that lands on the tray a different size from
    // the patty that went into the pan reads as the wrong item
    private static SpawnableFood BuildCookedMeat(SpawnableFood raw, StringBuilder report)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(cookedPrefabPath);

        if (existing != null && existing.GetComponent<CookedMeat>() != null)
        {
            report.AppendLine("cooked-meat.prefab zaten var, dokunulmadi");
            report.AppendLine("  " + cookedPrefabPath);

            return existing.GetComponent<SpawnableFood>();
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(cookedModelPath);
        MeshFilter source = model == null ? null : model.GetComponentInChildren<MeshFilter>(true);

        if (source == null || source.sharedMesh == null)
        {
            report.AppendLine("meat-cooked.fbx bulunamadi: " + cookedModelPath);
            return null;
        }

        MeshFilter rawFilter = raw.GetComponentInChildren<MeshFilter>(true);

        if (rawFilter == null || rawFilter.sharedMesh == null)
        {
            report.AppendLine("meat.prefab icinde mesh yok");
            return null;
        }

        Vector3 rawScale = rawFilter.transform.localScale;
        Vector3 rawSize = rawFilter.sharedMesh.bounds.size;

        float wanted = Mathf.Max(rawSize.x * Mathf.Abs(rawScale.x), rawSize.z * Mathf.Abs(rawScale.z));

        Bounds bounds = source.sharedMesh.bounds;
        float have = Mathf.Max(bounds.size.x, bounds.size.z);
        float fit = have <= .0001f || wanted <= .0001f ? 1f : wanted / have;

        GameObject built = new GameObject("cooked-meat");

        try
        {
            GameObject visual = new GameObject("Renderer");

            visual.transform.SetParent(built.transform, false);

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();

            filter.sharedMesh = source.sharedMesh;

            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();

            if (sourceRenderer != null && sourceRenderer.sharedMaterials.Length > 0)
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;

            // Course rule: model node at zero, no rotation, size the only
            // difference. FoodPosition.Push zeroes the first two on every pickup
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * fit;

            CookedMeat cooked = built.AddComponent<CookedMeat>();

            float height = bounds.size.y * fit;

            SerializedObject so = new SerializedObject(cooked);

            so.FindProperty("filter").objectReferenceValue = filter;
            so.FindProperty("meshRenderer").objectReferenceValue = renderer;
            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyMesh").objectReferenceValue = source.sharedMesh;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(built, cookedPrefabPath, out bool saved);

            if (!saved)
            {
                report.AppendLine("KAYIT BASARISIZ: " + cookedPrefabPath);
                return null;
            }

            report.AppendLine("cooked-meat.prefab olusturuldu");
            report.AppendLine("  " + cookedPrefabPath);
            report.AppendLine("  mesh: " + source.sharedMesh.name + "  (meat-cooked.fbx)");
            report.AppendLine("  olcek " + fit.ToString("0.0000") + " -- ham etin genisligine esitlendi");
            report.AppendLine("  yigin araligi: " + height.ToString("0.0000"));

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(cookedPrefabPath);

            return reloaded == null ? null : reloaded.GetComponent<SpawnableFood>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(built);
        }
    }

    // One flag on the raw food, and serving asks the food rather than the
    // counter. The alternative -- teaching every counter which foods it sells --
    // shut the door on the salad and the pizza too
    private static string MarkRaw(SpawnableFood raw)
    {
        string path = AssetDatabase.GetAssetPath(raw);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
            return "  " + path + " acilamadi\n";

        try
        {
            SpawnableFood food = root.GetComponent<SpawnableFood>();

            if (food == null)
                return "  " + path + " icinde SpawnableFood yok\n";

            SerializedObject so = new SerializedObject(food);
            SerializedProperty flag = so.FindProperty("ingredientOnly");

            if (flag == null)
                return "  ingredientOnly alani yok -- derleme bitmemis olabilir\n";

            if (flag.boolValue)
                return "  " + System.IO.Path.GetFileName(path) + " zaten 'cig' isaretli\n";

            flag.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);

            return "  " + System.IO.Path.GetFileName(path) +
                   " 'cig' isaretlendi -- pisirilmeden musteriye verilemez\n";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- why the oven is not taking anything -------------------------------

    // Two things stop meat going in and neither is visible from the game: a
    // trigger the player cannot stand inside, and a FoodSpawnerStation left on
    // the same object -- PlayerDetector checks for that one first, so it wins
    // and the oven is never even asked
    [MenuItem("Cooked Fast/Oven: 2 - Neden Et Girmiyor (kontrol + duzelt)", priority = 141)]
    public static void Diagnose()
    {
        GameObject zone = ResolveZone(out _);

        if (zone == null)
        {
            EditorUtility.DisplayDialog("Hata", "oven-zone bulunamadi", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Ocak: " + zone.name);
        report.AppendLine();

        CookingStation station = zone.GetComponent<CookingStation>();

        if (station == null)
        {
            report.AppendLine("CookingStation YOK -- once 'Oven: 1 - Istasyonu Kur' calistir");
            Show(report, "Ocak Kontrolu");
            return;
        }

        SerializedObject so = new SerializedObject(station);

        SpawnableFood raw = so.FindProperty("rawFoodPrefab").objectReferenceValue as SpawnableFood;

        report.AppendLine("alir: " + (raw == null ? "BOS" : raw.GetType().Name));
        report.AppendLine("verir: " + Name(so.FindProperty("cookedFoodPrefab").objectReferenceValue));
        report.AppendLine("Cook Point: " + Name(so.FindProperty("cookPoint").objectReferenceValue));
        report.AppendLine("Capacity: " + so.FindProperty("capacity").intValue);
        report.AppendLine();

        // PlayerDetector's chain is if / else if. Anything earlier in it on this
        // same object swallows the trigger and the oven never runs
        FoodSpawnerStation spawner = zone.GetComponent<FoodSpawnerStation>();

        if (spawner != null)
        {
            Undo.DestroyObjectImmediate(spawner);
            report.AppendLine("FoodSpawnerStation KALDIRILDI -- PlayerDetector once onu");
            report.AppendLine("  yakaliyordu, ocak hic sorulmuyordu. Sebep buydu.");
            report.AppendLine();
        }

        report.Append(FitTrigger(zone));

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = zone;

        Show(report, "Ocak Kontrolu");
    }

    private static string Name(UnityEngine.Object value)
    {
        return value == null ? "BOS" : value.name;
    }

    // Setup looks for a pan by name and settles for the middle of the oven when
    // it cannot find one. Guessing harder is the wrong answer -- pointing at the
    // pan takes one click and is right every time
    [MenuItem("Cooked Fast/Oven: 3 - Cook Point'i Secili Tavaya Tasi", priority = 142)]
    public static void SnapCookPoint()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da tasima kaydedilmez. Once Play'i durdur.", "Tamam");
            return;
        }

        GameObject target = Selection.activeGameObject;

        if (target == null || !target.scene.IsValid())
        {
            EditorUtility.DisplayDialog("Hata",
                "Hierarchy'den tavayi (ya da etin durmasini istedigin objeyi) sec,\n" +
                "sonra bu komutu calistir.",
                "Tamam");
            return;
        }

        GameObject zone = ResolveZone(out _);
        CookingStation station = zone == null ? null : zone.GetComponent<CookingStation>();

        if (station == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "oven-zone uzerinde CookingStation yok.\n" +
                "Once 'Oven: 1 - Istasyonu Kur' calistir.", "Tamam");
            return;
        }

        SerializedObject so = new SerializedObject(station);

        Transform point = so.FindProperty("cookPoint").objectReferenceValue as Transform;

        if (point == null)
            point = zone.transform.Find(cookPointName);

        if (point == null)
        {
            EditorUtility.DisplayDialog("Hata",
                cookPointName + " bulunamadi. 'Oven: 1' calistir.", "Tamam");
            return;
        }

        if (!TryBounds(target, out Bounds bounds))
        {
            EditorUtility.DisplayDialog("Hata",
                target.name + " icinde renderer yok, olculemedi.\n" +
                "Tavanin kendisini (mesh'i olan objeyi) sec.",
                "Tamam");
            return;
        }

        Undo.RecordObject(point, "Snap cook point");

        Vector3 was = point.position;

        point.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

        so.FindProperty("cookPoint").objectReferenceValue = point;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = point.gameObject;

        Debug.Log("[Ocak] " + cookPointName + " -> " + target.name + "\n" +
                  "  onceki: " + was.ToString("0.000") + "\n" +
                  "  simdi : " + point.position.ToString("0.000") +
                  "  (" + target.name + " ust yuzeyi)\n" +
                  "  Tavanin olculeri: merkez " + bounds.center.ToString("0.000") +
                  " boyut " + bounds.size.ToString("0.000") + "\n" +
                  "  Et tavanin kenarinda kaliyorsa " + cookPointName +
                  "'i elle kaydir -- duz bir Transform, secili birakildi.");
    }

    // Grown to cover the oven plus room to stand. A trigger the size of the prop
    // can only be entered by walking into the prop, which its solid collider
    // exists to prevent
    private static string FitTrigger(GameObject zone)
    {
        BoxCollider box = null;

        foreach (Collider candidate in zone.GetComponents<Collider>())
        {
            if (!candidate.isTrigger)
                continue;

            box = candidate as BoxCollider;

            if (box == null)
                return "trigger var ama BoxCollider degil (" + candidate.GetType().Name +
                       ") -- boyutunu elle buyut\n";

            break;
        }

        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider>(zone);
            box.isTrigger = true;
        }

        Undo.RecordObject(box, "Fit oven trigger");

        Vector3 wasCenter = box.center;
        Vector3 wasSize = box.size;

        if (!TryBounds(zone, out Bounds bounds))
        {
            box.size = Divide(Vector3.one * 2.5f, zone.transform.lossyScale);

            return "trigger: ocakta renderer yok, 2.5 birim kup kondu\n";
        }

        Vector3 local = Divide(bounds.size, zone.transform.lossyScale);
        Vector3 pad = Divide(Vector3.one * 1.6f, zone.transform.lossyScale);

        box.center = zone.transform.InverseTransformPoint(bounds.center);
        box.size = local + pad;

        Vector3 world = Vector3.Scale(box.size, zone.transform.lossyScale);

        return "trigger buyutuldu\n" +
               "  onceki: merkez " + wasCenter.ToString("0.00") + " boyut " + wasSize.ToString("0.00") + "\n" +
               "  simdi : merkez " + box.center.ToString("0.00") + " boyut " + box.size.ToString("0.00") + "\n" +
               "  dunyada " + world.ToString("0.00") + " -- oyuncu icine girebilmeli\n" +
               "  Hala girmiyorsa: oyuncunun DURMASI gerekiyor, yururken tetiklenmez\n";
    }

    // ---- the station -------------------------------------------------------

    private static string WireStation(GameObject zone, SpawnableFood raw, SpawnableFood cooked)
    {
        CookingStation station = zone.GetComponent<CookingStation>();
        bool fresh = station == null;

        if (fresh)
            station = Undo.AddComponent<CookingStation>(zone);

        SerializedObject so = new SerializedObject(station);

        so.FindProperty("rawFoodPrefab").objectReferenceValue = raw;
        so.FindProperty("cookedFoodPrefab").objectReferenceValue = cooked;

        string report = "  alir: " + raw.GetType().Name + "\n";

        report += "  verir: " + cooked.GetType().Name + "\n";

        if (fresh)
            so.FindProperty("cookDuration").floatValue = 6f;

        report += "  pisme suresi: " + so.FindProperty("cookDuration").floatValue.ToString("0.0") + " sn\n";

        Transform cookPoint = EnsureCookPoint(zone, ref report);

        so.FindProperty("cookPoint").objectReferenceValue = cookPoint;

        Transform workerPoint = EnsureChild(zone, workerPointName, ref report);

        so.FindProperty("workerTargetPoint").objectReferenceValue = workerPoint;

        GameObject timerRoot = EnsureTimer(zone, out Image fill, ref report);

        so.FindProperty("timerRoot").objectReferenceValue = timerRoot;
        so.FindProperty("timerFill").objectReferenceValue = fill;

        so.ApplyModifiedProperties();

        report += EnsureTrigger(zone);

        report += "\n";
        report += "  Pisme suresi     : " + zone.name + " > Cooking Station > Cook Duration\n";
        report += "  Halka buyuklugu  : ayni yer > Timer Size (dunya birimi)\n";
        report += "  Ayni anda kac et : ayni yer > Capacity\n";
        report += "  Etin durdugu yer : " + zone.name + " > " + cookPointName + "\n";
        report += "  Halkanin yeri    : " + zone.name + " > " + timerName + " (Transform)\n";

        return report;
    }

    // Inside the pan if the scene has one, on top of the oven otherwise. Either
    // beats leaving it at the zone's pivot, which is usually on the floor
    private static Transform EnsureCookPoint(GameObject zone, ref string report)
    {
        Transform existing = zone.transform.Find(cookPointName);

        if (existing != null)
        {
            report += "  " + cookPointName + ": zaten var\n";
            return existing;
        }

        GameObject point = new GameObject(cookPointName);

        Undo.RegisterCreatedObjectUndo(point, "Add cook point");
        Undo.SetTransformParent(point.transform, zone.transform, "Add cook point");

        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;
        point.transform.localPosition = Vector3.zero;

        GameObject pan = FindInScene(panName);

        if (pan != null && TryBounds(pan, out Bounds panBounds))
        {
            point.transform.position = new Vector3(panBounds.center.x, panBounds.max.y, panBounds.center.z);

            report += "  " + cookPointName + ": " + panName + " icine kondu\n";
        }
        else if (TryBounds(zone, out Bounds ovenBounds))
        {
            point.transform.position =
                new Vector3(ovenBounds.center.x, ovenBounds.max.y, ovenBounds.center.z);

            report += "  " + cookPointName + ": ocagin ustune kondu (" + panName + " yok)\n";
        }
        else
        {
            report += "  " + cookPointName + ": eklendi, konum sifir -- elle yerlestir\n";
        }

        // The food keeps its own local scale when it is reparented here, so a
        // cook point that inherited an odd scale from the prop would resize it
        point.transform.localScale = Divide(Vector3.one, zone.transform.lossyScale);

        return point.transform;
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

    // ---- the ring ----------------------------------------------------------

    // A world space canvas, a dim disc behind and a bright one in front set to
    // Filled / Radial 360. FaceCamera turns the whole thing to the camera every
    // frame, which is what keeps it off the floor without anyone having to work
    // out the isometric rotation by hand
    private static GameObject EnsureTimer(GameObject zone, out Image fill, ref string report)
    {
        Transform existing = zone.transform.Find(timerName);

        if (existing != null)
        {
            fill = FindFill(existing);

            report += "  " + timerName + ": zaten var" +
                      (fill == null ? "  <-- icinde Filled Image yok" : "") + "\n";

            return existing.gameObject;
        }

        GameObject root = new GameObject(timerName);

        // Canvas brings its own RectTransform. Asking for one in the constructor
        // instead means replacing a Transform that already exists, which Unity
        // allows and then complains about
        Canvas canvas = root.AddComponent<Canvas>();

        Undo.RegisterCreatedObjectUndo(root, "Add cook timer");
        Undo.SetTransformParent(root.transform, zone.transform, "Add cook timer");

        canvas.renderMode = RenderMode.WorldSpace;

        root.AddComponent<FaceCamera>();

        RectTransform rect = (RectTransform)root.transform;

        // 100 units across at a hundredth scale is one world unit. Keeping the
        // rect in the hundreds rather than in fractions is what the rest of this
        // project's world space UI does, and mixing the two conventions is how a
        // canvas ends up either invisible or the size of the room
        // 100 canvas units across. The scale that turns that into a sensible
        // diameter is worked out by CookingStation from its Timer Size field --
        // set here only so a fresh setup does not flash up a ring the size of
        // the kitchen before the first OnValidate
        rect.sizeDelta = new Vector2(100f, 100f);
        rect.localScale = Divide(Vector3.one * (defaultTimerSize / 100f), zone.transform.lossyScale);
        rect.localRotation = Quaternion.identity;

        if (TryBounds(zone, out Bounds bounds))
            rect.position = new Vector3(bounds.center.x, bounds.max.y + .5f, bounds.center.z);
        else
            rect.localPosition = Vector3.up;

        Sprite disc = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        AddDisc(rect, "Back", disc, new Color(0f, 0f, 0f, .35f), false);

        fill = AddDisc(rect, "Fill", disc, new Color(1f, .78f, .25f, 1f), true);

        report += "  " + timerName + ": eklendi (World Space Canvas + Radial 360)\n";
        report += "    ocagin 0.5 birim ustune kondu -- elle kaydirabilirsin\n";

        return root;
    }

    private static Image AddDisc(RectTransform parent, string name, Sprite sprite, Color color, bool filled)
    {
        GameObject go = new GameObject(name);

        // Image brings RectTransform and CanvasRenderer with it
        Image image = go.AddComponent<Image>();

        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;

        if (!filled)
            return image;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
        image.fillAmount = 0f;

        return image;
    }

    private static Image FindFill(Transform root)
    {
        foreach (Image candidate in root.GetComponentsInChildren<Image>(true))
        {
            if (candidate.type == Image.Type.Filled)
                return candidate;
        }

        return null;
    }

    // ---- the trigger -------------------------------------------------------

    private static string EnsureTrigger(GameObject zone)
    {
        foreach (Collider candidate in zone.GetComponents<Collider>())
        {
            if (candidate.isTrigger)
                return "  trigger: zaten var (" + candidate.GetType().Name + ")\n";
        }

        BoxCollider box = Undo.AddComponent<BoxCollider>(zone);

        box.isTrigger = true;

        // Sized off the oven itself and grown a little, so the player standing in
        // front of it is inside. A trigger the size of the prop is only entered
        // by walking into the prop, which the collider on it prevents
        if (TryBounds(zone, out Bounds bounds))
        {
            // Both the size and the padding go through the scale. Adding world
            // units to a local size is how a trigger on a prop scaled to a
            // hundredth ends up swallowing the room
            Vector3 local = Divide(bounds.size, zone.transform.lossyScale);
            Vector3 pad = Divide(Vector3.one * 1.2f, zone.transform.lossyScale);

            box.center = zone.transform.InverseTransformPoint(bounds.center);
            box.size = local + pad;

            return "  trigger: eklendi, ocagin boyutundan turetildi\n";
        }

        box.size = Vector3.one * 2f;

        return "  trigger: eklendi, boyut 2x2x2 -- elle ayarla\n";
    }

    private static Vector3 Divide(Vector3 wanted, Vector3 parent)
    {
        return new Vector3(
            Mathf.Abs(parent.x) < .0001f ? wanted.x : wanted.x / parent.x,
            Mathf.Abs(parent.y) < .0001f ? wanted.y : wanted.y / parent.y,
            Mathf.Abs(parent.z) < .0001f ? wanted.z : wanted.z / parent.z);
    }
}
