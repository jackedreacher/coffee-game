using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// The burger is built in the player's hand, layer by layer. burger 1.prefab
// already has every layer as its own child, so this command's job is to say
// which child belongs to which ingredient and hand that map to the Burger
// component -- after that, picking an ingredient up switches its child on
public static class BurgerSetup
{
    private const string foodFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";
    private const string burgerName = "burger 1";

    // Ingredient prefab -> the words that name its layer inside burger 1
    private static readonly string[] partNames = { "bread", "cooked-meat", "cheese" };

    private static readonly string[][] layerKeywords =
    {
        new[] { "bun", "bread" },
        new[] { "patty", "meat" },
        new[] { "cheese" },
    };

    // The closing piece. Matched first and taken out of the running, or the
    // bread layer would claim it too and a lone bun would come with a lid
    private static readonly string[] lidKeywords = { "top" };

    [MenuItem("Cooked Fast/Burger: 1 - Tarifi Kur (elde birlesir)", priority = 150)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz.\n\n" +
                "Oyuncuya yazilan tarif Play durunca silinir.\n" +
                "Once Play'i durdur, sonra tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        SpawnableFood[] parts = new SpawnableFood[partNames.Length];
        bool missing = false;

        for (int i = 0; i < partNames.Length; i++)
        {
            parts[i] = FindFood(partNames[i]);

            if (parts[i] != null)
                continue;

            report.AppendLine(partNames[i] + ".prefab bulunamadi ya da yemek degil");
            missing = true;
        }

        if (missing)
        {
            report.AppendLine();
            report.AppendLine("Eksik olanlari once kur:");
            report.AppendLine("  bread       -> Bread: 1 - Istasyonu Kur");
            report.AppendLine("  cheese      -> Cheese: 1 - Istasyonu Kur");
            report.AppendLine("  cooked-meat -> Oven: 1 - Istasyonu Kur");

            report.Insert(0, "SONUC: malzemeler eksik, tarif yazilmadi\n\n");
            Show(report, "Burger Tarifi -- YARIM KALDI");
            return;
        }

        Burger burger = BuildBurger(parts, report);

        if (burger == null)
        {
            report.Insert(0, "SONUC: burger prefabi hazirlanamadi, hicbir seye dokunulmadi\n\n");
            Show(report, "Burger Tarifi -- YARIM KALDI");
            return;
        }

        report.AppendLine();
        report.AppendLine("Servis kurali");

        for (int i = 0; i < parts.Length; i++)
            report.Append(SetIngredientOnly(parts[i], true));

        report.AppendLine("  " + burgerName + ".prefab: katmanlari tamamlaninca verilebilir");
        report.AppendLine("    (kutu degil, kendi durumundan cevap veriyor)");

        report.AppendLine();
        report.Append(WriteRecipe(burger, parts, out bool wrote));

        report.Insert(0, wrote
            ? "SONUC: malzemeler oyuncunun elinde burger'a ekleniyor\n\n"
            : "SONUC: tarif yazilamadi -- oyuncu bulunamadi\n\n");

        Show(report, "Burger Tarifi");
    }

    private static void Show(StringBuilder report, string title)
    {
        Debug.Log("[" + title + "]\n" + report);
        EditorUtility.DisplayDialog(title, report.ToString(), "Tamam");
    }

    // ---- the burger prefab -------------------------------------------------

    private static Burger BuildBurger(SpawnableFood[] parts, StringBuilder report)
    {
        string path = FindPrefabPath(burgerName);

        if (path == null)
        {
            report.AppendLine(burgerName + ".prefab bulunamadi: " + foodFolder);
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
        {
            report.AppendLine(path + " acilamadi");
            return null;
        }

        try
        {
            Burger burger = root.GetComponent<Burger>();
            bool added = burger == null;

            if (added)
                burger = root.AddComponent<Burger>();

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            if (filters.Length <= 0)
            {
                report.AppendLine(path + " icinde mesh yok");
                return null;
            }

            report.AppendLine(burgerName + ".prefab hazirlandi");
            report.AppendLine("  " + path);
            report.AppendLine("  Burger bileseni " + (added ? "eklendi" : "zaten vardi"));
            report.AppendLine();
            report.AppendLine("Katmanlar");

            SerializedObject so = new SerializedObject(burger);
            SerializedProperty layers = so.FindProperty("layers");

            if (layers == null)
            {
                report.AppendLine("  layers alani yok -- derleme bitmemis olabilir");
                return null;
            }

            layers.arraySize = parts.Length;

            List<Transform> used = new List<Transform>();

            // Claimed before the layers get a look, since "bun-top" answers to
            // the bread keywords as readily as "bun-bottom" does
            Transform lid = MatchOne(root.transform, lidKeywords, used);

            if (lid != null)
                used.Add(lid);

            so.FindProperty("lid").objectReferenceValue = lid;

            for (int i = 0; i < parts.Length; i++)
            {
                Transform found = MatchOne(root.transform, layerKeywords[i], used);

                SerializedProperty entry = layers.GetArrayElementAtIndex(i);

                entry.FindPropertyRelative("part").objectReferenceValue = parts[i];
                entry.FindPropertyRelative("visual").objectReferenceValue = found;
                entry.FindPropertyRelative("height").floatValue = Thickness(found);

                report.Append("  " + parts[i].GetType().Name + " -> ");

                if (found == null)
                {
                    report.AppendLine("ESLESMEDI  <-- elle bagla");
                    continue;
                }

                used.Add(found);

                report.AppendLine(found.name +
                                  "   kalinlik " + Thickness(found).ToString("0.0000"));
            }

            report.AppendLine("  Kapak (ust ekmek) -> " +
                              (lid == null ? "ESLESMEDI  <-- elle bagla" : lid.name));

            // The renderer fields on SpawnableFood are not optional: IsVisible
            // reads meshRenderer.enabled and would throw on a null one the first
            // time a plateau rearranged itself
            MeshFilter anchor = filters[0];

            so.FindProperty("filter").objectReferenceValue = anchor;
            so.FindProperty("meshRenderer").objectReferenceValue = anchor.GetComponent<MeshRenderer>();
            so.FindProperty("dirtyMesh").objectReferenceValue = anchor.sharedMesh;

            float height = Height(root);

            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;
            so.FindProperty("ingredientOnly").boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);

            if (!saved)
            {
                report.AppendLine("KAYIT BASARISIZ: " + path);
                return null;
            }

            report.AppendLine();
            report.AppendLine("  yigin araligi: " + height.ToString("0.0000"));

            string leftovers = Unmapped(root.transform, used);

            if (leftovers.Length > 0)
            {
                report.AppendLine("  eslesmeyen cocuklar: " + leftovers);
                report.AppendLine("    Bunlar hicbir malzemeye bagli degil -- oyunda hep gorunurler.");
                report.AppendLine("    Gerekiyorsa " + burgerName + " > Burger > Katmanlar'dan ekle.");
            }

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            return reloaded == null ? null : reloaded.GetComponent<Burger>();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform MatchOne(Transform root, string[] keywords, List<Transform> taken)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root || taken.Contains(child))
                continue;

            string name = Strip(child.name).ToLowerInvariant();

            for (int i = 0; i < keywords.Length; i++)
            {
                if (name.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                return child;
            }
        }

        return null;
    }

    // How far the next layer has to rise to sit on this one. Read off the mesh
    // rather than typed in, and left on the component so it can be nudged when
    // two layers end up looking wedged together
    private static float Thickness(Transform visual)
    {
        if (visual == null)
            return 0f;

        MeshFilter filter = visual.GetComponentInChildren<MeshFilter>(true);

        if (filter == null || filter.sharedMesh == null)
            return 0f;

        return filter.sharedMesh.bounds.size.y * Mathf.Abs(visual.localScale.y);
    }

    private static string Unmapped(Transform root, List<Transform> used)
    {
        string list = "";

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root || used.Contains(child))
                continue;

            if (child.GetComponent<MeshFilter>() == null)
                continue;

            list += (list.Length > 0 ? ", " : "") + child.name;
        }

        return list;
    }

    private static float Height(GameObject root)
    {
        Bounds bounds = default;
        bool any = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return any ? bounds.size.y : .1f;
    }

    // ---- the recipe on the carrier ------------------------------------------

    private static string WriteRecipe(Burger burger, SpawnableFood[] parts, out bool wrote)
    {
        wrote = false;

        string report = "Tarif\n";
        int count = 0;

        foreach (HoldFoodAbility carrier in UnityEngine.Object.FindObjectsByType<HoldFoodAbility>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (carrier.GetComponent<PlayerController>() == null)
                continue;

            Undo.RecordObject(carrier, "Burger tarifi");

            SerializedObject so = new SerializedObject(carrier);
            SerializedProperty list = so.FindProperty("recipeParts");

            if (list == null)
            {
                report += "  recipeParts alani yok -- derleme bitmemis olabilir\n";
                return report;
            }

            list.arraySize = parts.Length;

            for (int i = 0; i < parts.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];

            so.FindProperty("recipeResult").objectReferenceValue = burger;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(carrier.gameObject.scene);

            report += "  " + Path(carrier.transform) + "\n";
            count++;
        }

        if (count <= 0)
        {
            report += "  Sahnede PlayerController tasiyan HoldFoodAbility bulunamadi\n";
            return report;
        }

        wrote = true;

        report += "  malzemeler: ";

        for (int i = 0; i < parts.Length; i++)
            report += (i > 0 ? " + " : "") + parts[i].GetType().Name;

        report += "\n  sonuc     : " + burgerName + "\n";
        report += "  sira onemsiz -- ilk malzemeyi alinca burger elinde olusur,\n";
        report += "  digerleri geldikce katmanlari acilir\n";
        report += "\n";
        report += "  Tarifi degistirmek icin: Player > Hold Food Ability > Tarif\n";
        report += "  Katmanlari degistirmek icin: " + burgerName + " > Burger > Katmanlar\n";

        return report;
    }

    private static string Path(Transform transform)
    {
        string path = transform.name;

        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    // ---- the serving rule --------------------------------------------------

    private static string SetIngredientOnly(SpawnableFood food, bool value)
    {
        string path = AssetDatabase.GetAssetPath(food);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
            return "  " + path + " acilamadi\n";

        try
        {
            SpawnableFood target = root.GetComponent<SpawnableFood>();

            if (target == null)
                return "  " + path + " icinde SpawnableFood yok\n";

            SerializedObject so = new SerializedObject(target);
            SerializedProperty flag = so.FindProperty("ingredientOnly");

            if (flag == null)
                return "  ingredientOnly alani yok -- derleme bitmemis olabilir\n";

            string name = System.IO.Path.GetFileName(path);

            if (flag.boolValue == value)
                return "  " + name + ": zaten tek basina verilemez\n";

            flag.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);

            return "  " + name + ": tek basina VERILEMEZ olarak isaretlendi\n";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- shared ------------------------------------------------------------

    private static bool Same(string left, string right)
    {
        return string.Equals(Strip(left), Strip(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Strip(string text)
    {
        return text.Replace(" ", "").Replace("-", "").Replace("_", "");
    }

    private static string FindPrefabPath(string foodName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { foodFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Same(System.IO.Path.GetFileNameWithoutExtension(path), foodName))
                return path;
        }

        return null;
    }

    private static SpawnableFood FindFood(string foodName)
    {
        string path = FindPrefabPath(foodName);
        GameObject prefab = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);

        return prefab == null ? null : prefab.GetComponent<SpawnableFood>();
    }
}
