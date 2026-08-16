using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

// Renders every food prefab to a png and hands it back to the prefab.
//
// The project has no food icons and never had any -- the ones on the upgrade
// desk are stat icons. Drawing a set by hand would be a second thing to keep in
// step with the prefabs, and buying one means the picture and the model are two
// different pizzas. Rendering the model is the only version that cannot drift.
//
// Not AssetPreview.GetAssetPreview, which is the obvious way and the wrong one:
// it loads asynchronously, returns null or a placeholder while it thinks about
// it, and gives no say over the angle or the background
public static class FoodIconBaker
{
    private const string outputFolder = "Assets/Tiny Coffee Shop/Sprites/Food Icons";

    private const int resolution = 256;

    // Roughly the kitchen camera's angle, so the picture of the pizza looks like
    // the pizza the player is looking at
    private static readonly Vector3 viewAngle = new Vector3(25f, 135f, 0f);

    // Far from everything else in the scene. Rendering happens with a camera
    // whose clip planes only reach the subject, which culls the kitchen without
    // needing a spare layer -- and a spare layer is exactly the kind of thing a
    // project runs out of quietly
    private static readonly Vector3 stage = new Vector3(9000f, 9000f, 9000f);

    [MenuItem("Cooked Fast/Musteri: Yemek Ikonlarini Uret", priority = 601)]
    public static void Bake()
    {
        List<GameObject> foods = FindFoodPrefabs();

        if (foods.Count <= 0)
        {
            EditorUtility.DisplayDialog("Yemek Ikonlari",
                "SpawnableFood tasiyan prefab bulunamadi.", "Tamam");
            return;
        }

        if (!AssetDatabase.IsValidFolder(outputFolder))
            CreateFolders(outputFolder);

        StringBuilder report = new StringBuilder();

        int made = 0;

        foreach (GameObject food in foods)
        {
            string path = outputFolder + "/Icon_" + food.name.Replace(" ", "_") + ".png";

            if (Render(food, path))
            {
                made++;
                report.AppendLine("  " + food.name + " -> " + Path.GetFileName(path));
            }
            else
            {
                report.AppendLine("  " + food.name + ": gorunur mesh yok, atlandi");
            }
        }

        AssetDatabase.Refresh();

        // Imported as sprites in a second pass: the importer settings can only
        // be set once the files exist on disk and Unity has seen them
        foreach (GameObject food in foods)
        {
            string path = outputFolder + "/Icon_" + food.name.Replace(" ", "_") + ".png";

            if (File.Exists(path))
                MakeSprite(path);
        }

        AssetDatabase.Refresh();

        report.Insert(0, made + " ikon uretildi:\n");

        report.AppendLine();
        report.Append(Assign(foods));

        report.AppendLine();
        report.AppendLine("Klasor: " + outputFolder);
        report.AppendLine("Begenmedigin olursa prefabin Spawnable Food > Icon alanina");
        report.AppendLine("  elle baska bir resim surukleyebilirsin.");

        Debug.Log("[Ikon]\n" + report);
        EditorUtility.DisplayDialog("Yemek Ikonlari", report.ToString(), "Tamam");
    }

    // Written onto each prefab, so the bubble asks the food for its picture
    // instead of looking it up in a table that would have to be maintained
    private static string Assign(List<GameObject> foods)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("Prefablara baglandi:");

        foreach (GameObject food in foods)
        {
            string path = outputFolder + "/Icon_" + food.name.Replace(" ", "_") + ".png";

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
                continue;

            string prefabPath = AssetDatabase.GetAssetPath(food);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                SpawnableFood spawnable = root.GetComponent<SpawnableFood>();

                if (spawnable == null)
                    continue;

                SerializedObject so = new SerializedObject(spawnable);

                SerializedProperty field = so.FindProperty("icon");

                // A picture somebody chose by hand outlives a rendered one.
                //
                // The test is against this exact filename rather than the folder,
                // so a drawn icon is safe wherever it is kept -- including inside
                // this folder. Only the file this command wrote itself gets
                // replaced, which is what makes re-running it safe to do
                Object current = field.objectReferenceValue;

                if (current != null && AssetDatabase.GetAssetPath(current) != path)
                {
                    report.AppendLine("  " + food.name + ": elle konmus ikon var, dokunulmadi");
                    continue;
                }

                field.objectReferenceValue = sprite;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                report.AppendLine("  " + food.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return report.ToString();
    }

    private static bool Render(GameObject prefab, string path)
    {
        GameObject subject = Object.Instantiate(prefab);

        // The clone is a live SpawnableFood otherwise, and its Awake runs
        subject.transform.position = stage;
        subject.transform.rotation = Quaternion.identity;

        GameObject rig = new GameObject("Icon Rig");

        rig.transform.position = stage;

        Camera camera = rig.AddComponent<Camera>();

        RenderTexture texture = new RenderTexture(resolution, resolution, 24,
            RenderTextureFormat.ARGB32);

        Texture2D output = null;

        try
        {
            if (!TryBounds(subject, out Bounds bounds))
                return false;

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;

            // Fully transparent, so the icon sits on the bubble rather than in a
            // coloured square on it
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            float radius = bounds.extents.magnitude;

            camera.orthographicSize = radius * 1.15f;
            camera.nearClipPlane = .01f;

            // Just past the subject. This is what keeps the kitchen out of the
            // picture without a culling layer
            camera.farClipPlane = radius * 6f;

            rig.transform.rotation = Quaternion.Euler(viewAngle);
            rig.transform.position = bounds.center - rig.transform.forward * (radius * 3f);

            camera.targetTexture = texture;

            // Its own light, because the kitchen's is nowhere near the stage
            GameObject sun = new GameObject("Icon Light");

            sun.transform.position = stage;
            sun.transform.rotation = Quaternion.Euler(40f, 160f, 0f);

            Light light = sun.AddComponent<Light>();

            light.type = LightType.Directional;
            light.intensity = 1.3f;

            camera.Render();

            RenderTexture previous = RenderTexture.active;

            RenderTexture.active = texture;

            output = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);

            output.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            output.Apply();

            RenderTexture.active = previous;

            File.WriteAllBytes(path, output.EncodeToPNG());

            Object.DestroyImmediate(sun);

            return true;
        }
        finally
        {
            camera.targetTexture = null;

            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(subject);

            texture.Release();
            Object.DestroyImmediate(texture);

            if (output != null)
                Object.DestroyImmediate(output);
        }
    }

    // Public and shared, because a png that Unity imported as a plain Texture is
    // not a Sprite and LoadAssetAtPath<Sprite> answers null for it -- silently,
    // which then looks like a missing file or a wrong path. Every command that
    // reaches for artwork dropped into the project has to settle this first
    public static bool MakeSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
            return false;

        // Re-importing is slow and re-importing what is already right is slow
        // for nothing, so the settings are compared before they are written
        bool alreadyRight = importer.textureType == TextureImporterType.Sprite &&
                            importer.spriteImportMode == SpriteImportMode.Single &&
                            importer.alphaIsTransparency &&
                            !importer.mipmapEnabled;

        if (alreadyRight)
            return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;

        importer.SaveAndReimport();

        return true;
    }

    // ---- the drawn set -----------------------------------------------------

    private const string drawnFolder = "Assets/food-icons";

    // Prefab name on the left, file name on the right. An explicit table rather
    // than guessing from names: "Cup" is a coffee, "hamburger" is a burger, and
    // a rule clever enough to match those is a rule that will match the wrong
    // thing the first time a food is added
    private static readonly string[,] drawnMap =
    {
        { "burger", "hamburger" },
        { "burger 1", "hamburger" },
        { "fries", "fries" },
        { "Salad", "salad" },
        { "drink", "drink" },
        { "Cup", "coffee" },
    };

    [MenuItem("Cooked Fast/Musteri: Cizilmis Ikonlari Bagla", priority = 602)]
    public static void BindDrawn()
    {
        if (!AssetDatabase.IsValidFolder(drawnFolder))
        {
            EditorUtility.DisplayDialog("Cizilmis Ikonlar",
                "Klasor yok:\n" + drawnFolder, "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        int fixedImports = 0;

        // Every png in the folder, not only the mapped ones -- the sunburst
        // lives here too and the bubble needs it as a sprite just the same
        foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { drawnFolder }))
        {
            if (MakeSprite(AssetDatabase.GUIDToAssetPath(guid)))
                fixedImports++;
        }

        if (fixedImports > 0)
        {
            AssetDatabase.Refresh();
            report.AppendLine(fixedImports + " dosya Sprite olarak yeniden alindi.");
            report.AppendLine();
        }

        List<GameObject> foods = FindFoodPrefabs();

        report.AppendLine("Baglananlar:");

        int bound = 0;

        foreach (GameObject food in foods)
        {
            string file = DrawnFileFor(food.name);

            if (file == null)
            {
                report.AppendLine("  " + food.name + ": eslesen ikon yok, modeli kullanilacak");
                continue;
            }

            string path = drawnFolder + "/" + file + ".png";

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                report.AppendLine("  " + food.name + ": " + file + ".png OKUNAMADI");
                continue;
            }

            if (Bind(food, sprite))
            {
                bound++;
                report.AppendLine("  " + food.name + " -> " + file + ".png");
            }
        }

        AssetDatabase.SaveAssets();

        report.Insert(0, bound + " yemege ikon baglandi.\n\n");

        report.AppendLine();
        report.AppendLine("Baska bir resim istersen prefabin Spawnable Food > Icon");
        report.AppendLine("  alanina elle surukle -- bu komut ustune yazmaz.");

        Debug.Log("[Ikon]\n" + report);
        EditorUtility.DisplayDialog("Cizilmis Ikonlar", report.ToString(), "Tamam");
    }

    private static string DrawnFileFor(string prefabName)
    {
        for (int i = 0; i < drawnMap.GetLength(0); i++)
        {
            if (string.Equals(drawnMap[i, 0], prefabName,
                    System.StringComparison.OrdinalIgnoreCase))
                return drawnMap[i, 1];
        }

        return null;
    }

    private static bool Bind(GameObject food, Sprite sprite)
    {
        string prefabPath = AssetDatabase.GetAssetPath(food);

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            SpawnableFood spawnable = root.GetComponent<SpawnableFood>();

            if (spawnable == null)
                return false;

            SerializedObject so = new SerializedObject(spawnable);

            so.FindProperty("icon").objectReferenceValue = sprite;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static List<GameObject> FindFoodPrefabs()
    {
        List<GameObject> found = new List<GameObject>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null || prefab.GetComponent<SpawnableFood>() == null)
                continue;

            found.Add(prefab);
        }

        return found;
    }

    private static bool TryBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;

        bool any = false;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || !renderer.enabled)
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

    private static void CreateFolders(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
