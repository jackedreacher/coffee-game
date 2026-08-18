using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Puts a different model behind the cooked meat.
//
// Dragging a mesh out of the scene makes a prefab that looks right and is not a
// food: no component, the mesh sitting on the root, and the spot in the kitchen
// it was dragged from baked into its position. Left like that it lands on the
// plate twenty metres away. So it is upgraded in place first, then pointed at.
//
// Only the oven's own field actually decides what comes out of the pan. The
// recipe and the burger match on TYPE, so they keep working either way -- they
// are repointed anyway, because a field pointing at the prefab that is no longer
// used is a trap for whoever reads it next
public static class CookedMeatSwap
{
    private const string newPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Cooked-meat 2.prefab";
    private const string burgerPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/burger 1.prefab";
    private const string foodFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";
    private const string visualName = "Renderer";

    [MenuItem("Cooked Fast/Istasyon/Oven: 4 - Pismis Et Prefabini Degistir", priority = 143)]
    public static void Swap()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz.\n\n" +
                "Prefab degisiklikleri Play durunca geri alinir.\n" +
                "Once Play'i durdur, sonra tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        SpawnableFood raw = FindFood("meat");

        if (raw == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "meat.prefab bulunamadi ya da SpawnableFood degil.\n\n" +
                "Once 'Meat: 1 - Istasyonu Kur' calistir.", "Tamam");
            return;
        }

        SpawnableFood cooked = Upgrade(raw, report);

        if (cooked == null)
        {
            report.Insert(0, "SONUC: prefab hazirlanamadi, hicbir sey baglanmadi\n\n");
            Show(report);
            return;
        }

        report.AppendLine();
        report.Append(PointOven(cooked));

        report.AppendLine();
        report.Append(PointRecipe(cooked));

        report.AppendLine();
        report.Append(PointBurger(cooked));

        report.AppendLine();
        report.AppendLine("Ayarlar");
        report.AppendLine("  Buyuklugu   : Cooked-meat 2 > " + visualName + " > Scale");
        report.AppendLine("  Yigin araligi: Cooked-meat 2 > Cooked Meat > Clean Y Offset On Plateau");
        report.AppendLine("  Ocaktaki yeri: oven-zone > Cook Point");
        report.AppendLine();
        report.AppendLine("Not: burgerin icindeki et gorseli ayri bir mesh --");
        report.AppendLine("  burger 1 > et katmaninin Visual'i. Bu degisiklik onu etkilemez.");

        report.Insert(0, "SONUC: ocak artik Cooked-meat 2 veriyor\n\n");

        Show(report);
    }

    // ---- the patty inside the burger ---------------------------------------

    // The oven's prefab is never what the player ends up holding. Cooked meat is
    // a recipe part, so the moment it is picked up HoldFoodAbility reads its type
    // and folds it into a burger -- and what is on the tray from then on is the
    // burger's OWN meat layer, a separate mesh out of burger.fbx that still looks
    // raw. Cooking a patty and carrying a raw one away is the visible result.
    //
    // Kept as its own command rather than bolted onto the oven one: swapping the
    // burger's model is a look decision, not part of wiring the station up
    [MenuItem("Cooked Fast/Istasyon/Burger: Et Katmanini Pismis Et Modeline Cevir", priority = 144)]
    public static void SwapBurgerPatty()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz. Once Play'i durdur.", "Tamam");
            return;
        }

        GameObject cookedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);

        MeshFilter source = cookedAsset == null
            ? null
            : cookedAsset.GetComponentInChildren<MeshFilter>(true);

        if (source == null || source.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Cooked-meat 2 icinde mesh yok.\n\n" +
                "Once 'Oven: 4 - Pismis Et Prefabini Degistir' calistir.", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        Material[] materials = source.GetComponent<MeshRenderer>() == null
            ? null
            : source.GetComponent<MeshRenderer>().sharedMaterials;

        if (materials == null || materials.Length <= 0 || materials[0] == null)
        {
            if (!TryMaterialsFromModel(source.sharedMesh, out materials))
            {
                EditorUtility.DisplayDialog("Hata",
                    "Pismis etin materyali yok, burgere pembe et koymanin anlami yok.\n\n" +
                    "Once 'Oven: 4' calistir.", "Tamam");
                return;
            }

            report.AppendLine("  materyal FBX'ten alindi: " + materials[0].name);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(burgerPath);

        if (root == null)
        {
            EditorUtility.DisplayDialog("Hata", burgerPath + " acilamadi", "Tamam");
            return;
        }

        try
        {
            Burger burger = root.GetComponent<Burger>();

            if (burger == null)
            {
                EditorUtility.DisplayDialog("Hata", "burger 1 uzerinde Burger yok", "Tamam");
                return;
            }

            SerializedObject so = new SerializedObject(burger);
            SerializedProperty layers = so.FindProperty("layers");

            SerializedProperty meatLayer = null;

            for (int i = 0; i < layers.arraySize && meatLayer == null; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);

                if (element.FindPropertyRelative("part").objectReferenceValue is CookedMeat)
                    meatLayer = element;
            }

            if (meatLayer == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Burgerde et katmani bulunamadi.\n\n" +
                    "Layers icinde Part'i pismis et olan bir satir olmali.", "Tamam");
                return;
            }

            Transform visual = meatLayer.FindPropertyRelative("visual").objectReferenceValue as Transform;

            MeshFilter filter = visual == null ? null : visual.GetComponent<MeshFilter>();
            MeshRenderer renderer = visual == null ? null : visual.GetComponent<MeshRenderer>();

            if (filter == null || renderer == null || filter.sharedMesh == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Et katmaninin Visual'i bir mesh degil.", "Tamam");
                return;
            }

            report.AppendLine("Burger et katmani: " + visual.name);

            // Sized to the footprint the old patty had, not to the raw meat: it
            // has to keep fitting between two buns that are not moving
            Bounds was = filter.sharedMesh.bounds;
            Vector3 wasScale = visual.localScale;

            float wanted = Mathf.Max(
                was.size.x * Mathf.Abs(wasScale.x),
                was.size.z * Mathf.Abs(wasScale.z));

            Bounds now = source.sharedMesh.bounds;
            float have = Mathf.Max(now.size.x, now.size.z);

            float fit = have <= .0001f || wanted <= .0001f ? 1f : wanted / have;

            report.AppendLine("  mesh : " + filter.sharedMesh.name + " -> " + source.sharedMesh.name);
            report.AppendLine("  olcek: " + wasScale.ToString("0.00") + " -> " + fit.ToString("0.0000") +
                              "  (genislik " + wanted.ToString("0.000") + " birim, ayni kaldi)");

            filter.sharedMesh = source.sharedMesh;
            renderer.sharedMaterials = materials;
            visual.localScale = Vector3.one * fit;

            SerializedProperty height = meatLayer.FindPropertyRelative("height");

            float wasHeight = height.floatValue;

            height.floatValue = now.size.y * fit;

            report.AppendLine("  kalinlik: " + wasHeight.ToString("0.0000") + " -> " +
                              height.floatValue.ToString("0.0000"));

            // How far the mesh hangs below its own origin. A patty pivoted
            // through the middle has a negative min.y, and placing it at the
            // stack height buries that half in the bun underneath
            SerializedProperty lift = meatLayer.FindPropertyRelative("lift");

            if (lift != null)
            {
                lift.floatValue = -now.min.y * fit;

                report.AppendLine("  yukseltme: " + lift.floatValue.ToString("0.0000") +
                                  (Mathf.Abs(lift.floatValue) < .0001f
                                      ? "  (pivot zaten tabanda)"
                                      : "  (pivot mesh'in ortasinda, alt kismi kadar kaldirildi)"));
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, burgerPath, out bool saved);

            report.Insert(0, saved
                ? "SONUC: burgerdeki et artik pismis modele donustu\n\n"
                : "SONUC: KAYIT BASARISIZ\n\n");

            report.AppendLine();
            report.AppendLine("Ayarlar  (burger 1 > " + visual.name + ")");
            report.AppendLine("  Buyuklugu  : Transform > Scale");
            report.AppendLine("  Kalinligi  : burger 1 > Burger > Layers > et satiri > Height");
            report.AppendLine("    ustteki katman bu sayi kadar yukari cikar");
            report.AppendLine("  Yuksekligi : ayni satir > Lift");
            report.AppendLine("    et ekmegin icine gomuluyorsa buyut, havada kaliyorsa kucult");
            report.AppendLine();
            report.AppendLine("Eski haline dondurmek istersen: " + visual.name +
                              " > Mesh Filter'a burger.fbx icindeki");
            report.AppendLine("  " + was.size.ToString("0.00") + " boyutundaki eski mesh'i geri koy.");

            Show(report);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- turning the dragged mesh into a food ------------------------------

    private static SpawnableFood Upgrade(SpawnableFood raw, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(newPrefabPath);

        if (root == null)
        {
            report.AppendLine(newPrefabPath + " acilamadi");
            return null;
        }

        try
        {
            report.AppendLine("Prefab: " + root.name);

            MeshFilter filter = MoveMeshToChild(root, report);

            if (filter == null || filter.sharedMesh == null)
            {
                report.AppendLine("  icinde mesh yok, yapacak bir sey kalmadi");
                return null;
            }

            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

            // Whatever the mesh was dragged from, the food's own origin is the
            // point it balances on the plate. FoodPosition.Push zeroes position
            // and rotation on every pickup but never scale, so the root keeping
            // a scale of one is what stops the tray resizing it
            report.AppendLine("  kok sifirlandi: " +
                              root.transform.localPosition.ToString("0.00") + " -> 0,0,0");

            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            float fit = FitScale(raw, filter.sharedMesh, report);

            filter.transform.localPosition = Vector3.zero;
            filter.transform.localRotation = Quaternion.identity;
            filter.transform.localScale = Vector3.one * fit;

            CookedMeat cooked = root.GetComponent<CookedMeat>();

            if (cooked == null)
            {
                cooked = root.AddComponent<CookedMeat>();
                report.AppendLine("  CookedMeat eklendi");
            }
            else
            {
                report.AppendLine("  CookedMeat zaten vardi");
            }

            float height = filter.sharedMesh.bounds.size.y * fit;

            SerializedObject so = new SerializedObject(cooked);

            so.FindProperty("filter").objectReferenceValue = filter;
            so.FindProperty("meshRenderer").objectReferenceValue = renderer;
            so.FindProperty("cleanYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = height;
            so.FindProperty("dirtyMesh").objectReferenceValue = filter.sharedMesh;

            // Same flag the old cooked meat carried. Cooked meat is a burger
            // layer, not a meal -- handing one straight to a customer is exactly
            // what this stops
            so.FindProperty("ingredientOnly").boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();

            report.AppendLine("  yigin araligi: " + height.ToString("0.0000"));
            report.AppendLine("  tek basina servis edilemez (ingredientOnly)");

            PrefabUtility.SaveAsPrefabAsset(root, newPrefabPath, out bool saved);

            if (!saved)
            {
                report.AppendLine("  KAYIT BASARISIZ");
                return null;
            }

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);

            return reloaded == null ? null : reloaded.GetComponent<SpawnableFood>();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The mesh belongs on a child so the root's scale can stay at one. Every
    // other food in this project is built that way, and the size fit has to go
    // somewhere the plateau will not overwrite
    private static MeshFilter MoveMeshToChild(GameObject root, StringBuilder report)
    {
        Transform visual = root.transform.Find(visualName);

        MeshFilter rootFilter = root.GetComponent<MeshFilter>();
        MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();

        if (visual == null)
        {
            GameObject child = new GameObject(visualName);

            child.transform.SetParent(root.transform, false);
            visual = child.transform;

            report.AppendLine("  " + visualName + " cocugu olusturuldu");
        }

        MeshFilter filter = visual.GetComponent<MeshFilter>();

        if (filter == null)
            filter = visual.gameObject.AddComponent<MeshFilter>();

        MeshRenderer renderer = visual.GetComponent<MeshRenderer>();

        if (renderer == null)
            renderer = visual.gameObject.AddComponent<MeshRenderer>();

        if (rootFilter != null && filter.sharedMesh == null)
            filter.sharedMesh = rootFilter.sharedMesh;

        // Unconditionally, and that matters: AddComponent hands back a renderer
        // whose sharedMaterials is one entry long and that entry is null. Asking
        // whether the array was EMPTY answered no, the copy was skipped, and the
        // meat came out magenta
        if (rootRenderer != null)
            renderer.sharedMaterials = rootRenderer.sharedMaterials;

        // Renderer first: a MeshRenderer left behind with no filter draws
        // nothing and logs about it
        if (rootRenderer != null)
        {
            UnityEngine.Object.DestroyImmediate(rootRenderer, true);
            report.AppendLine("  mesh kokten " + visualName + "'a tasindi");
        }

        if (rootFilter != null)
            UnityEngine.Object.DestroyImmediate(rootFilter, true);

        report.Append(EnsureMaterial(renderer, filter.sharedMesh));

        return filter;
    }

    // The model the mesh came out of is the one thing that still knows how it
    // is meant to look. Needed as a repair as much as a safety net: once the
    // root renderer has been destroyed the original material reference is gone
    // from the prefab, so a second run has nowhere else to read it from
    private static string EnsureMaterial(MeshRenderer renderer, Mesh mesh)
    {
        if (!NeedsMaterial(renderer))
            return "  materyal: yerinde (" + renderer.sharedMaterial.name + ")\n";

        if (!TryMaterialsFromModel(mesh, out Material[] materials))
            return "  UYARI: materyal bulunamadi -- et PEMBE gorunur.\n" +
                   "    Cooked-meat 2 > " + visualName + " > Materials'a elle bir materyal koy\n";

        renderer.sharedMaterials = materials;

        return "  materyal FBX'ten alindi: " + materials[0].name + "\n";
    }

    // Empty, null, or one of Unity's built-in defaults. That last one is not
    // paranoia: the built-in default material uses a shader URP cannot render,
    // which is the same magenta by another route
    private static bool NeedsMaterial(MeshRenderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;

        if (materials == null || materials.Length <= 0)
            return true;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
                return true;

            string path = AssetDatabase.GetAssetPath(materials[i]);

            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                return true;
        }

        return false;
    }

    private static bool TryMaterialsFromModel(Mesh mesh, out Material[] materials)
    {
        materials = null;

        if (mesh == null)
            return false;

        string path = AssetDatabase.GetAssetPath(mesh);

        if (string.IsNullOrEmpty(path))
            return false;

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (model == null)
            return false;

        // The renderer sitting on this exact mesh first: an FBX with several
        // meshes in it has several materials, and taking the first one going
        // would put the aubergine's material on the meat
        foreach (MeshFilter candidate in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (candidate.sharedMesh != mesh)
                continue;

            MeshRenderer found = candidate.GetComponent<MeshRenderer>();

            if (found == null || found.sharedMaterials.Length <= 0 || found.sharedMaterials[0] == null)
                continue;

            materials = found.sharedMaterials;
            return true;
        }

        // One mesh in the file and the names did not line up. Still better than
        // magenta, and the report says where it came from
        foreach (MeshRenderer found in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (found.sharedMaterials.Length <= 0 || found.sharedMaterials[0] == null)
                continue;

            materials = found.sharedMaterials;
            return true;
        }

        return false;
    }

    // Matched to the raw meat rather than picked. The two are the same food at
    // two moments, and a steak that lands on the tray a different size from the
    // patty that went into the pan reads as the wrong item
    private static float FitScale(SpawnableFood raw, Mesh mesh, StringBuilder report)
    {
        MeshFilter rawFilter = raw.GetComponentInChildren<MeshFilter>(true);

        if (rawFilter == null || rawFilter.sharedMesh == null)
        {
            report.AppendLine("  olcek: ham et olculemedi, 1 birakildi");
            return 1f;
        }

        Vector3 rawScale = rawFilter.transform.localScale;
        Vector3 rawSize = rawFilter.sharedMesh.bounds.size;

        float wanted = Mathf.Max(
            rawSize.x * Mathf.Abs(rawScale.x),
            rawSize.z * Mathf.Abs(rawScale.z));

        Bounds bounds = mesh.bounds;
        float have = Mathf.Max(bounds.size.x, bounds.size.z);

        if (have <= .0001f || wanted <= .0001f)
        {
            report.AppendLine("  olcek: hesaplanamadi, 1 birakildi");
            return 1f;
        }

        float fit = wanted / have;

        report.AppendLine("  olcek " + fit.ToString("0.0000") +
                          "  (ham etin genisligi " + wanted.ToString("0.000") + " birim)");

        return fit;
    }

    // ---- pointing everything at it -----------------------------------------

    private static string PointOven(SpawnableFood cooked)
    {
        CookingStation[] stations = UnityEngine.Object.FindObjectsByType<CookingStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (stations.Length <= 0)
            return "Ocak: sahnede CookingStation yok -- oven-zone kurulu mu?\n";

        string report = "Ocak\n";

        foreach (CookingStation station in stations)
        {
            SerializedObject so = new SerializedObject(station);
            SerializedProperty property = so.FindProperty("cookedFoodPrefab");

            string was = property.objectReferenceValue == null
                ? "BOS"
                : property.objectReferenceValue.name;

            property.objectReferenceValue = cooked;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(station.gameObject.scene);

            report += "  " + station.name + ": " + was + " -> " + cooked.name + "\n";
        }

        return report;
    }

    private static string PointRecipe(SpawnableFood cooked)
    {
        HoldFoodAbility[] hands = UnityEngine.Object.FindObjectsByType<HoldFoodAbility>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (hands.Length <= 0)
            return "Tarif: sahnede HoldFoodAbility yok\n";

        string report = "Tarif  (elde birlesen malzemeler)\n";

        foreach (HoldFoodAbility hand in hands)
        {
            SerializedObject so = new SerializedObject(hand);
            SerializedProperty parts = so.FindProperty("recipeParts");

            if (parts == null || !parts.isArray)
            {
                report += "  " + hand.name + ": recipeParts yok\n";
                continue;
            }

            bool changed = false;

            for (int i = 0; i < parts.arraySize; i++)
            {
                SerializedProperty element = parts.GetArrayElementAtIndex(i);

                if (!(element.objectReferenceValue is CookedMeat))
                    continue;

                if (element.objectReferenceValue == cooked)
                    continue;

                element.objectReferenceValue = cooked;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(hand.gameObject.scene);
            }

            report += "  " + hand.name + ": " +
                      (changed ? "et girisi yenilendi" : "zaten dogru") + "\n";
        }

        return report;
    }

    private static string PointBurger(SpawnableFood cooked)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(burgerPath);

        if (root == null)
            return "Burger: " + burgerPath + " acilamadi\n";

        try
        {
            Burger burger = root.GetComponent<Burger>();

            if (burger == null)
                return "Burger: prefabda Burger bileseni yok\n";

            SerializedObject so = new SerializedObject(burger);
            SerializedProperty layers = so.FindProperty("layers");

            if (layers == null || !layers.isArray)
                return "Burger: layers alani yok\n";

            bool changed = false;

            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty part = layers.GetArrayElementAtIndex(i).FindPropertyRelative("part");

                if (part == null || !(part.objectReferenceValue is CookedMeat))
                    continue;

                if (part.objectReferenceValue == cooked)
                    continue;

                part.objectReferenceValue = cooked;
                changed = true;
            }

            if (!changed)
                return "Burger: et katmani zaten dogru\n";

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, burgerPath, out bool saved);

            return "Burger: et katmani " + cooked.name + " oldu" +
                   (saved ? "\n" : "  <-- KAYIT BASARISIZ\n");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- helpers -----------------------------------------------------------

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

    private static bool Same(string left, string right)
    {
        return string.Equals(
            left.Replace(" ", "").Replace("-", "").Replace("_", ""),
            right.Replace(" ", "").Replace("-", "").Replace("_", ""),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void Show(StringBuilder report)
    {
        Debug.Log("[Pismis Et Degisimi]\n" + report);
        EditorUtility.DisplayDialog("Pismis Et Degisimi", report.ToString(), "Tamam");
    }
}
