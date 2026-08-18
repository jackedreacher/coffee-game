using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Turns whatever is selected into a place to put food down. Selection driven on
// purpose: "Drop Zone" is already the name of the counters the serving stations
// own, and guessing by name would wire the wrong one
public static class ShelfSetup
{
    private const string plateauPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Plateau.prefab";
    private const string standPointName = "Stand Point";

    [MenuItem("Cooked Fast/Istasyon/Malzeme Rafi: Secili Objeye Kur", priority = 170)]
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

        GameObject zone = Selection.activeGameObject;

        if (zone == null || !zone.scene.IsValid())
        {
            EditorUtility.DisplayDialog("Hata",
                "Hierarchy'den raf olacak objeyi sec, sonra komutu calistir.\n\n" +
                "Ekrandaki tabakli yer neyse o.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Raf: " + zone.name);
        report.AppendLine("  layer: " + LayerMask.LayerToName(zone.layer));
        report.AppendLine();

        HoldingShelf shelf = zone.GetComponent<HoldingShelf>();

        if (shelf == null)
        {
            shelf = Undo.AddComponent<HoldingShelf>(zone);
            report.AppendLine("  HoldingShelf eklendi");
        }
        else
        {
            report.AppendLine("  HoldingShelf zaten vardi");
        }

        SerializedObject so = new SerializedObject(shelf);

        Plateau plateau = EnsurePlateau(zone, report);

        so.FindProperty("plateau").objectReferenceValue = plateau;

        Transform stand = EnsureStandPoint(zone, report);

        so.FindProperty("standPoint").objectReferenceValue = stand;
        so.ApplyModifiedProperties();

        report.Append(EnsureClickBox(zone, plateau));

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = zone;

        report.AppendLine();
        report.AppendLine("Nasil kullanilir");
        report.AppendLine("  Rafa tikla. Elin doluysa birakir, bossa geri alir.");
        report.AppendLine("  Yarim burgeri buraya birak, eti pisir, gel al.");
        report.AppendLine();
        report.AppendLine("Ayarlar");
        report.AppendLine("  Tabagin yeri  : " + zone.name + " > Plateau (Transform)");
        report.AppendLine("  Kac sey dursun: ayni yer > Plateau > Max Capacity");
        report.AppendLine("  Durma noktasi : " + zone.name + " > " + standPointName);
        report.AppendLine("  Ne kadar yakin: Player > Tap To Serve > Shelf Reach");

        report.Insert(0, plateau == null
            ? "SONUC: tepsi kurulamadi, raf calismaz\n\n"
            : "SONUC: " + zone.name + " artik yemek birakilip alinabilen bir raf\n\n");

        Debug.Log("[Malzeme Rafi]\n" + report);
        EditorUtility.DisplayDialog("Malzeme Rafi", report.ToString(), "Tamam");
    }

    // The shared Plateau prefab, plate visual included -- the same tray the
    // stations use, so a parked burger sits on a plate like everywhere else
    private static Plateau EnsurePlateau(GameObject zone, StringBuilder report)
    {
        Plateau existing = zone.GetComponentInChildren<Plateau>(true);

        if (existing != null)
        {
            report.AppendLine("  plateau: zaten var (" + existing.name +
                              "), Max Capacity " + Capacity(existing));

            return existing;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plateauPrefabPath);

        if (prefab == null)
        {
            report.AppendLine("  plateau: Plateau.prefab bulunamadi");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, zone.transform);

        Undo.RegisterCreatedObjectUndo(instance, "Add shelf plateau");

        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localPosition = Vector3.zero;

        if (TryBounds(zone, out Bounds bounds))
        {
            instance.transform.position =
                new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            report.AppendLine("  plateau: eklendi, rafin ustune oturtuldu");
        }
        else
        {
            report.AppendLine("  plateau: eklendi, konum sifir -- elle yerlestir");
        }

        Plateau added = instance.GetComponent<Plateau>();

        // One thing at a time. A plateau only ever holds a single food type
        // anyway, so a bigger number buys nothing and hides what is parked
        SerializedObject so = new SerializedObject(added);

        so.FindProperty("maxCapacity").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine("  Max Capacity 1 -- ayni anda tek sey durur");

        return added;
    }

    private static int Capacity(Plateau plateau)
    {
        return new SerializedObject(plateau).FindProperty("maxCapacity").intValue;
    }

    private static Transform EnsureStandPoint(GameObject zone, StringBuilder report)
    {
        Transform existing = zone.transform.Find(standPointName);

        if (existing != null)
        {
            report.AppendLine("  " + standPointName + ": zaten var");
            return existing;
        }

        GameObject point = new GameObject(standPointName);

        Undo.RegisterCreatedObjectUndo(point, "Add stand point");
        Undo.SetTransformParent(point.transform, zone.transform, "Add stand point");

        point.transform.localPosition = Vector3.zero;
        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;

        report.AppendLine("  " + standPointName + ": eklendi -- oyuncunun duracagi yere kaydir");

        return point.transform;
    }

    private const string clickBoxName = "Click Box";

    // The tap is a raycast, and a plate has no collider -- the ray goes straight
    // through it into the counter behind, which is a different object and knows
    // nothing about any shelf. Whatever collider the shelf already had is left
    // alone (it may be load bearing for something else) and a box of its own is
    // put over the plate instead.
    //
    // A trigger rather than a solid one: the ray is cast with
    // QueryTriggerInteraction.Collide so it still lands, and a solid box on a
    // counter top would wall the player out of their own kitchen
    private static string EnsureClickBox(GameObject zone, Plateau plateau)
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

        collider.isTrigger = true;

        // Around the plate if there is one, around the whole shelf otherwise
        GameObject target = plateau == null ? zone : plateau.gameObject;

        if (!TryAnyBounds(target, out Bounds bounds))
        {
            box.transform.localPosition = Vector3.zero;
            collider.center = Vector3.zero;
            collider.size = Vector3.one;

            return "  " + clickBoxName + ": eklendi, boyut 1x1x1 -- elle ayarla\n";
        }

        box.transform.position = bounds.center;

        // A comfortable thumb target, whatever the plate measures. A box the
        // exact size of a plate is a box most taps miss
        Vector3 wanted = new Vector3(
            Mathf.Max(bounds.size.x, .7f),
            Mathf.Max(bounds.size.y, .5f),
            Mathf.Max(bounds.size.z, .7f));

        collider.center = Vector3.zero;
        collider.size = Divide(wanted, box.transform.lossyScale);

        return "  " + clickBoxName + ": tabagin uzerine kondu, dunyada " +
               wanted.ToString("0.00") + " -- tiklanacak yer burasi\n";
    }

    // Unlike TryBounds this one counts the plateau, since the plate is exactly
    // what the click box is meant to cover
    private static bool TryAnyBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;

        bool any = false;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
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

    // Moving the plate leaves the click box behind. Re-running the whole setup
    // would work too, but this touches nothing else
    [MenuItem("Cooked Fast/Istasyon/Malzeme Rafi: Tik Kutusunu Tabaga Oturt", priority = 171)]
    public static void RefitClickBox()
    {
        GameObject zone = Selection.activeGameObject;
        HoldingShelf shelf = zone == null ? null : zone.GetComponent<HoldingShelf>();

        if (shelf == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Hierarchy'den rafi sec (uzerinde HoldingShelf olan obje).", "Tamam");
            return;
        }

        Plateau plateau = new SerializedObject(shelf)
            .FindProperty("plateau").objectReferenceValue as Plateau;

        string report = EnsureClickBox(zone, plateau);

        EditorSceneManager.MarkSceneDirty(zone.scene);

        Debug.Log("[Malzeme Rafi]\n" + report +
                  "\nHala tiklanmiyorsa: Console'da 'raf secildi' satiri cikiyor mu bak." +
                  "\nCikmiyorsa isin hala baska bir seye carpiyor -- kutuyu buyut.");
    }

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

    private static Vector3 Divide(Vector3 wanted, Vector3 parent)
    {
        return new Vector3(
            Mathf.Abs(parent.x) < .0001f ? wanted.x : wanted.x / parent.x,
            Mathf.Abs(parent.y) < .0001f ? wanted.y : wanted.y / parent.y,
            Mathf.Abs(parent.z) < .0001f ? wanted.z : wanted.z / parent.z);
    }
}
