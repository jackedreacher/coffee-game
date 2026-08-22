#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Adds the hat wardrobe without rebuilding the existing GUI. Re-running the
// command wires the catalogue again but keeps hand-tuned RectTransforms.
public static class HatShopSetup
{
    private const string hatFolder = "Assets/LowpolyHats/Prefabs";
    private const string materialFolder = "Assets/LowpolyHats/Materials";
    private const string rootName = "Accessory Wardrobe";
    private const string fontPath =
        "Assets/Hyper_Casual_UI/Fonts/Baloo2-ExtraBold.ttf";
    private const string buttonPath =
        "Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/Green empty.png";
    private const string leftPath =
        "Assets/Hyper_Casual_UI/Sprites/Icons/left icon.png";
    private const string rightPath =
        "Assets/Hyper_Casual_UI/Sprites/Icons/right icon.png";

    [MenuItem("Cooked Fast/GUI/Sapka Magazasini Ekle veya Guncelle", priority = 223)]
    public static void InstallFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Sapka Magazasi",
                "Play modunu durdur, sonra tekrar dene.", "Tamam");
            return;
        }

        int hats = InstallIntoCurrentScene(true);
        if (hats < 0)
            return;

        EditorUtility.DisplayDialog("Sapka Magazasi",
            hats + " sapka kataloglandi.\n\n" +
            "- Mevcut GUI silinmedi veya yeniden kurulmadı\n" +
            "- Karakter ve sapka ayni canli 3D vitrinde seciliyor\n" +
            "- SAPKASIZ secenegi katalogda var\n" +
            "- Secim kaydediliyor ve Player'in Head kemigine takiliyor\n" +
            "- Sapka her hayvanin olcusune gore otomatik oturuyor\n\n" +
            "Sahne henuz KAYDEDILMEDI. Kontrol et, sonra Ctrl+S.", "Tamam");
    }

    public static int InstallIntoCurrentScene(bool selectResult)
    {
        HyperCasualGameMenu menu = UnityEngine.Object.FindFirstObjectByType<
            HyperCasualGameMenu>(FindObjectsInactive.Include);
        CharacterSkinPreview preview = UnityEngine.Object.FindFirstObjectByType<
            CharacterSkinPreview>(FindObjectsInactive.Include);

        if (menu == null || preview == null)
        {
            if (selectResult)
                EditorUtility.DisplayDialog("Sapka Magazasi",
                    "Sahnede HyperCasualGameMenu veya CharacterSkinPreview yok.",
                    "Tamam");
            return -1;
        }

        SerializedObject menuObject = new SerializedObject(menu);
        GameObject characterMenu = menuObject.FindProperty("characterMenu")
            .objectReferenceValue as GameObject;

        if (characterMenu == null)
        {
            if (selectResult)
                EditorUtility.DisplayDialog("Sapka Magazasi",
                    "Menu icindeki Character Shop baglantisi bos.", "Tamam");
            return -1;
        }

        int materials = UpgradeMaterials();
        List<GameObject> hats = HatPrefabs();
        if (hats.Count == 0)
        {
            if (selectResult)
                EditorUtility.DisplayDialog("Sapka Magazasi",
                    hatFolder + " icinde prefab bulunamadi.", "Tamam");
            return -1;
        }

        Transform wardrobe = characterMenu.transform.Find(rootName);
        if (wardrobe == null)
        {
            GameObject host = NewUi(rootName, characterMenu.transform);
            wardrobe = host.transform;
            Stretch((RectTransform)wardrobe);
            Undo.RegisterCreatedObjectUndo(host, "Add accessory wardrobe");
        }
        wardrobe.SetAsLastSibling();

        RawImage display = EnsureRaw(wardrobe, "Live 3D Wardrobe",
            new Vector2(0f, 105f), new Vector2(650f, 760f));
        EnsureLabel(wardrobe, "Hat Section Title", "HATS", 50,
            new Vector2(0f, 690f), new Vector2(500f, 80f));
        Text skinName = EnsureLabel(wardrobe, "Wardrobe Skin Name", "SQUIRREL", 39,
            new Vector2(0f, -250f), new Vector2(540f, 65f));
        Button previousSkin = EnsureArrow(wardrobe, "Wardrobe Previous Skin",
            leftPath, new Vector2(-405f, 90f));
        Button nextSkin = EnsureArrow(wardrobe, "Wardrobe Next Skin",
            rightPath, new Vector2(405f, 90f));

        EnsureLabel(wardrobe, "Hat Label", "HATS", 32,
            new Vector2(0f, -345f), new Vector2(420f, 55f));
        Text hatName = EnsureLabel(wardrobe, "Selected Hat Name", "NO HAT", 34,
            new Vector2(0f, -430f), new Vector2(520f, 65f));
        Button previousHat = EnsureArrow(wardrobe, "Previous Hat", leftPath,
            new Vector2(-325f, -430f));
        Button nextHat = EnsureArrow(wardrobe, "Next Hat", rightPath,
            new Vector2(325f, -430f));
        Button equip = EnsureButton(wardrobe, "Equip Character And Hat", "EQUIP",
            new Vector2(0f, -570f), new Vector2(360f, 115f));

        // Replace only the obsolete fixed icon/hotspot; nothing is deleted.
        Transform oldPanel = characterMenu.transform.Find("Hyper Casual Shop");
        HideOld(oldPanel == null ? null : oldPanel.Find("Character"));
        HideOld(oldPanel == null ? null : oldPanel.Find("Buy Squirrel Chef"));

        SerializedObject previewObject = new SerializedObject(preview);
        Set(previewObject, "wardrobeDisplay", display);
        Set(previewObject, "wardrobeSkinName", skinName);
        Set(previewObject, "wardrobePreviousSkin", previousSkin);
        Set(previewObject, "wardrobeNextSkin", nextSkin);
        Set(previewObject, "hatName", hatName);
        Set(previewObject, "previousHatButton", previousHat);
        Set(previewObject, "nextHatButton", nextHat);

        SerializedProperty models = previewObject.FindProperty("hatModels");
        SerializedProperty names = previewObject.FindProperty("hatNames");
        models.arraySize = hats.Count;
        names.arraySize = hats.Count;
        for (int i = 0; i < hats.Count; i++)
        {
            models.GetArrayElementAtIndex(i).objectReferenceValue = hats[i];
            names.GetArrayElementAtIndex(i).stringValue = PrettyName(hats[i].name);
        }
        previewObject.ApplyModifiedPropertiesWithoutUndo();

        menuObject.Update();
        menuObject.FindProperty("chooseCharacterButton").objectReferenceValue = equip;
        menuObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(preview);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(characterMenu.scene);
        if (selectResult)
            Selection.activeGameObject = wardrobe.gameObject;
        if (materials > 0)
            AssetDatabase.SaveAssets();
        return hats.Count;
    }

    private static int UpgradeMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
        {
            Debug.LogError("[Sapka] URP Lit shader bulunamadi.");
            return 0;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material",
            new[] { materialFolder });
        int changed = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == shader ||
                (material.shader != null && material.shader.name.StartsWith(
                    "Universal Render Pipeline/")))
                continue;

            Texture texture = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.mainTexture;
            Color colour = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.color;
            float smoothness = material.HasProperty("_Smoothness")
                ? material.GetFloat("_Smoothness")
                : material.HasProperty("_Glossiness")
                    ? material.GetFloat("_Glossiness")
                    : 0f;

            Undo.RecordObject(material, "Convert hat material to URP");
            material.shader = shader;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            changed++;
        }

        if (changed > 0)
            Debug.Log("[Sapka] " + changed + " materyal URP'ye donusturuldu.");
        return changed;
    }

    private static List<GameObject> HatPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { hatFolder });
        List<GameObject> result = new List<GameObject>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                result.Add(prefab);
        }
        result.Sort((a, b) => string.Compare(a.name, b.name,
            StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static string PrettyName(string name)
    {
        if (name.StartsWith("m_", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(2);
        if (name.EndsWith("01", StringComparison.Ordinal))
            name = name.Substring(0, name.Length - 2);
        name = name.Replace("Hat", "");
        for (int i = name.Length - 1; i > 0; i--)
            if (char.IsUpper(name[i]) && !char.IsWhiteSpace(name[i - 1]))
                name = name.Insert(i, " ");
        return name.Trim();
    }

    private static void HideOld(Transform item)
    {
        if (item == null || !item.gameObject.activeSelf)
            return;
        Undo.RecordObject(item.gameObject, "Hide old fixed shop control");
        item.gameObject.SetActive(false);
    }

    private static RawImage EnsureRaw(Transform parent, string name,
        Vector2 position, Vector2 size)
    {
        bool created;
        GameObject host = FindOrCreate(parent, name, out created);
        RawImage image = host.GetComponent<RawImage>();
        if (image == null)
            image = Undo.AddComponent<RawImage>(host);
        image.color = Color.white;
        image.raycastTarget = false;
        if (created)
            Place((RectTransform)host.transform, position, size);
        return image;
    }

    private static Text EnsureLabel(Transform parent, string name, string value,
        int fontSize, Vector2 position, Vector2 size)
    {
        bool created;
        GameObject host = FindOrCreate(parent, name, out created);
        Text label = host.GetComponent<Text>();
        if (label == null)
            label = Undo.AddComponent<Text>(host);
        label.font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        label.text = value;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, .88f, .58f);
        label.raycastTarget = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 18;
        label.resizeTextMaxSize = fontSize;
        if (host.GetComponent<Outline>() == null)
        {
            Outline outline = Undo.AddComponent<Outline>(host);
            outline.effectColor = new Color(.20f, .035f, .035f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
        }
        if (created)
            Place((RectTransform)host.transform, position, size);
        return label;
    }

    private static Button EnsureArrow(Transform parent, string name,
        string spritePath, Vector2 position)
    {
        bool created;
        GameObject host = FindOrCreate(parent, name, out created);
        Image image = host.GetComponent<Image>();
        if (image == null)
            image = Undo.AddComponent<Image>(host);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        image.preserveAspect = true;
        Button button = host.GetComponent<Button>();
        if (button == null)
            button = Undo.AddComponent<Button>(host);
        button.targetGraphic = image;
        if (created)
            Place((RectTransform)host.transform, position, new Vector2(130f, 130f));
        return button;
    }

    private static Button EnsureButton(Transform parent, string name, string value,
        Vector2 position, Vector2 size)
    {
        bool created;
        GameObject host = FindOrCreate(parent, name, out created);
        Image image = host.GetComponent<Image>();
        if (image == null)
            image = Undo.AddComponent<Image>(host);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(buttonPath);
        image.preserveAspect = false;
        Button button = host.GetComponent<Button>();
        if (button == null)
            button = Undo.AddComponent<Button>(host);
        button.targetGraphic = image;
        EnsureLabel(host.transform, "Label", value, 38, Vector2.zero, size);
        if (created)
            Place((RectTransform)host.transform, position, size);
        return button;
    }

    private static GameObject FindOrCreate(Transform parent, string name,
        out bool created)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            created = false;
            return existing.gameObject;
        }
        GameObject host = NewUi(name, parent);
        Undo.RegisterCreatedObjectUndo(host, "Add " + name);
        created = true;
        return host;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));
        host.layer = 5;
        host.transform.SetParent(parent, false);
        return host;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void Set(SerializedObject so, string property,
        UnityEngine.Object value)
    {
        SerializedProperty field = so.FindProperty(property);
        if (field != null)
            field.objectReferenceValue = value;
    }
}
#endif
