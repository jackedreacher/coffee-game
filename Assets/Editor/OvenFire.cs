using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Puts the fire under the oven and hands it to CookingStation.
//
// Placed rather than left to be dragged in because the two things that make it
// look right -- sitting at the floor line of the oven and being as wide as the
// oven is -- are both readable from the mesh, and neither is obvious by eye
// from a scene view looking down at an isometric kitchen
public static class OvenFire
{
    private const string firePrefabPath =
        "Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_GroundFire_Line.prefab";

    private const string fireName = "Fire";

    [MenuItem("Cooked Fast/Ocak: Ates Efektini Bagla", priority = 500)]
    public static void Attach()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz -- eklenen her sey Play durunca silinir.",
                "Tamam");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(firePrefabPath);

        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Ates",
                "Efekt bulunamadi:\n" + firePrefabPath, "Tamam");
            return;
        }

        CookingStation[] stations = Object.FindObjectsByType<CookingStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (stations.Length <= 0)
        {
            EditorUtility.DisplayDialog("Ates", "Sahnede CookingStation yok.", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        GameObject last = null;

        foreach (CookingStation station in stations)
        {
            report.AppendLine(station.name);

            SerializedObject so = new SerializedObject(station);
            SerializedProperty field = so.FindProperty("fireEffect");

            if (field.objectReferenceValue != null)
            {
                report.AppendLine("  zaten bagli: " + field.objectReferenceValue.name);

                // Still worth a pass. An effect wired by the previous version of
                // this command was left switched on, and that is exactly the
                // fire that burns for the whole game
                if (field.objectReferenceValue is GameObject bound && bound.activeSelf)
                {
                    Undo.RecordObject(bound, "Switch fire off");
                    bound.SetActive(false);

                    report.AppendLine("  aciktik, KAPATILDI -- oyun gerektiginde yakacak");

                    EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
                }

                continue;
            }

            // Re-running should replace, not stack a second fire on the first
            Transform existing = station.transform.Find(fireName);

            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            GameObject fire = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab, station.transform);

            Undo.RegisterCreatedObjectUndo(fire, "Add oven fire");

            fire.name = fireName;
            fire.transform.localRotation = Quaternion.identity;
            fire.transform.localScale = Vector3.one;

            report.Append(Place(fire.transform, station.gameObject));

            // Off in the scene. The effect plays on awake, so left switched on
            // it burns under an empty oven in every scene view and through the
            // first frames of play, before Start has had a chance to decide
            fire.SetActive(false);

            field.objectReferenceValue = fire;
            so.ApplyModifiedProperties();

            report.AppendLine("  CookingStation > Fire Effect: baglandi (kapali basliyor)");

            last = fire;

            EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
        }

        report.AppendLine();
        report.AppendLine("Ates ete atilinca yanar, son parca pisince soner.");
        report.AppendLine("Sonerken aninda kaybolmaz -- yeni kivilcim uretmeyi birakir,");
        report.AppendLine("  yananlar kendi sureleri dolunca gider.");
        report.AppendLine();
        report.AppendLine("Yeri ya da boyu tutmuyorsa: oven-zone > " + fireName);
        report.AppendLine("  Position Y ile yuksekligi, Scale X ile genisligi ayarla.");

        if (last != null)
        {
            Selection.activeGameObject = last;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        Debug.Log("[Ocak]\n" + report);
        EditorUtility.DisplayDialog("Ocak Atesi", report.ToString(), "Tamam");
    }

    private const string burnPrefabPath =
        "Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_Fire.prefab";

    private const string warningName = "Burn Warning";

    // The second half of the mechanic: the mark that says "take this now" and
    // the fire that says "too late"
    [MenuItem("Cooked Fast/Ocak: Yanma Uyarisini Kur", priority = 501)]
    public static void SetupBurnWarning()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz -- eklenen her sey Play durunca silinir.",
                "Tamam");
            return;
        }

        CookingStation[] stations = Object.FindObjectsByType<CookingStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        FryerStation[] fryers = Object.FindObjectsByType<FryerStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (stations.Length <= 0 && fryers.Length <= 0)
        {
            EditorUtility.DisplayDialog("Yanma",
                "Sahnede ne CookingStation ne FryerStation var.", "Tamam");
            return;
        }

        GameObject burnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(burnPrefabPath);

        StringBuilder report = new StringBuilder();

        GameObject last = null;

        // Measured before anything is torn down, and used for every mark this
        // run builds. Sizing each one against its own prop made the oven's
        // twice the fryer's, because an oven is a tall appliance and a fryer's
        // basket is not -- and the mark is a screen icon, not part of the prop.
        // Taking the smallest existing one means "make them all like the good
        // one" needs no number typed anywhere
        float target = SmallestMarkHeight();

        report.AppendLine(target > 0f
            ? "Isaret yuksekligi: " + target.ToString("0.00") +
              " birim (mevcut isaretten olculdu)"
            : "Isaret yuksekligi: mevcut isaret yok, prop'a gore hesaplanacak");

        report.AppendLine();

        // One routine for both. The two stations work differently inside -- the
        // oven holds pieces, the fryer holds a state -- but they carry the same
        // two fields under the same two names, so the wiring has no reason to
        // know which is which
        foreach (CookingStation station in stations)
            last = Wire(station, burnPrefab, target, report) ?? last;

        foreach (FryerStation fryer in fryers)
            last = Wire(fryer, burnPrefab, target, report) ?? last;

        report.AppendLine();
        report.AppendLine("Nasil isliyor (ocak ve fritoz ayni)");
        report.AppendLine("  pisiyor / kizariyor   -> halka doluyor");
        report.AppendLine("  hazir                 -> ikinci sayac baslar");
        report.AppendLine("  sure kadar bekledi    -> UYARI yanip sonmeye baslar");
        report.AppendLine("  2 saniye daha         -> yanar, uzerinde ates cikar");
        report.AppendLine("  yanigi alirsan        -> ates sonda kalir, yemek kara gelir");
        report.AppendLine();
        report.AppendLine("Yanik yemek hicbir yere kabul edilmez: musteriye verilemez,");
        report.AppendLine("  burgere girmez, teslim alanina ve rafa konmaz. Sadece cop.");
        report.AppendLine();
        report.AppendLine("Ayarlar: istasyon > Yanma");
        report.AppendLine("  Burn Grace          0 = pisirme/kizartma suresi kadar");
        report.AppendLine("  Burn Warning Time   uyaridan yanmaya kac saniye (2)");
        report.AppendLine("  Warning Blinks Per Second  yanip sonme hizi (4)");

        if (last != null)
        {
            Selection.activeGameObject = last;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        Debug.Log("[Ocak]\n" + report);
        EditorUtility.DisplayDialog("Yanma Uyarisi", report.ToString(), "Tamam");
    }

    // The smallest mark already in the scene, in world units. Smallest rather
    // than first because the one that was too big is exactly the one being
    // replaced
    private static float SmallestMarkHeight()
    {
        float smallest = 0f;

        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer.transform.parent == null ||
                renderer.transform.parent.name != warningName)
                continue;

            float height = renderer.bounds.size.y;

            if (height <= .0001f)
                continue;

            if (smallest <= 0f || height < smallest)
                smallest = height;
        }

        return smallest;
    }

    private static GameObject Wire(Component station, GameObject burnPrefab, float targetHeight,
        StringBuilder report)
    {
        report.AppendLine(station.name);

        SerializedObject so = new SerializedObject(station);

        SerializedProperty burn = so.FindProperty("burnEffect");

        if (burn.objectReferenceValue != null)
            report.AppendLine("  Burn Effect: zaten bagli (" +
                              burn.objectReferenceValue.name + ")");
        else if (burnPrefab == null)
            report.AppendLine("  Burn Effect: EFEKT YOK -- " + burnPrefabPath);
        else
        {
            burn.objectReferenceValue = burnPrefab;
            report.AppendLine("  Burn Effect: VFX_Fire baglandi");
        }

        SerializedProperty warning = so.FindProperty("warningRoot");

        GameObject bound = warning.objectReferenceValue as GameObject;

        // Ours is rebuilt, a hand-made one is left alone. The first version of
        // this built a mark that drew nothing, and skipping on "already wired"
        // would have kept it forever -- the field was filled, which is exactly
        // what made the failure invisible
        if (bound != null && bound.name == warningName)
        {
            Undo.DestroyObjectImmediate(bound);
            bound = null;

            report.AppendLine("  Warning Root: eski isaret silindi, yeniden kuruluyor");
        }

        GameObject mark = null;

        if (bound != null)
        {
            report.AppendLine("  Warning Root: elle atanmis (" + bound.name + "), korundu");
        }
        else
        {
            mark = BuildWarning(station.gameObject, targetHeight, report);

            if (mark != null)
                warning.objectReferenceValue = mark;
        }

        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(station.gameObject.scene);

        return mark;
    }

    // The ring is placed by hand and ends up near the pan rather than on it,
    // because the pan is a small thing inside a big prop and the difference is
    // hard to judge from a scene view looking down at the kitchen
    [MenuItem("Cooked Fast/Ocak: Sayaci Ortala", priority = 502)]
    public static void CentreTimers()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz -- tasinan her sey Play durunca geri doner.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        int moved = 0;

        foreach (CookingStation station in Object.FindObjectsByType<CookingStation>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            report.AppendLine(station.name);

            SerializedObject so = new SerializedObject(station);

            // The pan, not the whole zone. Zone bounds take in the cabinet, the
            // stand point and the canvas, and their centre is off in the counter
            if (!TryNamedBounds(station.gameObject, "pan", out Bounds target))
            {
                report.AppendLine("  \"pan\" adinda gorunur bir parca yok, atlandi");
                continue;
            }

            moved += Centre(so.FindProperty("timerRoot").objectReferenceValue as GameObject,
                target, "Timer Root", timerLeftShift, report);

            // Dead centre. The mark is a shout, not a readout -- it wants to be
            // over the middle of the thing that is about to be ruined
            moved += Centre(so.FindProperty("warningRoot").objectReferenceValue as GameObject,
                target, "Warning Root", 0f, report);

            EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
        }

        report.AppendLine();
        report.AppendLine(moved + " obje ortalandi.");
        report.AppendLine("Sadece X ve Z tasindi -- yukseklik elle ayarlandigi gibi kaldi.");

        Debug.Log("[Ocak]\n" + report);
        EditorUtility.DisplayDialog("Sayac Ortalama", report.ToString(), "Tamam");
    }

    // How far left of the pan's centre the ring sits, as a fraction of the pan's
    // width. Nudged rather than centred because dead centre puts the ring over
    // the food it is timing
    private const float timerLeftShift = .35f;

    // X and Z only. The height was chosen by eye against the prop and is the
    // one part of the placement that was already right
    private static int Centre(GameObject target, Bounds over, string label, float shift,
        StringBuilder report)
    {
        if (target == null)
        {
            report.AppendLine("  " + label + ": bos, atlandi");
            return 0;
        }

        Vector3 where = target.transform.position;

        Undo.RecordObject(target.transform, "Centre over pan");

        Vector3 centre = new Vector3(over.center.x, where.y, over.center.z);

        target.transform.position = centre + Left() * (over.size.x * shift);

        report.AppendLine("  " + label + ": " + where.ToString("0.00") + " -> " +
                          target.transform.position.ToString("0.00"));

        return 1;
    }

    // Screen left, not world left.
    //
    // The camera is isometric, so which world axis points left on screen is a
    // question about the camera's rotation and not something that can be
    // written down as -X. Taken from the camera and flattened, so the nudge
    // stays on the floor plane
    private static Vector3 Left()
    {
        Camera camera = Camera.main;

        if (camera == null)
            return Vector3.left;

        Vector3 right = camera.transform.right;

        right.y = 0f;

        return right.sqrMagnitude < .0001f ? Vector3.left : -right.normalized;
    }

    private static bool TryNamedBounds(GameObject station, string wanted, out Bounds bounds)
    {
        bounds = default;

        bool any = false;

        foreach (Renderer renderer in station.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || !renderer.name.ToLower().Contains(wanted))
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

    // Real geometry above the oven, not UI.
    //
    // The canvas versions of this were invisible twice, and each time for a
    // different reason: a Text whose font did not resolve, then a world space
    // canvas whose scale and draw order are decided by things placed by hand
    // elsewhere. None of it could be checked without running the game. Two
    // cubes and an unlit material are visible in the scene view the moment they
    // exist, which is the whole point of switching
    private static GameObject BuildWarning(GameObject station, float targetHeight,
        StringBuilder report)
    {
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

        // Only the cube fallback needs a material, so a missing shader is not a
        // reason to refuse when the icon is right there
        Material material = icon != null ? null : WarningMaterial();

        if (icon == null && material == null)
        {
            report.AppendLine("  Warning Root: KURULAMADI -- ne ikon ne unlit shader var");
            return null;
        }

        if (!TryOvenBounds(station, out Bounds bounds))
        {
            report.AppendLine("  Warning Root: KURULAMADI -- istasyonun mesh'i bulunamadi");
            return null;
        }

        GameObject host = new GameObject(warningName);

        Undo.RegisterCreatedObjectUndo(host, "Add burn warning");
        Undo.SetTransformParent(host.transform, station.transform, "Add burn warning");

        // The measured one when there is one, so every station ends up matching.
        // The prop-relative rule is only the opening guess for the first mark
        float height = targetHeight > .0001f ? targetHeight : Mathf.Max(.2f, bounds.size.y * .55f);
        float width = height * .22f;

        host.transform.position = new Vector3(
            bounds.center.x,
            bounds.max.y + height * .75f,
            bounds.center.z);

        host.transform.localRotation = Quaternion.identity;
        host.transform.localScale = Vector3.one;

        if (icon != null)
            AddIcon(host, icon, height, report);
        else
        {
            report.AppendLine("  ikon bulunamadi, iki kupten kuruluyor:");
            report.AppendLine("    " + iconPath);

            AddBlock(host, material, "Bar",
                new Vector3(width, height * .6f, width),
                new Vector3(0f, height * .28f, 0f));

            AddBlock(host, material, "Dot",
                new Vector3(width, width, width),
                new Vector3(0f, -height * .22f, 0f));
        }

        host.SetActive(false);

        report.AppendLine("  Warning Root: kuruldu ve baglandi (kapali)");
        report.AppendLine("    yer " + host.transform.position.ToString("0.00") +
                          ", yukseklik " + height.ToString("0.00") + " birim");

        return host;
    }

    private const string iconPath =
        "Assets/Layer Lab/2D Icons-PictoIconPack01/Icons/PictoIcon_64/" +
        "Icon_PictoIcon_Mark_Caution-1.Png";

    // A SpriteRenderer, not a Canvas.
    //
    // The same picture through a world space canvas needs a render mode, a
    // scale, a draw order and a rect, and every one of those is a way for it to
    // come out invisible -- which it did, twice. A sprite in the world is a
    // quad with a texture on it: it shows up in the scene view the moment it
    // exists and is sorted against the kitchen like any other object
    private static void AddIcon(GameObject host, Sprite icon, float height, StringBuilder report)
    {
        GameObject piece = new GameObject("Icon");

        Undo.RegisterCreatedObjectUndo(piece, "Add burn warning");
        Undo.SetTransformParent(piece.transform, host.transform, "Add burn warning");

        piece.transform.localPosition = Vector3.zero;
        piece.transform.localRotation = Quaternion.identity;

        SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();

        renderer.sprite = icon;

        // In front of the kitchen it floats over. The oven is opaque geometry
        // and a sprite at the same depth would flicker against it
        renderer.sortingOrder = 100;

        // The sprite's own world size comes from its pixel-to-unit setting,
        // which is about the atlas it was cut from and nothing to do with this
        // oven. Scaled to the height asked for instead
        float own = renderer.bounds.size.y;

        piece.transform.localScale = own > .0001f
            ? Vector3.one * (height / own)
            : Vector3.one;

        // Turned to the camera every frame. The kitchen camera is isometric, so
        // a sprite left facing world forward is seen edge on from one side
        piece.AddComponent<FaceCamera>();

        report.AppendLine("  ikon: " + icon.name);
        report.AppendLine("    " + (own > .0001f ? (height / own).ToString("0.00") : "1.00") +
                          " kat olceklendi, FaceCamera eklendi");
    }

    private static void AddBlock(GameObject host, Material material, string name,
        Vector3 size, Vector3 offset)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Undo.RegisterCreatedObjectUndo(block, "Add burn warning");

        block.name = name;

        // A primitive arrives with a collider, and this one sits right where the
        // player taps to use the oven. Left on, it eats the tap
        Collider collider = block.GetComponent<Collider>();

        if (collider != null)
            Object.DestroyImmediate(collider);

        block.GetComponent<Renderer>().sharedMaterial = material;

        Undo.SetTransformParent(block.transform, host.transform, "Add burn warning");

        block.transform.localPosition = offset;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = size;
    }

    // Saved as an asset rather than created in the scene, so the two cubes and
    // every oven added later share one material and one colour to change
    private static Material WarningMaterial()
    {
        const string folder = "Assets/Tiny Coffee Shop/Materials";
        const string path = folder + "/Burn Warning.mat";

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (existing != null)
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material material = new Material(shader);

        Color colour = new Color(1f, .18f, .06f);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", colour);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", colour);

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Tiny Coffee Shop", "Materials");

        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();

        return material;
    }


    // The floor line of the oven, as wide as the oven. The line effect ships at
    // whatever length its author chose, which has nothing to do with this oven
    private static string Place(Transform fire, GameObject station)
    {
        if (!TryOvenBounds(station, out Bounds bounds))
        {
            fire.localPosition = Vector3.zero;

            return "  UYARI: ocagin mesh'i bulunamadi, ates merkeze kondu -- elle tasi\n";
        }

        fire.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        // Scale is applied in the fire's own space, and its parent may carry a
        // scale of its own, so the width has to be divided back out
        float parent = Mathf.Abs(fire.lossyScale.x / Mathf.Max(.0001f, fire.localScale.x));

        if (parent > .0001f)
            fire.localScale = Vector3.one * (bounds.size.x / parent);

        return "  yer: ocagin taban hizasi, genislik " +
               bounds.size.x.ToString("0.00") + "\n";
    }

    // The oven prop, not the whole zone. Zone bounds take in the stand point
    // marker, the timer canvas and whatever else was parented there, and a fire
    // stretched across all of that reaches out into the walkway
    // Names the prop itself might carry, most specific first. A fryer has no
    // child called "oven", so the list is what makes one routine serve both
    private static readonly string[] propNames = { "oven", "fryer", "fries", "basket", "oil" };

    private static bool TryOvenBounds(GameObject station, out Bounds bounds)
    {
        bounds = default;

        Transform prop = null;

        foreach (string wanted in propNames)
        {
            foreach (Transform child in station.GetComponentsInChildren<Transform>(true))
            {
                if (child == station.transform || !child.name.ToLower().Contains(wanted))
                    continue;

                prop = child;
                break;
            }

            if (prop != null)
                break;
        }

        Transform root = prop != null ? prop : station.transform;

        bool any = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
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
}
