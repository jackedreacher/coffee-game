using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Walks the chain that ends in a picture over a customer's head, and says where
// it breaks.
//
// An empty bubble looks the same whichever link failed: the drop zone with no
// accepted food, the queue with no possible orders, the food with no icon. All
// three produce a blank card and none of them produce an error, so the bubble
// gets blamed for all three. This asks each link in turn
public static class OrderDiagnostics
{
    [MenuItem("Cooked Fast/Musteri/Siparis Durumunu Soyle", priority = 603)]
    public static void Tell()
    {
        StringBuilder report = new StringBuilder();

        report.Append(Counters());
        report.AppendLine();
        report.Append(Queues());
        report.AppendLine();
        report.Append(Foods());
        report.AppendLine();
        report.Append(Assets());

        Debug.Log("[Siparis Durumu]\n" + report);
        EditorUtility.DisplayDialog("Siparis Durumu", report.ToString(), "Tamam");
    }

    // Foods that are servable but should not be ordered anyway. Pizza by
    // request; "burger 1" is a second copy of the burger prefab and would
    // double its odds without being a second thing to want; Cup is the coffee
    // shop's, and this kitchen has no way to make one
    private static readonly string[] excluded = { "Pizza", "burger 1", "Cup" };

    // The scene has no OrderCounter, and OrderCounter was the only thing that
    // ever filled Possible Orders -- so it was empty everywhere and every
    // customer walked in wanting nothing. Filled here instead, from what the
    // kitchen can actually produce
    [MenuItem("Cooked Fast/Musteri/Siparisleri Doldur", priority = 604)]
    public static void Fill()
    {
        StringBuilder report = new StringBuilder();

        List<SpawnableFood> menu = Menu(report);

        if (menu.Count <= 0)
        {
            EditorUtility.DisplayDialog("Siparisler",
                "Servis edilebilir yemek bulunamadi.\n\n" + report, "Tamam");
            return;
        }

        FoodServingCustomerManager[] managers =
            Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        report.AppendLine();
        report.AppendLine("Yazildi:");

        foreach (FoodServingCustomerManager manager in managers)
        {
            SerializedObject so = new SerializedObject(manager);
            SerializedProperty orders = so.FindProperty("possibleOrders");

            orders.arraySize = menu.Count;

            for (int i = 0; i < menu.Count; i++)
                orders.GetArrayElementAtIndex(i).objectReferenceValue = menu[i];

            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

            report.AppendLine("  " + manager.name + ": " + menu.Count + " urun");
        }

        report.AppendLine();
        report.AppendLine("DIKKAT: mutfagin yapamadigi bir urun listede kalirsa");
        report.AppendLine("  onu isteyen musteri hicbir zaman servis edilemez.");
        report.AppendLine("  Inspector'dan o satiri sil.");
        report.AppendLine();
        report.AppendLine("Sahneyi kaydet: Ctrl+S");

        Debug.Log("[Siparis]\n" + report);
        EditorUtility.DisplayDialog("Siparisler", report.ToString(), "Tamam");
    }

    // The scene's own drop zones first: what a counter accepts IS what the
    // kitchen sells, and reading it beats a list kept somewhere separate. Only
    // when there are none does this fall back to every servable food prefab,
    // which is a guess and is reported as one
    private static List<SpawnableFood> Menu(StringBuilder report)
    {
        List<SpawnableFood> menu = new List<SpawnableFood>();
        List<string> dropped = new List<string>();

        FoodDropZone[] zones = Object.FindObjectsByType<FoodDropZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (FoodDropZone zone in zones)
        {
            SpawnableFood food = zone.AcceptedFood;

            // Raw meat and loose cheese are things the player carries, not
            // things anybody orders
            if (food == null || food.IngredientOnly || menu.Contains(food))
                continue;

            if (IsExcluded(food.name))
            {
                dropped.Add(food.name);
                continue;
            }

            menu.Add(food);
        }

        if (dropped.Count > 0)
        {
            report.AppendLine("Listeden cikarilanlar (OrderDiagnostics > excluded):");

            foreach (string name in dropped)
                report.AppendLine("  " + name);

            report.AppendLine();
        }

        if (menu.Count > 0)
        {
            report.AppendLine("Sahnedeki drop zone'lardan bulundu:");

            foreach (SpawnableFood food in menu)
                report.AppendLine("  " + food.name);

            return menu;
        }

        report.AppendLine("Geriye drop zone kalmadi -- prefablardan bulundu:");

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));

            if (prefab == null)
                continue;

            SpawnableFood food = prefab.GetComponent<SpawnableFood>();

            if (food == null || food.IngredientOnly || IsExcluded(prefab.name))
                continue;

            menu.Add(food);
            report.AppendLine("  " + prefab.name);
        }

        return menu;
    }

    private static bool IsExcluded(string name)
    {
        for (int i = 0; i < excluded.Length; i++)
        {
            if (string.Equals(excluded[i], name, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // Link one: what the counter is able to sell at all
    private static string Counters()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("1) TEZGAH -- ne satabiliyor");

        OrderCounter[] counters = Object.FindObjectsByType<OrderCounter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (counters.Length <= 0)
        {
            report.AppendLine("  sahnede OrderCounter yok");
            return report.ToString();
        }

        foreach (OrderCounter counter in counters)
        {
            report.AppendLine("  " + counter.name);

            SerializedProperty zones = new SerializedObject(counter).FindProperty("dropZones");

            if (zones == null || zones.arraySize <= 0)
            {
                report.AppendLine("    Drop Zones BOS -- hicbir sey satilamaz");
                continue;
            }

            for (int i = 0; i < zones.arraySize; i++)
            {
                FoodDropZone zone =
                    zones.GetArrayElementAtIndex(i).objectReferenceValue as FoodDropZone;

                if (zone == null)
                {
                    report.AppendLine("    [" + i + "] bos satir");
                    continue;
                }

                report.AppendLine("    [" + i + "] " + zone.name + " -> " +
                    (zone.AcceptedFood == null
                        ? "Accepted Food BOS"
                        : zone.AcceptedFood.name));
            }
        }

        return report.ToString();
    }

    // Link two: what a customer is allowed to walk in wanting
    private static string Queues()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("2) SIRA -- musteri ne isteyebilir");

        FoodServingCustomerManager[] managers =
            Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (managers.Length <= 0)
        {
            report.AppendLine("  sahnede FoodServingCustomerManager yok");
            return report.ToString();
        }

        foreach (FoodServingCustomerManager manager in managers)
        {
            report.AppendLine("  " + manager.name);

            SerializedProperty orders =
                new SerializedObject(manager).FindProperty("possibleOrders");

            if (orders == null || orders.arraySize <= 0)
            {
                // Not fatal on its own -- the counter fills this in Awake. It is
                // only fatal together with an empty counter, which is why both
                // are asked rather than either
                report.AppendLine("    Possible Orders bos.");
                report.AppendLine("    Tezgah Awake'de dolduruyor -- yukarisi doluysa sorun yok");
                continue;
            }

            int filled = 0;

            for (int i = 0; i < orders.arraySize; i++)
            {
                Object food = orders.GetArrayElementAtIndex(i).objectReferenceValue;

                if (food == null)
                {
                    report.AppendLine("    [" + i + "] BOS SATIR -- bu secilirse balon bos cikar");
                    continue;
                }

                filled++;
                report.AppendLine("    [" + i + "] " + food.name);
            }

            if (filled <= 0)
                report.AppendLine("    hicbiri dolu degil -- her musteri bos balonla gelir");
        }

        return report.ToString();
    }

    // Link three: whether the thing they asked for has a picture
    private static string Foods()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("3) YEMEKLER -- ikonu var mi");

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            SpawnableFood food = prefab.GetComponent<SpawnableFood>();

            if (food == null)
                continue;

            report.AppendLine("  " + prefab.name + ": " +
                (food.Icon == null ? "ikon YOK -- 3D modeli kullanilir" : food.Icon.name));
        }

        return report.ToString();
    }

    // Link four: the two pictures the bubble itself is made of
    private static string Assets()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("4) BALON PARCALARI");

        report.AppendLine("  kart : " + SpriteState(
            "Assets/Skyden_Games/Free_Casual_GUI/Demo/Sprites/Others/Shapes/Shape_01.png"));

        report.AppendLine("  isin : " + SpriteState("Assets/food-icons/Sunburst.png"));

        return report.ToString();
    }

    // Says which of the two ways it can be missing actually happened. A png that
    // exists but was imported as a plain Texture loads as a null Sprite, and
    // that is a different problem from a missing file with the same symptom
    private static string SpriteState(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            return "DOSYA YOK  " + path;

        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
            return "Sprite DEGIL -- Texture Type: Sprite yapilmali  " + path;

        return "tamam";
    }
}
