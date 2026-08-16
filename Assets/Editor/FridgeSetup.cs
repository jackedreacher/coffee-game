using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Wires the fridge door and gives it a way to be dialled in.
//
// The angle is the whole job here. A hinge on the wrong side or a sign the
// wrong way round swings the door straight through the fridge, and that is not
// something anyone can read off a number -- so there are preview commands that
// swing it in the editor, with the same maths play mode uses
public static class FridgeSetup
{
    private const string zoneName = "fridge-zone";
    private const string fridgeName = "fridge";
    private const string standPointName = "Stand Point";

    private const string drinkPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/drink.prefab";
    private const string drinkModelPath = "Assets/Tiny Coffee Shop/FBX-food/soda-can.fbx";

    // Sized against a food already in the game rather than against a number
    // picked here. Fries were sized by hand, so matching them is matching a
    // decision that has already been looked at on screen
    private const string sizeReferencePath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/fries.prefab";

    [MenuItem("Cooked Fast/Buzdolabi: 1 - Kapiyi Kur", priority = 180)]
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

        GameObject fridge = ResolveFridge(out string note);

        if (fridge == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Adinda 'door' gecen bir cocuk objesi olan dolap bulunamadi.\n\n" +
                "fridge.prefab'i sahneye koy, sonra Hierarchy'den sec ve\n" +
                "komutu tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Dolap: " + fridge.name + note);
        report.AppendLine("  layer: " + LayerMask.LayerToName(fridge.layer));
        report.AppendLine();

        FridgeDoor component = fridge.GetComponent<FridgeDoor>();
        bool fresh = component == null;

        if (fresh)
        {
            component = Undo.AddComponent<FridgeDoor>(fridge);
            report.AppendLine("  FridgeDoor eklendi");
        }
        else
        {
            report.AppendLine("  FridgeDoor zaten vardi -- ayarlarina dokunulmadi");
        }

        Transform door = component.FindDoor();

        // Built BEFORE the SerializedObject is opened, and that ordering is the
        // whole fix. SaveAsPrefabAsset runs an asset import, an import can
        // refresh serialization underneath an open SerializedObject, and
        // ApplyModifiedProperties on a stale one throws nothing and writes
        // nothing -- which is how the drink field stayed empty on a run whose
        // report said everything had gone fine
        StringBuilder drinkReport = new StringBuilder();
        SpawnableFood drink = BuildDrink(drinkReport);

        SerializedObject so = new SerializedObject(component);

        so.FindProperty("door").objectReferenceValue = door;

        report.AppendLine("  kapi: " + (door == null ? "BULUNAMADI" : door.name));

        // Captured every run rather than only on the first: the door's closed
        // pose is whatever it is sitting at now, and re-running after nudging
        // the fridge is exactly how someone would expect to re-record it. The
        // preview commands close it first for the same reason
        if (door != null)
        {
            so.FindProperty("closedPosition").vector3Value = door.localPosition;
            so.FindProperty("closedEuler").vector3Value = door.localRotation.eulerAngles;
            so.FindProperty("captured").boolValue = true;

            report.AppendLine("  kapali hali kaydedildi: " +
                              door.localPosition.ToString("0.00") + "  " +
                              door.localRotation.eulerAngles.ToString("0"));
        }

        Transform stand = EnsureStandPoint(fridge, report);

        so.FindProperty("standPoint").objectReferenceValue = stand;

        report.AppendLine();
        report.Append(drinkReport);

        so.FindProperty("drinkPrefab").objectReferenceValue = drink;

        so.ApplyModifiedProperties();

        // Read back off the component rather than trusting the write. Every
        // other setup here ends with a SONUC line for the same reason: a report
        // that only repeats what it meant to do cannot catch a write that did
        // not land
        SpawnableFood wired = new SerializedObject(component)
            .FindProperty("drinkPrefab").objectReferenceValue as SpawnableFood;

        report.AppendLine(wired == null
            ? "  icecek: BAGLANAMADI -- 'Buzdolabi: 4 - Icecegi Bagla' calistir"
            : "  icecek: " + wired.name + " bagli (dogrulandi)");

        report.Append(CheckCollider(fridge));

        EditorSceneManager.MarkSceneDirty(fridge.scene);
        Selection.activeGameObject = fridge;

        report.AppendLine();
        report.AppendLine("Nasil kullanilir");
        report.AppendLine("  Dolaba tikla -> kapi acilir VE icecek eline gelir.");
        report.AppendLine("  Beklemek yok, tek tik. Kapi " +
                          so.FindProperty("autoCloseAfter").floatValue.ToString("0.0") +
                          " sn sonra kendi kapanir.");
        report.AppendLine("  El doluysa sadece kapi acilir, icecek verilmez.");
        report.AppendLine();
        report.AppendLine("Kapi ters yone aciliyorsa");
        report.AppendLine("  'Buzdolabi: 2 - Kapiyi Ac' ile bak, Open Angle'in");
        report.AppendLine("  basindaki eksiyi kaldir ya da koy, tekrar bak.");
        report.AppendLine();
        report.AppendLine("Ayarlar  (" + fridge.name + " > Fridge Door)");
        report.AppendLine("  Acilma acisi   : Open Angle       (su an " +
                          so.FindProperty("openAngle").floatValue.ToString("0") + ")");
        report.AppendLine("  Acilma hizi    : Speed            (derece/sn)");
        report.AppendLine("  Kendi kapanma  : Auto Close After (0 = kapanmaz)");
        report.AppendLine("  Mentese ekseni : Hinge Axis       (yukari = yana acilir)");
        report.AppendLine("  Mentese yeri   : Hinge Offset     (pivot mentesede degilse)");
        report.AppendLine("  Uzaktan acilir : Open From Anywhere");
        report.AppendLine("  Cikan icecek   : Drink Prefab");
        report.AppendLine("  Icecegin boyu  : drink.prefab > Renderer > Scale");

        report.Insert(0, door == null
            ? "SONUC: kapi objesi bulunamadi, dolap tiklaninca bir sey olmaz\n\n"
            : "SONUC: " + fridge.name + " tiklaninca " + door.name +
              " aciliyor" + (drink == null ? "" : " ve " + drink.name + " veriyor") + "\n\n");

        Show(report, "Buzdolabi Kurulumu");
    }

    // ---- linking the drink on its own --------------------------------------

    // Nothing is built, nothing is measured, no prefab is written -- so there is
    // no asset import to knock the SerializedObject out from under the write.
    // Every FridgeDoor in the scene is listed by name whether it needed changing
    // or not, which is also how a second FridgeDoor hiding on another object
    // would show itself
    [MenuItem("Cooked Fast/Buzdolabi: 4 - Icecegi Bagla", priority = 183)]
    public static void LinkDrink()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da yapilan baglantilar Play durunca silinir.\n" +
                "Once Play'i durdur.", "Tamam");
            return;
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(drinkPath);

        if (asset == null)
        {
            EditorUtility.DisplayDialog("Hata",
                drinkPath + " yok.\n\n" +
                "Once 'Buzdolabi: 1 - Kapiyi Kur' calistir.", "Tamam");
            return;
        }

        SpawnableFood drink = asset.GetComponent<SpawnableFood>();

        if (drink == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "drink.prefab icinde SpawnableFood yok.\n\n" +
                "Derleme bitmemis olabilir. Console'da hata varsa once onu duzelt.",
                "Tamam");
            return;
        }

        FridgeDoor[] doors = UnityEngine.Object.FindObjectsByType<FridgeDoor>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        StringBuilder report = new StringBuilder();

        report.AppendLine("Icecek: " + drink.name + "  (" + drink.GetType().Name + ")");
        report.AppendLine("  " + drinkPath);
        report.AppendLine();

        if (doors.Length <= 0)
        {
            report.AppendLine("Sahnede FridgeDoor yok. Once 'Buzdolabi: 1' calistir.");
            Show(report, "Icecek Baglama");
            return;
        }

        report.AppendLine("Sahnedeki dolaplar: " + doors.Length);

        int ok = 0;

        foreach (FridgeDoor found in doors)
        {
            SerializedObject so = new SerializedObject(found);

            so.FindProperty("drinkPrefab").objectReferenceValue = drink;
            so.ApplyModifiedProperties();

            SpawnableFood back = new SerializedObject(found)
                .FindProperty("drinkPrefab").objectReferenceValue as SpawnableFood;

            report.AppendLine("  " + FullPath(found.transform) + ": " +
                              (back == null ? "YAZILAMADI" : "bagli -> " + back.name));

            if (back != null)
                ok++;

            EditorSceneManager.MarkSceneDirty(found.gameObject.scene);
        }

        report.Insert(0, ok == doors.Length
            ? "SONUC: " + ok + " dolap icecek veriyor\n\n"
            : "SONUC: " + ok + "/" + doors.Length + " dolap baglandi\n\n");

        if (doors.Length > 1)
        {
            report.AppendLine();
            report.AppendLine("Birden fazla FridgeDoor var. Tiklama, carpilan collider'in");
            report.AppendLine("  en yakin ustundekini bulur -- fazlaligi silmek isteyebilirsin.");
        }

        Show(report, "Icecek Baglama");
    }

    private static string FullPath(Transform target)
    {
        string path = target.name;

        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    // ---- the drink ---------------------------------------------------------

    // Built once and then left alone. Re-running the setup after the can has
    // been resized by hand must not put the calculated number back
    private static SpawnableFood BuildDrink(StringBuilder report)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(drinkPath);

        if (existing != null && existing.GetComponent<Drink>() != null)
        {
            report.AppendLine("  drink.prefab zaten var, dokunulmadi");

            return existing.GetComponent<SpawnableFood>();
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(drinkModelPath);
        MeshFilter source = model == null ? null : model.GetComponentInChildren<MeshFilter>(true);

        if (source == null || source.sharedMesh == null)
        {
            report.AppendLine("  " + drinkModelPath + " bulunamadi");
            return null;
        }

        float fit = FitAgainstReference(source.sharedMesh, report);

        GameObject built = new GameObject("drink");

        try
        {
            GameObject visual = new GameObject("Renderer");

            visual.transform.SetParent(built.transform, false);

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();

            filter.sharedMesh = source.sharedMesh;

            // Copied, never left to AddComponent's default: that hands back a
            // one-entry array whose entry is null, and URP draws null magenta
            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();

            if (sourceRenderer != null && sourceRenderer.sharedMaterials.Length > 0 &&
                sourceRenderer.sharedMaterials[0] != null)
            {
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                report.AppendLine("  materyal: " + sourceRenderer.sharedMaterials[0].name);
            }
            else
            {
                report.AppendLine("  UYARI: materyal yok, kutu PEMBE gorunur");
            }

            // Course rule: model node at zero, no rotation, size the only
            // difference. FoodPosition.Push zeroes the first two on every pickup
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * fit;

            Drink drink = built.AddComponent<Drink>();

            float height = source.sharedMesh.bounds.size.y * fit;

            SerializedObject so = new SerializedObject(drink);

            so.FindProperty("filter").objectReferenceValue = filter;
            so.FindProperty("meshRenderer").objectReferenceValue = renderer;
            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyMesh").objectReferenceValue = source.sharedMesh;

            // Deliberately not ingredientOnly: a can is a meal on its own and
            // goes straight to a customer
            so.FindProperty("ingredientOnly").boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(built, drinkPath, out bool saved);

            if (!saved)
            {
                report.AppendLine("  KAYIT BASARISIZ: " + drinkPath);
                return null;
            }

            report.AppendLine("  drink.prefab olusturuldu");
            report.AppendLine("    " + drinkPath);
            report.AppendLine("    mesh: " + source.sharedMesh.name + "  (soda-can.fbx)");
            report.AppendLine("    olcek " + fit.ToString("0.0000"));
            report.AppendLine("    musteriye tek basina verilebilir");

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(drinkPath);

            return reloaded == null ? null : reloaded.GetComponent<SpawnableFood>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(built);
        }
    }

    // Height against height. A can and a portion of chips are both things that
    // stand up on a tray, so making them the same height puts the can in the
    // right world straight away
    private static float FitAgainstReference(Mesh mesh, StringBuilder report)
    {
        GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>(sizeReferencePath);

        float have = mesh.bounds.size.y;

        if (reference == null || have <= .0001f)
        {
            report.AppendLine("  olcek: olcu alinamadi, 1 birakildi");
            return 1f;
        }

        float wanted = 0f;

        foreach (MeshFilter filter in reference.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            wanted = Mathf.Max(wanted,
                filter.sharedMesh.bounds.size.y * Mathf.Abs(filter.transform.lossyScale.y));
        }

        if (wanted <= .0001f)
        {
            report.AppendLine("  olcek: " + reference.name + " olculemedi, 1 birakildi");
            return 1f;
        }

        report.AppendLine("  olcek: " + reference.name + " yuksekligine esitlendi (" +
                          wanted.ToString("0.000") + " birim)");

        return wanted / have;
    }

    // ---- preview -----------------------------------------------------------

    [MenuItem("Cooked Fast/Buzdolabi: 2 - Kapiyi Ac (onizleme)", priority = 181)]
    public static void PreviewOpen()
    {
        Preview(true);
    }

    [MenuItem("Cooked Fast/Buzdolabi: 3 - Kapiyi Kapat", priority = 182)]
    public static void PreviewClose()
    {
        Preview(false);
    }

    private static void Preview(bool open)
    {
        FridgeDoor component = ResolveComponent();

        if (component == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "FridgeDoor bulunamadi.\n\n" +
                "Once 'Buzdolabi: 1 - Kapiyi Kur' calistir.", "Tamam");
            return;
        }

        Transform door = component.Door == null ? component.FindDoor() : component.Door;

        if (door == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Kapi objesi bulunamadi. Adinda 'door' gecen bir cocuk yok.", "Tamam");
            return;
        }

        // Without a recorded closed pose the maths swings from zero, which drops
        // the door at the fridge's own origin -- usually on the floor
        if (!component.Captured)
        {
            Undo.RecordObject(component, "Capture closed door");
            component.CaptureClosed();
        }

        Undo.RecordObject(door, open ? "Preview door open" : "Preview door closed");

        component.PreviewAt(open ? component.OpenAngle : 0f);

        EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        Selection.activeGameObject = component.gameObject;

        Debug.Log("[Buzdolabi] kapi " + (open ? "acildi" : "kapandi") +
                  "  (" + (open ? component.OpenAngle.ToString("0") : "0") + " derece)\n" +
                  (open
                      ? "  Yanlis yone gittiyse: Fridge Door > Open Angle isaretini degistir,\n" +
                        "  sonra bu komutu tekrar calistir.\n" +
                        "  Kapi kayiyorsa: Hinge Offset ile mentese yerini kaydir.\n" +
                        "  Isini bitirince '3 - Kapiyi Kapat' calistir -- sahne oyle kaydedilsin."
                      : "  Kapali hali kayitli olan konum. Play'de de buradan baslar."));
    }

    // ---- finding things ----------------------------------------------------

    private static FridgeDoor ResolveComponent()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected != null && selected.scene.IsValid())
        {
            FridgeDoor onSelection = selected.GetComponentInParent<FridgeDoor>();

            if (onSelection != null)
                return onSelection;
        }

        return UnityEngine.Object.FindFirstObjectByType<FridgeDoor>(FindObjectsInactive.Include);
    }

    // The component has to sit on something the door mesh hangs underneath, or
    // the tap ray lands on a collider whose parents know nothing about a door.
    // That rules the hand made zone out unless the fridge is actually inside it
    private static GameObject ResolveFridge(out string note)
    {
        GameObject selected = Selection.activeGameObject;

        if (selected != null && selected.scene.IsValid() && HasDoor(selected))
        {
            note = "";
            return selected;
        }

        GameObject zone = FindInScene(zoneName);

        if (zone != null && HasDoor(zone))
        {
            note = "  (" + zoneName + " icinden bulundu)";
            return zone;
        }

        Transform door = FindDoorInScene();

        if (door == null)
        {
            note = "";
            return null;
        }

        GameObject root = door.parent == null ? door.gameObject : door.parent.gameObject;

        note = zone == null
            ? "  (" + zoneName + " sahnede yok, kapinin bagli oldugu obje kullanildi)"
            : "  <-- DIKKAT: " + zoneName + " kapiyi icermiyor, kullanilmadi";

        return root;
    }

    private static bool HasDoor(GameObject target)
    {
        foreach (Transform candidate in target.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != target.transform && IsDoor(candidate.name))
                return true;
        }

        return false;
    }

    private static bool IsDoor(string name)
    {
        return name.ToLower().Contains("door");
    }

    private static Transform FindDoorInScene()
    {
        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (IsDoor(candidate.name))
                return candidate;
        }

        return null;
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

    // ---- the tap target ----------------------------------------------------

    // fridge.prefab ships with a Blocker box that already covers the whole
    // fridge, so there is usually nothing to do here. Reported rather than
    // silently assumed, because a fridge with no collider is a fridge that
    // cannot be tapped and nothing on screen says so
    private static string CheckCollider(GameObject fridge)
    {
        Collider[] colliders = fridge.GetComponentsInChildren<Collider>(true);

        if (colliders.Length > 0)
        {
            string names = "";

            foreach (Collider collider in colliders)
                names += (names.Length > 0 ? ", " : "") + collider.gameObject.name;

            return "  tiklama alani: var (" + names + ")\n";
        }

        BoxCollider box = Undo.AddComponent<BoxCollider>(fridge);

        box.isTrigger = true;

        if (TryBounds(fridge, out Bounds bounds))
        {
            box.center = fridge.transform.InverseTransformPoint(bounds.center);
            box.size = Divide(bounds.size, fridge.transform.lossyScale);

            return "  tiklama alani: collider yoktu, dolabin boyutunda trigger eklendi\n";
        }

        box.size = Vector3.one * 2f;

        return "  tiklama alani: collider yoktu, 2x2x2 trigger eklendi -- elle ayarla\n";
    }

    private static Transform EnsureStandPoint(GameObject fridge, StringBuilder report)
    {
        Transform existing = fridge.transform.Find(standPointName);

        if (existing != null)
        {
            report.AppendLine("  " + standPointName + ": zaten var");
            return existing;
        }

        GameObject point = new GameObject(standPointName);

        Undo.RegisterCreatedObjectUndo(point, "Add stand point");
        Undo.SetTransformParent(point.transform, fridge.transform, "Add stand point");

        point.transform.localPosition = Vector3.zero;
        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;

        report.AppendLine("  " + standPointName +
                          ": eklendi (sadece Open From Anywhere kapatilirsa kullanilir)");

        return point.transform;
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

    private static Vector3 Divide(Vector3 wanted, Vector3 parent)
    {
        return new Vector3(
            Mathf.Abs(parent.x) < .0001f ? wanted.x : wanted.x / parent.x,
            Mathf.Abs(parent.y) < .0001f ? wanted.y : wanted.y / parent.y,
            Mathf.Abs(parent.z) < .0001f ? wanted.z : wanted.z / parent.z);
    }

    private static void Show(StringBuilder report, string title)
    {
        Debug.Log("[" + title + "]\n" + report);
        EditorUtility.DisplayDialog(title, report.ToString(), "Tamam");
    }
}
