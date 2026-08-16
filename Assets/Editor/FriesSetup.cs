using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

// Wires every fries-zone in the scene, however many there are.
//
// Written for the plural on purpose: two fryers already exist and setting one up
// by hand while the other quietly stays broken is the failure that costs an
// evening. Everything is found by name, reported per zone, and re-runnable
public static class FriesSetup
{
    private const string zonePrefix = "fries-zone";
    private const string friesPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/fries.prefab";

    private const string standPointName = "Stand Point";
    private const string readyPointName = "Ready Point";
    private const string oilName = "oil_Volume";
    private const string surfaceName = "oil_Surface";
    private const string timerName = "Fry Timer";
    private const string clickBoxName = "Click Box";

    private const float defaultTimerSize = .35f;

    [MenuItem("Cooked Fast/Patates: 1 - Fritozlari Kur", priority = 150)]
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

        StringBuilder report = new StringBuilder();

        SpawnableFood fries = BuildFries(report);

        if (fries == null)
        {
            report.Insert(0, "SONUC: patates prefabi hazirlanamadi, fritozlara dokunulmadi\n\n");
            Show(report);
            return;
        }

        report.AppendLine();

        GameObject[] zones = FindZones();

        if (zones.Length <= 0)
        {
            report.AppendLine("Sahnede adi '" + zonePrefix + "' ile baslayan obje yok.");
            report.Insert(0, "SONUC: fritoz bulunamadi\n\n");
            Show(report);
            return;
        }

        int wired = 0;

        foreach (GameObject zone in zones)
        {
            report.Append(Wire(zone, fries));
            report.AppendLine();

            if (zone.GetComponent<FryerStation>() != null)
                wired++;
        }

        report.AppendLine("Nasil kullanilir");
        report.AppendLine("  Fritoze tikla  -> yag sifirdan dolmaya baslar");
        report.AppendLine("  Yag dolunca    -> tekrar tikla, patates eline gelir");
        report.AppendLine("  Alinca yag sifirlanir, dongu bastan baslar.");
        report.AppendLine("  Icine bir sey koymak gerekmiyor, patates ortada durmaz.");
        report.AppendLine();
        report.AppendLine("Ayarlar  (her fries-zone > Fryer Station)");
        report.AppendLine("  Sure          : Fry Duration");
        report.AppendLine("  Yagin govdesi : Oil Volume   (0'dan kendi boyuna buyur)");
        report.AppendLine("  Yagin yuzeyi  : Oil Surface  (inip cikar -- yukaridan gorunen bu)");
        report.AppendLine("  Halka boyutu  : Timer Size (dunya birimi)");
        report.AppendLine("  Halkayi kaldir: Timer Root'u bosalt -- sadece yag kalir");
        report.AppendLine("  Durma noktasi : " + standPointName);
        report.AppendLine("  Ne kadar yakin: Player > Tap To Serve > Fryer Reach");
        report.AppendLine();
        report.AppendLine("Yagin dolu hali sahnedeki hali. Farkli olsun istersen");
        report.AppendLine("  " + oilName + "'un Scale'ini ve " + surfaceName + "'in Y'sini");
        report.AppendLine("  Editor'de degistir -- oyun onlari 'dolu' kabul eder.");
        report.AppendLine();
        report.AppendLine("Yuzey bos halde cok asagi/yukari kaliyorsa");
        report.AppendLine("  Fryer Station > Override Surface Empty Y'yi isaretle,");
        report.AppendLine("  altindaki sayiya istedigin yerel Y'yi yaz.");

        report.Insert(0, "SONUC: " + wired + " fritoz calisiyor, patates: " +
                         fries.GetType().Name + "\n\n");

        Show(report);
    }

    // ---- the food ----------------------------------------------------------

    // Upgraded in place rather than rebuilt. fries.prefab already exists and has
    // been sized by hand -- root at 1.7, the inner mesh at its own numbers -- and
    // rebuilding it from the fbx would throw that away. Only the missing half is
    // added: the component, and the two references it needs
    private static SpawnableFood BuildFries(StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(friesPath);

        if (root == null)
        {
            report.AppendLine(friesPath + " acilamadi");
            return null;
        }

        try
        {
            report.AppendLine("Patates prefabi: " + root.name);

            MeshFilter filter = root.GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
                filter = root.GetComponentInChildren<MeshFilter>(true);

            if (filter == null || filter.sharedMesh == null)
            {
                report.AppendLine("  icinde mesh yok");
                return null;
            }

            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

            if (renderer == null)
            {
                report.AppendLine("  mesh'in MeshRenderer'i yok");
                return null;
            }

            Fries food = root.GetComponent<Fries>();

            if (food == null)
            {
                food = root.AddComponent<Fries>();
                report.AppendLine("  Fries eklendi");
            }
            else
            {
                report.AppendLine("  Fries zaten vardi");
            }

            report.AppendLine("  olcek korundu: kok " + root.transform.localScale.ToString("0.00"));

            float height = HeightOf(root);

            SerializedObject so = new SerializedObject(food);

            so.FindProperty("filter").objectReferenceValue = filter;
            so.FindProperty("meshRenderer").objectReferenceValue = renderer;
            so.FindProperty("dirtyMesh").objectReferenceValue = filter.sharedMesh;

            // Only read when a plateau stacks more than one, which the fryer
            // never does. Filled in anyway so a tray that does holds them apart
            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;

            // Deliberately NOT ingredientOnly: a portion of fries is a meal, it
            // can go straight to a customer
            so.FindProperty("ingredientOnly").boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();

            report.AppendLine("  yigin araligi: " + height.ToString("0.0000"));
            report.AppendLine("  musteriye tek basina verilebilir");

            PrefabUtility.SaveAsPrefabAsset(root, friesPath, out bool saved);

            if (!saved)
            {
                report.AppendLine("  KAYIT BASARISIZ");
                return null;
            }

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(friesPath);

            return reloaded == null ? null : reloaded.GetComponent<SpawnableFood>();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Tallest piece, scale included. Renderer.bounds is world space and means
    // nothing on an asset that is not in a scene, so it is measured off the
    // meshes instead
    private static float HeightOf(GameObject root)
    {
        float tallest = 0f;

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            float height = filter.sharedMesh.bounds.size.y *
                           Mathf.Abs(filter.transform.lossyScale.y);

            tallest = Mathf.Max(tallest, height);
        }

        return tallest;
    }

    // ---- the stations ------------------------------------------------------

    private static GameObject[] FindZones()
    {
        System.Collections.Generic.List<GameObject> found =
            new System.Collections.Generic.List<GameObject>();

        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Strip(candidate.name).StartsWith(Strip(zonePrefix), StringComparison.OrdinalIgnoreCase))
                found.Add(candidate.gameObject);
        }

        return found.ToArray();
    }

    private static string Wire(GameObject zone, SpawnableFood fries)
    {
        string report = "Fritoz: " + zone.name + "\n";

        FryerStation station = zone.GetComponent<FryerStation>();
        bool fresh = station == null;

        if (fresh)
        {
            station = Undo.AddComponent<FryerStation>(zone);
            report += "  FryerStation eklendi\n";
        }
        else
        {
            report += "  FryerStation zaten vardi -- sayilarina dokunulmadi\n";
        }

        SerializedObject so = new SerializedObject(station);

        so.FindProperty("foodPrefab").objectReferenceValue = fries;

        Transform oil = FindInside(zone, oilName);

        so.FindProperty("oilVolume").objectReferenceValue = oil;

        report += oil == null
            ? "  " + oilName + ": BULUNAMADI\n"
            : "  " + oilName + ": bagli, dolu boyu " +
              oil.localScale.ToString("0.00") + " olarak alinacak\n";

        // The one the player can actually see. Without it the level animates
        // inside a tub nobody is looking into and reads as frozen
        Transform surface = FindInside(zone, surfaceName);

        so.FindProperty("oilSurface").objectReferenceValue = surface;

        report += surface == null
            ? "  " + surfaceName + ": BULUNAMADI -- yukaridan bakinca seviye degismiyor gibi durur\n"
            : "  " + surfaceName + ": bagli, dolu yuksekligi " +
              surface.localPosition.y.ToString("0.000") + "\n";

        if (oil == null && surface == null)
            report += "  UYARI: ikisi de yok, yag hic hareket etmez\n";

        // Left over from the version that put a portion in the basket. Reported
        // rather than deleted -- it may have been moved somewhere on purpose
        Transform strays = zone.transform.Find(readyPointName);

        if (strays != null)
            report += "  not: " + readyPointName + " artik kullanilmiyor, silebilirsin\n";

        Transform stand = EnsureChild(zone, standPointName, ref report);

        so.FindProperty("standPoint").objectReferenceValue = stand;

        GameObject timerRoot = EnsureTimer(zone, out Image fill, ref report);

        so.FindProperty("timerRoot").objectReferenceValue = timerRoot;
        so.FindProperty("timerFill").objectReferenceValue = fill;

        if (fresh)
            so.FindProperty("fryDuration").floatValue = 5f;

        report += "  sure: " + so.FindProperty("fryDuration").floatValue.ToString("0.0") + " sn\n";

        so.ApplyModifiedProperties();

        report += EnsureClickBox(zone);

        EditorSceneManager.MarkSceneDirty(zone.scene);

        return report;
    }

    private static Transform FindInside(GameObject zone, string name)
    {
        foreach (Transform candidate in zone.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != zone.transform && Same(candidate.name, name))
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

        report += "  " + name + ": eklendi -- oyuncunun duracagi yere kaydir\n";

        return child.transform;
    }

    // ---- the ring ----------------------------------------------------------

    // Same shape as the oven's: a world space canvas, a dim disc behind and a
    // bright one in front set to Filled / Radial 360, with FaceCamera turning the
    // whole thing to the camera every frame. That last part is what keeps it off
    // the floor without anyone working out the isometric rotation by hand
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

        Undo.RegisterCreatedObjectUndo(root, "Add fry timer");
        Undo.SetTransformParent(root.transform, zone.transform, "Add fry timer");

        canvas.renderMode = RenderMode.WorldSpace;

        root.AddComponent<FaceCamera>();

        RectTransform rect = (RectTransform)root.transform;

        // 100 canvas units across. The scale that turns that into a sensible
        // diameter is worked out by FryerStation from its Timer Size field -- set
        // here only so a fresh setup does not flash up a ring the size of the
        // kitchen before the first OnValidate
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
        report += "    fritozun 0.5 birim ustune kondu -- elle kaydirabilirsin\n";

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

    // ---- the tap target ----------------------------------------------------

    // The tap is a raycast and these props may carry no collider at all. Its own
    // box rather than growing whatever is there: a solid one on a cabinet is
    // load bearing for keeping the player out of it, and resizing it would open
    // a hole in the kitchen
    private static string EnsureClickBox(GameObject zone)
    {
        Transform existing = zone.transform.Find(clickBoxName);
        GameObject box = existing == null ? null : existing.gameObject;

        if (box == null)
        {
            box = new GameObject(clickBoxName);

            Undo.RegisterCreatedObjectUndo(box, "Add click box");
            Undo.SetTransformParent(box.transform, zone.transform, "Add click box");

            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = Vector3.one;
        }

        BoxCollider collider = box.GetComponent<BoxCollider>();

        if (collider == null)
            collider = Undo.AddComponent<BoxCollider>(box);

        Undo.RecordObject(collider, "Fit click box");
        Undo.RecordObject(box.transform, "Fit click box");

        // A trigger, not a solid one: the ray is cast with
        // QueryTriggerInteraction.Collide so it still lands, and a solid box
        // would wall the player away from their own fryer
        collider.isTrigger = true;

        Transform fryer = FindContaining(zone, "deepfryer");
        GameObject target = fryer == null ? zone : fryer.gameObject;

        if (!TryBounds(target, out Bounds bounds))
        {
            box.transform.localPosition = Vector3.zero;
            collider.center = Vector3.zero;
            collider.size = Vector3.one;

            return "  " + clickBoxName + ": eklendi, boyut 1x1x1 -- elle ayarla\n";
        }

        box.transform.position = bounds.center;

        Vector3 wanted = new Vector3(
            Mathf.Max(bounds.size.x, .8f),
            Mathf.Max(bounds.size.y, .8f),
            Mathf.Max(bounds.size.z, .8f));

        collider.center = Vector3.zero;
        collider.size = Divide(wanted, box.transform.lossyScale);

        return "  " + clickBoxName + ": " + target.name + " uzerine kondu, dunyada " +
               wanted.ToString("0.00") + "\n";
    }

    private static Transform FindContaining(GameObject zone, string fragment)
    {
        foreach (Transform candidate in zone.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != zone.transform &&
                candidate.name.ToLower().Contains(fragment.ToLower()))
                return candidate;
        }

        return null;
    }

    // ---- helpers -----------------------------------------------------------

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

    private static Vector3 Divide(Vector3 wanted, Vector3 parent)
    {
        return new Vector3(
            Mathf.Abs(parent.x) < .0001f ? wanted.x : wanted.x / parent.x,
            Mathf.Abs(parent.y) < .0001f ? wanted.y : wanted.y / parent.y,
            Mathf.Abs(parent.z) < .0001f ? wanted.z : wanted.z / parent.z);
    }

    private static bool Same(string left, string right)
    {
        return string.Equals(Strip(left), Strip(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Strip(string text)
    {
        return text.Replace(" ", "").Replace("-", "").Replace("_", "");
    }

    private static void Show(StringBuilder report)
    {
        Debug.Log("[Patates Kurulumu]\n" + report);
        EditorUtility.DisplayDialog("Patates Kurulumu", report.ToString(), "Tamam");
    }
}
