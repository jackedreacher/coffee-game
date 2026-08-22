#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds the full-screen Cooked Fast front door around Hyper Casual UI Pack's
// supplied controls. Pause/settings/shop/exit remain the package's finished
// screens; the main screen uses the project's own squirrel-chef key art.
public static class HyperCasualGuiSetup
{
    [System.Serializable]
    private sealed class LayoutFile
    {
        public List<LayoutNode> nodes = new List<LayoutNode>();
    }

    [System.Serializable]
    private sealed class LayoutNode
    {
        public string path;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
        public Vector3 localEulerAngles;
        public int siblingIndex;
        public bool active;
    }

    private const string rootName = "Hyper Casual GUI";
    private const string oldRootName = "Cartoon GUI";
    private const string layoutAsset =
        "Assets/Editor/HyperCasualGuiLayout.json";
    private const string package = "Assets/Hyper_Casual_UI/Sprites/";
    private const string gameUi = package + "GameUI/";
    private const string buttons = package + "Buttons/";
    private const string icons = package + "Icons/";
    private const string toggles = package + "Toggle/";
    private const string backdropArt = package + "Panel_Sprites/Settings pannel.png";
    private const string modeCardArt = package + "Panel_Sprites/Resume Game popup.png";
    private const string mainBackground =
        "Assets/Tiny Coffee Shop/Sprites/UI/CookedFast_MainMenu_Background_NoCharacter.png";
    private const string menuFont =
        "Assets/Hyper_Casual_UI/Fonts/Baloo2-ExtraBold.ttf";
    private const string quitButtonArt =
        "Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/light red.png";
    private const string pauseButtonArt =
        "Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/orange.png";
    private const string animalFolder =
        "Assets/DGN_15_CapsuleAnimals/Models/Characters/Outlined_Characters/";
    private const string animalPrefabFolder =
        "Assets/DGN_15_CapsuleAnimals/Prefabs/Character_Prefabs/" +
        "Outlined_Character_Prefabs/";
    private const string playerController =
        "Assets/Tiny Coffee Shop/Animations/Capsule/Capsule Player.controller";
    private const string avatarSource = animalFolder + "DGN_Bear_Outline.fbx";

    private const string pauseArt = gameUi + "Pause (1).png";
    private const string shopArt = gameUi + "Shop Panel.png";
    private const string quitArt = gameUi + "EXIT GAME.png";

    private static readonly string[] requiredSprites =
    {
        mainBackground, modeCardArt, pauseArt, shopArt,
        quitArt, backdropArt,
        buttons + "Play.png", buttons + "Setting.png",
        buttons + "Shop.png", buttons + "Retry.png",
        buttons + "Resume.png", buttons + "Home.png",
        buttons + "Pause.png", quitButtonArt, pauseButtonArt,
        buttons + "Back.png", buttons + "empty_buttons/blue.png",
        icons + "character.png", icons + "setting.png",
        icons + "left icon.png", icons + "right icon.png",
        toggles + "Toggle_ON.png", toggles + "Toggle_Off.png"
    };

    private static readonly string[] skinNames =
    {
        "Squirrel", "Bear", "Beaver", "Bull", "Cat", "Cow", "Deer",
        "Dog", "Fox", "Koala", "Mouse", "Panda", "Pig", "Rabbit", "Ram"
    };

    private sealed class Parts
    {
        public GameObject main;
        public GameObject pause;
        public GameObject settings;
        public GameObject character;
        public GameObject quit;
        public GameObject hud;
        public Button play;
        public Button shop;
        public Button settingsOpen;
        public Button quitOpen;
        public Button pauseOpen;
        public Button quickQuit;
        public Button retry;
        public Button resume;
        public Button pauseSettings;
        public Button home;
        public Button pauseQuit;
        public Button sound;
        public Button music;
        public Button settingsOk;
        public Image soundImage;
        public Image musicImage;
        public Slider effectsSlider;
        public Slider musicSlider;
        public Dropdown qualityDropdown;
        public Dropdown languageDropdown;
        public Button choose;
        public Button characterBack;
        public Button quitYes;
        public Button quitCancel;
        public CharacterSkinPreview skinPreview;
    }

    [MenuItem("Cooked Fast/GUI/Mevcut GUI Duzenini Kaydet", priority = 219)]
    public static void SaveCurrentLayout()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Hyper Casual GUI",
                "Play modunu durdur, sonra tekrar dene.", "Tamam");
            return;
        }

        GameObject current = FindGenerated(rootName);
        if (current == null)
        {
            EditorUtility.DisplayDialog("Hyper Casual GUI",
                "Sahnede Hyper Casual GUI bulunamadi.", "Tamam");
            return;
        }

        int count = CaptureLayout(current);
        EditorUtility.DisplayDialog("Hyper Casual GUI",
            count + " GUI nesnesinin elle yaptigin duzeni kaydedildi.\n\n" +
            layoutAsset, "Tamam");
    }

    [MenuItem("Cooked Fast/GUI/Pause Butonunu Cevirilebilir Yap", priority = 221)]
    public static void LocalizePauseButton()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Pause Butonu",
                "Play modunu durdur, sonra tekrar dene.", "Tamam");
            return;
        }

        EnsureSprite(pauseButtonArt);
        Sprite blank = Load(pauseButtonArt);
        HyperCasualGameMenu menu = Object.FindFirstObjectByType<HyperCasualGameMenu>(
            FindObjectsInactive.Include);

        if (menu == null || blank == null)
        {
            EditorUtility.DisplayDialog("Pause Butonu",
                menu == null ? "Sahnede HyperCasualGameMenu bulunamadi."
                             : "Paketin yazisiz turuncu butonu bulunamadi:\n" + pauseButtonArt,
                "Tamam");
            return;
        }

        SerializedObject menuObject = new SerializedObject(menu);
        Button pause = menuObject.FindProperty("pauseButton").objectReferenceValue as Button;

        if (pause == null)
        {
            EditorUtility.DisplayDialog("Pause Butonu",
                "HyperCasualGameMenu.pauseButton baglantisi bos.", "Tamam");
            return;
        }

        Image image = pause.GetComponent<Image>();
        if (image == null)
            image = Undo.AddComponent<Image>(pause.gameObject);

        Undo.RecordObject(image, "Make pause button localizable");
        image.sprite = blank;
        image.preserveAspect = true;
        pause.targetGraphic = image;

        RectTransform buttonRect = (RectTransform)pause.transform;
        Transform existing = pause.transform.Find("Label");
        Text label;

        if (existing != null && existing.TryGetComponent(out label))
        {
            Undo.RecordObject(label, "Update pause label");
            label.text = "PAUSE";
        }
        else
        {
            label = Label(pause.transform, "Label", "PAUSE", 38, Vector2.zero,
                buttonRect.sizeDelta, Color.white, new Color(.35f, .10f, .03f), 2f);
            Undo.RegisterCreatedObjectUndo(label.gameObject, "Add pause label");
        }

        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = buttonRect.sizeDelta;

        EditorUtility.SetDirty(pause);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = pause.gameObject;

        EditorUtility.DisplayDialog("Pause Butonu",
            "Pause yazisi artik gercek bir UI etiketidir ve secilen dile gore degisir.\n\n" +
            "Ctrl+S ile kaydet.", "Tamam");
    }

    [MenuItem("Cooked Fast/GUI/Settings Ekranini Tam Ekran Duzelt", priority = 222)]
    public static void FixSettingsLayout()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Settings Ekrani",
                "Play modunu durdur, sonra tekrar dene.", "Tamam");
            return;
        }

        HyperCasualGameMenu menu = Object.FindFirstObjectByType<HyperCasualGameMenu>(
            FindObjectsInactive.Include);

        if (menu == null)
        {
            EditorUtility.DisplayDialog("Settings Ekrani",
                "Sahnede HyperCasualGameMenu bulunamadi.", "Tamam");
            return;
        }

        SerializedObject menuObject = new SerializedObject(menu);
        GameObject settings = menuObject.FindProperty("settingsMenu")
            .objectReferenceValue as GameObject;
        RectTransform panel = settings == null ? null :
            settings.transform.Find("Hyper Casual Settings") as RectTransform;

        if (panel == null)
        {
            EditorUtility.DisplayDialog("Settings Ekrani",
                "Settings/Hyper Casual Settings paneli bulunamadi.", "Tamam");
            return;
        }

        Undo.RecordObjects(panel.GetComponentsInChildren<RectTransform>(true),
            "Fix settings layout");
        Undo.RecordObjects(panel.GetComponentsInChildren<Text>(true),
            "Fix settings labels");
        Undo.RecordObjects(panel.GetComponentsInChildren<Image>(true),
            "Fix settings images");

        LayoutSettingsPanel(panel);

        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;

        EditorUtility.DisplayDialog("Settings Ekrani",
            "Settings paneli tam ekrana yayildi ve butun kontroller ekrana sigacak " +
            "sekilde yeniden hizalandi.\n\nCtrl+S ile kaydet.", "Tamam");
    }

    [MenuItem("Cooked Fast/GUI/Hyper Casual GUI Kur", priority = 220)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Hyper Casual GUI",
                "Play modunu durdur, sonra bu komutu tekrar calistir.", "Tamam");
            return;
        }

        // Some files in this asset-store package ship as ordinary Texture2D
        // despite being UI artwork (notably Settings pannel.png). Normalize
        // every image the installer consumes before validating it.
        for (int i = 0; i < requiredSprites.Length; i++)
            EnsureSprite(requiredSprites[i]);

        string missing = MissingAsset();
        if (!string.IsNullOrEmpty(missing))
        {
            EditorUtility.DisplayDialog("Hyper Casual GUI",
                "Paket dosyasi bulunamadi:\n" + missing, "Tamam");
            return;
        }

        GameScreens screens = Object.FindFirstObjectByType<GameScreens>(
            FindObjectsInactive.Include);

        if (screens == null)
        {
            EditorUtility.DisplayDialog("Hyper Casual GUI",
                "Sahnede GameScreens yok. Once oyun ekranlari kurulumunu calistir.",
                "Tamam");
            return;
        }

        GameObject existing = FindGenerated(rootName);
        int remembered = existing == null ? 0 : CaptureLayout(existing);

        EnsureEventSystem();
        DestroyGenerated(oldRootName);
        DestroyGenerated(rootName);

        GameObject root = CanvasRoot();
        Parts ui = new Parts();

        BuildMain(root, ui);
        BuildPause(root, ui);
        BuildSettings(root, ui);
        BuildCharacter(root, ui);
        BuildQuit(root, ui);
        BuildHud(root, ui);

        HyperCasualGameMenu menu = Undo.AddComponent<HyperCasualGameMenu>(root);
        Wire(menu, screens, ui);
        WireGameScreens(screens, ui.main);

        // Add the accessory wardrobe before restoring the captured layout.
        // This makes a future full GUI rebuild retain hand-positioned hat UI
        // controls as well as the older menu controls.
        int hats = HatShopSetup.InstallIntoCurrentScene(false);
        int restored = ApplyLayout(root);
        MatchMainButtonSizes(ui);
        MatchHudButtonSizes(ui);

        // Invisible in Edit Mode and Device Simulator preview. The canvas root
        // stays active so Awake can run; the runtime components turn Main Menu
        // on only after Play actually begins.
        ui.main.SetActive(false);
        ui.pause.SetActive(false);
        ui.settings.SetActive(false);
        ui.character.SetActive(false);
        ui.quit.SetActive(false);
        ui.hud.SetActive(false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        string report =
            "Hyper Casual GUI kuruldu.\n\n" +
            "- Eski Cartoon GUI sahneden kaldirildi\n" +
            "- Tam ekran Cooked Fast sincap-sef ana menu arka plani kullanildi\n" +
            "- Ustte logo, ortada CANLI 3D skin vitrini, altta Hizli Servis karti kuruldu\n" +
            "- Hazir Pause / Settings / Shop / Exit ekranlari kullanildi\n" +
            "- Sag/sol ok ve kaydirma ile 15 hayvan skin'i seciliyor\n" +
            "- Magazada " + Mathf.Max(0, hats) + " adet 3D sapka seciliyor\n" +
            "- Close kaldirildi; her yerde gercek QUIT dugmesi kullaniliyor\n" +
            "- Hazir Play / Settings / Shop / Retry / Resume / Home / Pause butonlari kullanildi\n" +
            "- Ses ve muzik dugmeleri paketin ON/OFF sprite'lariyla calisiyor\n" +
            "- Edit Mode ve Simulator onizlemesinde gizli; Play'de acilir\n\n" +
            (remembered > 0
                ? "- Elle yaptigin GUI duzeni otomatik kaydedildi (" + remembered + ")\n"
                : "") +
            (restored > 0
                ? "- Kayitli GUI duzeni yeni yapinin ustune geri uygulandi (" + restored + ")\n\n"
                : "") +
            "Sincap, plateau ve oynanis objelerine dokunulmadi.\n" +
            "Sahne henuz KAYDEDILMEDI: kontrol et, sonra Ctrl+S.";

        Debug.Log("[Hyper Casual GUI]\n" + report, root);
        EditorUtility.DisplayDialog("Hyper Casual GUI", report, "Tamam");
    }

    private static string MissingAsset()
    {
        for (int i = 0; i < requiredSprites.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(requiredSprites[i]) == null)
                return requiredSprites[i];
        }

        if (AssetDatabase.LoadAssetAtPath<Font>(menuFont) == null)
            return menuFont;

        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(playerController) == null)
            return playerController;

        if (AvatarIn(avatarSource) == null)
            return avatarSource + " (Avatar)";

        for (int i = 0; i < skinNames.Length; i++)
        {
            string model = SkinPrefab(skinNames[i]);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(model) == null)
                return model;
        }

        return null;
    }

    private static void EnsureSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null || importer.textureType == TextureImporterType.Sprite)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void EnsureEventSystem()
    {
        EventSystem current = Object.FindFirstObjectByType<EventSystem>(
            FindObjectsInactive.Include);

        if (current != null)
        {
            StandaloneInputModule legacy = current.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Undo.DestroyObjectImmediate(legacy);

            if (current.GetComponent<InputSystemUIInputModule>() == null)
                Undo.AddComponent<InputSystemUIInputModule>(current.gameObject);

            return;
        }

        GameObject events = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(events, "Create EventSystem");
        events.AddComponent<EventSystem>();
        events.AddComponent<InputSystemUIInputModule>();
    }

    private static void DestroyGenerated(string name)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = all.Length - 1; i >= 0; i--)
        {
            if (all[i] != null && all[i].name == name && all[i].parent == null)
                Undo.DestroyObjectImmediate(all[i].gameObject);
        }
    }

    private static GameObject FindGenerated(string name)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            Transform item = all[i];
            if (item != null && item.parent == null && item.name == name)
                return item.gameObject;
        }

        return null;
    }

    private static int CaptureLayout(GameObject root)
    {
        LayoutFile file = new LayoutFile();
        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);

        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == root.transform)
                continue;

            file.nodes.Add(new LayoutNode
            {
                path = RelativePath(root.transform, rect),
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                anchoredPosition = rect.anchoredPosition,
                sizeDelta = rect.sizeDelta,
                localScale = rect.localScale,
                localEulerAngles = rect.localEulerAngles,
                siblingIndex = rect.GetSiblingIndex(),
                active = rect.gameObject.activeSelf,
            });
        }

        string absolute = Path.GetFullPath(layoutAsset);
        File.WriteAllText(absolute, JsonUtility.ToJson(file, true));
        AssetDatabase.ImportAsset(layoutAsset, ImportAssetOptions.ForceUpdate);
        return file.nodes.Count;
    }

    private static int ApplyLayout(GameObject root)
    {
        string absolute = Path.GetFullPath(layoutAsset);
        if (!File.Exists(absolute))
            return 0;

        LayoutFile file = JsonUtility.FromJson<LayoutFile>(
            File.ReadAllText(absolute));
        if (file == null || file.nodes == null)
            return 0;

        Dictionary<string, RectTransform> byPath =
            new Dictionary<string, RectTransform>(System.StringComparer.Ordinal);
        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i] != root.transform)
                byPath[RelativePath(root.transform, rects[i])] = rects[i];
        }

        int applied = 0;
        for (int i = 0; i < file.nodes.Count; i++)
        {
            LayoutNode saved = file.nodes[i];
            if (saved.path.StartsWith("Settings/Hyper Casual Settings",
                System.StringComparison.Ordinal))
                continue;
            if (!byPath.TryGetValue(saved.path, out RectTransform rect))
                continue;

            rect.anchorMin = saved.anchorMin;
            rect.anchorMax = saved.anchorMax;
            rect.pivot = saved.pivot;
            rect.anchoredPosition = saved.anchoredPosition;

            // These two controls have just changed from a tall triangle into
            // a horizontal package button. Preserve the user's placement but
            // retain the new control's correct 130x50 proportions.
            if (!saved.path.EndsWith("/Previous Skin", System.StringComparison.Ordinal) &&
                !saved.path.EndsWith("/Next Skin", System.StringComparison.Ordinal))
                rect.sizeDelta = saved.sizeDelta;

            rect.localScale = saved.localScale;
            rect.localEulerAngles = saved.localEulerAngles;
            rect.gameObject.SetActive(saved.active);
            applied++;
        }

        // Sibling indices are applied after transforms so one move cannot
        // change the index that a later entry refers to.
        for (int i = 0; i < file.nodes.Count; i++)
        {
            LayoutNode saved = file.nodes[i];
            if (byPath.TryGetValue(saved.path, out RectTransform rect))
                rect.SetSiblingIndex(Mathf.Clamp(saved.siblingIndex, 0,
                    rect.parent.childCount - 1));
        }

        return applied;
    }

    private static string RelativePath(Transform root, Transform item)
    {
        Stack<string> parts = new Stack<string>();
        Transform walk = item;

        while (walk != null && walk != root)
        {
            parts.Push(walk.name);
            walk = walk.parent;
        }

        return string.Join("/", parts);
    }

    private static GameObject CanvasRoot()
    {
        GameObject root = new GameObject(rootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Hyper Casual GUI");
        root.layer = 5;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 620;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 1f;

        root.AddComponent<GraphicRaycaster>();
        Stretch((RectTransform)root.transform);
        return root;
    }

    private static void BuildMain(GameObject root, Parts ui)
    {
        ui.main = Screen(root, "Main Menu", false);
        FullScreenArt(ui.main, "Cooked Fast Background", mainBackground);

        Label(ui.main.transform, "Logo",
            "<color=#FFF4DA>COOKED</color>\n<color=#FFB51B>FAST</color>",
            112, new Vector2(0f, 735f), new Vector2(900f, 260f),
            Color.white, new Color(.20f, .035f, .035f, 1f), 7f);
        Label(ui.main.transform, "Tagline", "HIZLI MUTFAK  •  MUTLU MUSTERI",
            31, new Vector2(0f, 605f), new Vector2(850f, 70f),
            new Color(1f, .88f, .58f), new Color(.20f, .035f, .035f, 1f), 3f);

        RawImage preview = Raw(ui.main.transform, "Live 3D Skin",
            new Vector2(-190f, 115f), new Vector2(600f, 720f));
        Button previous = SkinArrowButton(ui.main.transform, "Previous Skin",
            "<", new Vector2(-475f, 100f));
        Button next = SkinArrowButton(ui.main.transform, "Next Skin",
            ">", new Vector2(95f, 100f));
        Text selectedSkin = Label(ui.main.transform, "Skin Name", "SQUIRREL", 42,
            new Vector2(-190f, -255f), new Vector2(520f, 75f),
            new Color(1f, .88f, .58f), new Color(.20f, .035f, .035f, 1f), 3f);

        ui.skinPreview = preview.gameObject.AddComponent<CharacterSkinPreview>();
        WireSkinPreview(ui.skinPreview, preview, selectedSkin, previous, next);

        // Text baked into Setting.png / Shop.png cannot change language.
        // Use the matching blank package buttons, then draw icon and label as
        // real UI children so GameLocalization can replace the words live.
        ui.settingsOpen = AnchoredLocalizedButton(ui.main.transform, "Settings",
            "SETTINGS", buttons + "empty_buttons/PURPLE.png",
            icons + "setting.png", new Vector2(330f, 320f), 300f, 31);
        ui.shop = AnchoredLocalizedButton(ui.main.transform, "Shop",
            "SHOP", buttons + "empty_buttons/orange.png",
            icons + "shop.png", new Vector2(330f, 165f), 300f, 34);
        ui.quitOpen = FloatingTextButton(ui.main, "Quit", "QUIT",
            new Vector2(-28f, -120f), TextAnchor.UpperRight, 190f);

        RectTransform card = AnchoredArt(ui.main.transform, "Fast Service Card",
            modeCardArt, new Vector2(0f, -570f), 940f);
        Label(card, "Mode Title", "HIZLI SERVIS", 65,
            new Vector2(0f, 73f), new Vector2(800f, 100f),
            new Color(.30f, .10f, .055f), Color.white, 2f);
        Label(card, "Mode Detail", "Siparisleri tamamla, mutfagini buyut!", 31,
            new Vector2(0f, 5f), new Vector2(800f, 70f),
            new Color(.32f, .16f, .10f), Color.clear, 0f);
        ui.play = AnchoredLocalizedButton(card, "Play", "PLAY",
            buttons + "empty_buttons/Green empty.png", null,
            new Vector2(0f, -112f), 390f, 54);
    }

    private static void BuildPause(GameObject root, Parts ui)
    {
        // Pause is a modal card, not another full-screen menu. Keep the live
        // kitchen visible behind it, but intercept taps outside the card so a
        // pause tap cannot also queue a gameplay destination.
        ui.pause = Screen(root, "Pause", false);
        DimBehind(ui.pause, .42f);
        RectTransform panel = Art(ui.pause, "Hyper Casual Pause", pauseArt, 900f);
        Vector2 native = new Vector2(446f, 478f);

        ui.retry = Hotspot(panel, "Retry", new Vector2(223f, 184f),
            new Vector2(185f, 70f), native);
        ui.resume = Hotspot(panel, "Resume", new Vector2(223f, 255f),
            new Vector2(185f, 70f), native);
        ui.pauseSettings = Hotspot(panel, "Setting", new Vector2(223f, 326f),
            new Vector2(185f, 70f), native);
        ui.home = Hotspot(panel, "Home", new Vector2(223f, 397f),
            new Vector2(185f, 70f), native);
        ui.pauseQuit = FloatingTextButton(ui.pause, "Quit", "QUIT",
            new Vector2(-28f, -120f), TextAnchor.UpperRight, 190f);
    }

    private static void BuildSettings(GameObject root, Parts ui)
    {
        ui.settings = Screen(root, "Settings");
        RectTransform panel = Art(ui.settings, "Hyper Casual Settings", backdropArt, 1020f);
        Vector2 native = new Vector2(741f, 713f);

        SpriteImage(panel, "Settings Gear", icons + "setting.png",
            Px(92f, 82f, native), native, 78f);
        PanelLabel(panel, "Settings Title", "SETTINGS", 64,
            new Vector2(395f, 82f), new Vector2(500f, 90f), native);

        PanelLabel(panel, "SFX Label", "SFX", 42,
            new Vector2(100f, 205f), new Vector2(150f, 65f), native);
        ui.soundImage = SpriteImage(panel, "Sound Toggle", toggles + "Toggle_ON.png",
            Px(225f, 205f, native), native, 118f);
        ui.sound = ui.soundImage.gameObject.AddComponent<Button>();
        ui.sound.targetGraphic = ui.soundImage;
        ui.effectsSlider = PackageSlider(panel, "SFX Volume",
            new Vector2(485f, 205f), new Vector2(360f, 54f), native);

        PanelLabel(panel, "Music Label", "MUSIC", 42,
            new Vector2(100f, 298f), new Vector2(180f, 65f), native);
        ui.musicImage = SpriteImage(panel, "Music Toggle", toggles + "Toggle_Off.png",
            Px(225f, 298f, native), native, 118f);
        ui.music = ui.musicImage.gameObject.AddComponent<Button>();
        ui.music.targetGraphic = ui.musicImage;
        ui.musicSlider = PackageSlider(panel, "Music Volume",
            new Vector2(485f, 298f), new Vector2(360f, 54f), native);

        PanelLabel(panel, "Quality Label", "GRAPHICS", 36,
            new Vector2(125f, 410f), new Vector2(210f, 62f), native);
        ui.qualityDropdown = PackageDropdown(panel, "Graphic Quality",
            new Vector2(480f, 410f), new Vector2(420f, 72f), native);

        PanelLabel(panel, "Language Label", "LANGUAGE", 36,
            new Vector2(125f, 510f), new Vector2(230f, 62f), native);
        ui.languageDropdown = PackageDropdown(panel, "Language",
            new Vector2(480f, 510f), new Vector2(420f, 72f), native);

        ui.settingsOk = AnchoredTextButton(panel, "OK", "OK",
            buttons + "empty_buttons/blue.png",
            PanelPoint(panel, native, 371f, 632f), 260f, 46);

        LayoutSettingsPanel(panel);
    }

    private static void LayoutSettingsPanel(RectTransform panel)
    {
        if (panel == null)
            return;

        // Settings is a proper full-screen page. The previous 1020x981 card
        // left large dead bands above and below it and pushed long translated
        // labels through the left edge. Stretch the package artwork, then lay
        // out against a 1080x1920 portrait reference with uniform control
        // scaling so buttons and toggles keep their authored proportions.
        Stretch(panel);
        panel.localScale = Vector3.one;

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.preserveAspect = false;
            panelImage.raycastTarget = false;
        }

        Canvas.ForceUpdateCanvases();

        Vector2 actual = panel.rect.size;
        if (actual.x <= 1f || actual.y <= 1f)
            actual = new Vector2(1080f, 1920f);

        float uniform = Mathf.Min(actual.x / 1080f, actual.y / 1920f);

        PlaceSettings(panel, "Settings Gear", new Vector2(-430f, 720f),
            new Vector2(90f, 90f), actual, uniform);
        PlaceSettings(panel, "Settings Title", new Vector2(0f, 720f),
            new Vector2(650f, 110f), actual, uniform);

        PlaceSettings(panel, "SFX Label", new Vector2(-390f, 340f),
            new Vector2(260f, 82f), actual, uniform);
        PlaceSettings(panel, "Sound Toggle", new Vector2(-185f, 340f),
            new Vector2(150f, 51f), actual, uniform);
        PlaceSettings(panel, "SFX Volume", new Vector2(180f, 340f),
            new Vector2(500f, 76f), actual, uniform);

        PlaceSettings(panel, "Music Label", new Vector2(-390f, 100f),
            new Vector2(260f, 82f), actual, uniform);
        PlaceSettings(panel, "Music Toggle", new Vector2(-185f, 100f),
            new Vector2(150f, 51f), actual, uniform);
        PlaceSettings(panel, "Music Volume", new Vector2(180f, 100f),
            new Vector2(500f, 76f), actual, uniform);

        PlaceSettings(panel, "Quality Label", new Vector2(-365f, -210f),
            new Vector2(300f, 82f), actual, uniform);
        RectTransform quality = PlaceSettings(panel, "Graphic Quality",
            new Vector2(175f, -210f), new Vector2(570f, 104f), actual, uniform);

        PlaceSettings(panel, "Language Label", new Vector2(-365f, -510f),
            new Vector2(300f, 82f), actual, uniform);
        RectTransform language = PlaceSettings(panel, "Language",
            new Vector2(175f, -510f), new Vector2(570f, 104f), actual, uniform);

        RectTransform ok = PlaceSettings(panel, "OK", new Vector2(0f, -800f),
            new Vector2(310f, 118f), actual, uniform);

        ResizeSlider(panel.Find("SFX Volume") as RectTransform);
        ResizeSlider(panel.Find("Music Volume") as RectTransform);
        ResizeDropdown(quality);
        ResizeDropdown(language);

        if (ok != null && ok.Find("Label") is RectTransform okLabel)
            FitChild(okLabel, ok.rect.size);

        FitSettingsText(panel, "Settings Title", 64, 36);
        FitSettingsText(panel, "SFX Label", 42, 24);
        FitSettingsText(panel, "Music Label", 42, 24);
        FitSettingsText(panel, "Quality Label", 36, 20);
        FitSettingsText(panel, "Language Label", 36, 20);
    }

    private static RectTransform PlaceSettings(RectTransform panel, string name,
        Vector2 referencePosition, Vector2 referenceSize, Vector2 actual,
        float uniform)
    {
        RectTransform rect = panel.Find(name) as RectTransform;
        if (rect == null)
            return null;

        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(
            referencePosition.x * actual.x / 1080f,
            referencePosition.y * actual.y / 1920f);
        rect.sizeDelta = referenceSize * uniform;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void ResizeSlider(RectTransform slider)
    {
        if (slider == null)
            return;

        if (slider.Find("Handle Slide Area/Handle") is RectTransform handle)
            handle.sizeDelta = new Vector2(34f, Mathf.Max(24f, slider.rect.height - 8f));
    }

    private static void ResizeDropdown(RectTransform dropdown)
    {
        if (dropdown == null)
            return;

        float width = dropdown.rect.width;
        float height = dropdown.rect.height;

        if (dropdown.Find("Label") is RectTransform label)
        {
            label.anchoredPosition = new Vector2(-22f, 0f);
            label.sizeDelta = new Vector2(Mathf.Max(80f, width - 90f), height);
        }

        if (dropdown.Find("Arrow") is RectTransform arrow)
        {
            arrow.anchoredPosition = new Vector2(width * .5f - 40f, 1f);
            arrow.sizeDelta = new Vector2(60f, height);
        }

        if (dropdown.Find("Template") is RectTransform template)
            template.anchoredPosition = new Vector2(0f, -height * .5f);
    }

    private static void FitChild(RectTransform child, Vector2 size)
    {
        child.anchorMin = child.anchorMax = child.pivot = new Vector2(.5f, .5f);
        child.anchoredPosition = Vector2.zero;
        child.sizeDelta = size;
    }

    private static void FitSettingsText(RectTransform panel, string name,
        int maximum, int minimum)
    {
        Transform child = panel.Find(name);
        if (child == null || !child.TryGetComponent(out Text text))
            return;

        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minimum;
        text.resizeTextMaxSize = maximum;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static void BuildCharacter(GameObject root, Parts ui)
    {
        ui.character = Screen(root, "Character Shop");
        RectTransform panel = Art(ui.character, "Hyper Casual Shop", shopArt, 1020f);
        Vector2 native = new Vector2(741f, 713f);

        SpriteImage(panel, "Character", icons + "character.png",
            Px(143f, 304f, native), native, 92f);
        ui.choose = Hotspot(panel, "Buy Squirrel Chef", new Vector2(371f, 580f),
            new Vector2(235f, 92f), native);
        ui.characterBack = FloatingButton(ui.character, "Back", buttons + "Back.png",
            new Vector2(28f, -120f), TextAnchor.UpperLeft, 190f);
    }

    private static void BuildQuit(GameObject root, Parts ui)
    {
        // Exit is a modal card over the screen it came from. It intentionally
        // has no full-screen opaque image of its own.
        ui.quit = Screen(root, "Exit Game", false);
        RectTransform panel = Art(ui.quit, "Hyper Casual Exit", quitArt, 1000f);
        Vector2 native = new Vector2(598f, 223f);

        ui.quitYes = Hotspot(panel, "Yes", new Vector2(222f, 169f),
            new Vector2(145f, 57f), native);
        ui.quitCancel = Hotspot(panel, "Cancel", new Vector2(377f, 169f),
            new Vector2(145f, 57f), native);
    }

    private static void BuildHud(GameObject root, Parts ui)
    {
        ui.hud = Screen(root, "Game Buttons", false);
        ui.pauseOpen = FloatingLocalizedButton(ui.hud, "Pause", "PAUSE",
            pauseButtonArt, new Vector2(28f, -120f), TextAnchor.UpperLeft,
            190f, 38);
        ui.quickQuit = FloatingTextButton(ui.hud, "Quit", "QUIT",
            new Vector2(-28f, -120f), TextAnchor.UpperRight, 190f);
    }

    private static GameObject Screen(GameObject root, string name, bool blocker = true)
    {
        GameObject screen = UiObject(name, root.transform);
        Stretch((RectTransform)screen.transform);

        if (blocker)
        {
            // A dedicated child is intentional. Putting the Image on the same
            // object as the full-screen container proved unreliable in the
            // generated Simulator hierarchy: only the centred foreground art
            // was drawn. This child has an independently stretched RectTransform
            // and therefore covers every pixel outside the package panel too.
            GameObject background = UiObject("Opaque Background", screen.transform);
            RectTransform rect = (RectTransform)background.transform;
            Stretch(rect);
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();

            Image image = background.AddComponent<Image>();
            image.sprite = Load(backdropArt);
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = true;
        }

        return screen;
    }

    private static void DimBehind(GameObject screen, float alpha)
    {
        GameObject dimmer = UiObject("Dim Behind", screen.transform);
        RectTransform rect = (RectTransform)dimmer.transform;
        Stretch(rect);
        rect.localScale = Vector3.one;
        rect.SetAsFirstSibling();

        Image image = dimmer.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        image.raycastTarget = true;
    }

    private static RectTransform Art(GameObject screen, string name, string path,
        float width)
    {
        Sprite sprite = Load(path);
        GameObject host = UiObject(name, screen.transform);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);

        Image image = host.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }

    private static void FullScreenArt(GameObject screen, string name, string path)
    {
        GameObject host = UiObject(name, screen.transform);
        RectTransform rect = (RectTransform)host.transform;
        Stretch(rect);
        rect.SetAsFirstSibling();

        Image image = host.AddComponent<Image>();
        image.sprite = Load(path);
        // The generated art is 942x1680, within 0.3% of the 1080x1920 game
        // frame. Stretching avoids a one-pixel letterbox on device without a
        // perceptible change in the artwork.
        image.preserveAspect = false;
        image.raycastTarget = true;
    }

    private static RawImage Raw(Transform parent, string name, Vector2 position,
        Vector2 size)
    {
        GameObject host = UiObject(name, parent);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        RawImage image = host.AddComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = true;
        return image;
    }

    private static RectTransform AnchoredArt(Transform parent, string name,
        string path, Vector2 position, float width)
    {
        Sprite sprite = Load(path);
        GameObject host = UiObject(name, parent);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width,
            width * sprite.rect.height / sprite.rect.width);

        Image image = host.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }

    private static Button AnchoredSpriteButton(Transform parent, string name,
        string path, Vector2 position, float width)
    {
        Sprite sprite = Load(path);
        GameObject host = UiObject(name, parent);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width,
            width * sprite.rect.height / sprite.rect.width);

        Image image = host.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;

        Button button = host.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Button AnchoredLocalizedButton(Transform parent, string name,
        string value, string backgroundPath, string iconPath, Vector2 position,
        float width, int fontSize)
    {
        Button button = AnchoredSpriteButton(parent, name, backgroundPath,
            position, width);
        RectTransform rect = (RectTransform)button.transform;
        float iconSpace = string.IsNullOrEmpty(iconPath) ? 0f : 54f;

        if (!string.IsNullOrEmpty(iconPath))
        {
            Sprite iconSprite = Load(iconPath);
            GameObject iconHost = UiObject("Icon", button.transform);
            RectTransform iconRect = (RectTransform)iconHost.transform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot =
                new Vector2(.5f, .5f);
            iconRect.anchoredPosition = new Vector2(-width * .32f, 0f);
            iconRect.sizeDelta = new Vector2(48f, 48f);

            Image icon = iconHost.AddComponent<Image>();
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        Label(button.transform, "Label", value, fontSize,
            new Vector2(iconSpace * .35f, 1f),
            new Vector2(rect.sizeDelta.x - iconSpace - 24f, rect.sizeDelta.y),
            Color.white, new Color(.20f, .06f, .03f), 2f);
        return button;
    }

    private static Button SkinArrowButton(Transform parent, string name,
        string arrow, Vector2 position)
    {
        GameObject host = UiObject(name, parent);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(120f, 120f);

        // No visible button plate: the large transparent graphic keeps a
        // comfortable mobile tap target while only the chevron is drawn.
        Image hit = host.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, .001f);
        hit.raycastTarget = true;

        Button button = host.AddComponent<Button>();
        button.targetGraphic = hit;

        Label(button.transform, "Arrow", arrow, 96, Vector2.up * 4f,
            rect.sizeDelta, Color.white, new Color(.03f, .18f, .25f), 5f);
        return button;
    }

    private static void MatchHudButtonSizes(Parts ui)
    {
        if (ui.pauseOpen == null || ui.quickQuit == null)
            return;

        RectTransform pauseRect = (RectTransform)ui.pauseOpen.transform;
        RectTransform quitRect = (RectTransform)ui.quickQuit.transform;
        quitRect.sizeDelta = pauseRect.sizeDelta;

        Image quitImage = ui.quickQuit.GetComponent<Image>();
        if (quitImage != null)
            quitImage.preserveAspect = false;

        Transform label = ui.quickQuit.transform.Find("Label");
        if (label is RectTransform labelRect)
            labelRect.sizeDelta = pauseRect.sizeDelta;
    }

    private static void MatchMainButtonSizes(Parts ui)
    {
        if (ui.shop == null || ui.settingsOpen == null)
            return;

        RectTransform shopRect = (RectTransform)ui.shop.transform;
        RectTransform settingsRect = (RectTransform)ui.settingsOpen.transform;

        // The package's purple blank is 164x50 while orange is 130x50.
        // Preserve Aspect therefore makes two equal RectTransforms LOOK like
        // different-height buttons. Shop is the visual reference requested by
        // the menu layout; only size is copied, never the hand-placed position.
        settingsRect.sizeDelta = shopRect.sizeDelta;

        Image shopImage = ui.shop.GetComponent<Image>();
        Image settingsImage = ui.settingsOpen.GetComponent<Image>();

        if (shopImage != null)
            shopImage.preserveAspect = false;
        if (settingsImage != null)
            settingsImage.preserveAspect = false;
    }

    private static Text Label(Transform parent, string name, string value,
        int fontSize, Vector2 position, Vector2 size, Color color,
        Color outlineColor, float outlineSize)
    {
        GameObject host = UiObject(name, parent);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = host.AddComponent<Text>();
        text.font = AssetDatabase.LoadAssetAtPath<Font>(menuFont);
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        if (outlineSize > .01f)
        {
            Outline outline = host.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(outlineSize, -outlineSize);
            outline.useGraphicAlpha = true;
        }

        return text;
    }

    private static Vector2 PanelPoint(RectTransform panel, Vector2 native,
        float x, float y)
    {
        Vector2 normalized = Px(x, y, native);
        return new Vector2(normalized.x * panel.sizeDelta.x,
            normalized.y * panel.sizeDelta.y);
    }

    private static Text PanelLabel(RectTransform panel, string name, string value,
        int fontSize, Vector2 center, Vector2 nativeSize, Vector2 native)
    {
        Vector2 size = new Vector2(
            nativeSize.x * panel.sizeDelta.x / native.x,
            nativeSize.y * panel.sizeDelta.y / native.y);
        return Label(panel, name, value, fontSize,
            PanelPoint(panel, native, center.x, center.y), size,
            new Color(1f, .91f, .74f), new Color(.03f, .18f, .25f), 3f);
    }

    private static Slider PackageSlider(RectTransform panel, string name,
        Vector2 center, Vector2 nativeSize, Vector2 native)
    {
        GameObject host = UiObject(name, panel);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = PanelPoint(panel, native, center.x, center.y);
        rect.sizeDelta = new Vector2(
            nativeSize.x * panel.sizeDelta.x / native.x,
            nativeSize.y * panel.sizeDelta.y / native.y);

        Image background = host.AddComponent<Image>();
        background.color = new Color(.06f, .25f, .34f, 1f);

        GameObject fillArea = UiObject("Fill Area", rect);
        RectTransform fillAreaRect = (RectTransform)fillArea.transform;
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(14f, 12f);
        fillAreaRect.offsetMax = new Vector2(-14f, -12f);

        GameObject fillHost = UiObject("Fill", fillAreaRect);
        RectTransform fillRect = (RectTransform)fillHost.transform;
        Stretch(fillRect);
        Image fill = fillHost.AddComponent<Image>();
        fill.color = new Color(1f, .65f, .10f, 1f);

        GameObject handleArea = UiObject("Handle Slide Area", rect);
        RectTransform handleAreaRect = (RectTransform)handleArea.transform;
        Stretch(handleAreaRect);
        handleAreaRect.offsetMin = new Vector2(16f, 0f);
        handleAreaRect.offsetMax = new Vector2(-16f, 0f);

        GameObject handleHost = UiObject("Handle", handleAreaRect);
        RectTransform handleRect = (RectTransform)handleHost.transform;
        handleRect.sizeDelta = new Vector2(34f, rect.sizeDelta.y - 8f);
        Image handle = handleHost.AddComponent<Image>();
        handle.color = new Color(.96f, .94f, .82f, 1f);

        Slider slider = host.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        return slider;
    }

    private static Dropdown PackageDropdown(RectTransform panel, string name,
        Vector2 center, Vector2 nativeSize, Vector2 native)
    {
        GameObject host = UiObject(name, panel);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = PanelPoint(panel, native, center.x, center.y);
        rect.sizeDelta = new Vector2(
            nativeSize.x * panel.sizeDelta.x / native.x,
            nativeSize.y * panel.sizeDelta.y / native.y);

        Image background = host.AddComponent<Image>();
        background.color = new Color(.08f, .30f, .40f, 1f);

        Text caption = Label(rect, "Label", "", 34,
            new Vector2(-22f, 0f), new Vector2(rect.sizeDelta.x - 90f,
                rect.sizeDelta.y), new Color(1f, .91f, .74f),
            new Color(.03f, .18f, .25f), 2f);
        caption.alignment = TextAnchor.MiddleCenter;
        Label(rect, "Arrow", "▼", 36,
            new Vector2(rect.sizeDelta.x * .5f - 40f, 1f),
            new Vector2(60f, rect.sizeDelta.y), Color.white,
            new Color(.03f, .18f, .25f), 2f);

        GameObject templateHost = UiObject("Template", rect);
        RectTransform template = (RectTransform)templateHost.transform;
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(.5f, 1f);
        template.anchoredPosition = new Vector2(0f, -rect.sizeDelta.y * .5f);
        template.sizeDelta = new Vector2(0f, 300f);
        Image templateImage = templateHost.AddComponent<Image>();
        templateImage.color = new Color(.04f, .20f, .29f, .98f);
        ScrollRect scroll = templateHost.AddComponent<ScrollRect>();

        GameObject viewportHost = UiObject("Viewport", template);
        RectTransform viewport = (RectTransform)viewportHost.transform;
        Stretch(viewport);
        Image viewportImage = viewportHost.AddComponent<Image>();
        viewportImage.color = Color.white;
        Mask mask = viewportHost.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentHost = UiObject("Content", viewport);
        RectTransform content = (RectTransform)contentHost.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        GameObject itemHost = UiObject("Item", content);
        RectTransform item = (RectTransform)itemHost.transform;
        item.anchorMin = new Vector2(0f, .5f);
        item.anchorMax = new Vector2(1f, .5f);
        item.sizeDelta = new Vector2(0f, 58f);
        Toggle toggle = itemHost.AddComponent<Toggle>();

        GameObject itemBackgroundHost = UiObject("Item Background", item);
        RectTransform itemBackgroundRect =
            (RectTransform)itemBackgroundHost.transform;
        Stretch(itemBackgroundRect);
        Image itemBackground = itemBackgroundHost.AddComponent<Image>();
        itemBackground.color = new Color(.10f, .38f, .48f, 1f);
        toggle.targetGraphic = itemBackground;

        GameObject checkHost = UiObject("Item Checkmark", item);
        RectTransform checkRect = (RectTransform)checkHost.transform;
        checkRect.anchorMin = checkRect.anchorMax = new Vector2(0f, .5f);
        checkRect.anchoredPosition = new Vector2(24f, 0f);
        checkRect.sizeDelta = new Vector2(18f, 18f);
        Image check = checkHost.AddComponent<Image>();
        check.color = new Color(1f, .65f, .10f, 1f);
        toggle.graphic = check;

        Text itemLabel = Label(item, "Item Label", "Option", 28,
            new Vector2(18f, 0f), new Vector2(item.sizeDelta.x - 55f, 58f),
            Color.white, Color.clear, 0f);
        itemLabel.alignment = TextAnchor.MiddleLeft;
        RectTransform itemLabelRect = (RectTransform)itemLabel.transform;
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(46f, 0f);
        itemLabelRect.offsetMax = new Vector2(-10f, 0f);

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;

        Dropdown dropdown = host.AddComponent<Dropdown>();
        dropdown.targetGraphic = background;
        dropdown.template = template;
        dropdown.captionText = caption;
        dropdown.itemText = itemLabel;
        templateHost.SetActive(false);
        return dropdown;
    }

    private static Button AnchoredTextButton(Transform parent, string name,
        string value, string path, Vector2 position, float width, int fontSize)
    {
        Button button = AnchoredSpriteButton(parent, name, path, position, width);
        RectTransform rect = (RectTransform)button.transform;
        Label(rect, "Label", value, fontSize, Vector2.zero, rect.sizeDelta,
            Color.white, new Color(.03f, .18f, .25f), 2f);
        return button;
    }

    private static Image SpriteImage(RectTransform panel, string name, string path,
        Vector2 normalizedPosition, Vector2 nativePanel, float forcedWidth = 0f)
    {
        Sprite sprite = Load(path);
        GameObject host = UiObject(name, panel);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(
            normalizedPosition.x * panel.sizeDelta.x,
            normalizedPosition.y * panel.sizeDelta.y);

        float scale = panel.sizeDelta.x / nativePanel.x;
        float width = forcedWidth > .01f ? forcedWidth : sprite.rect.width * scale;
        rect.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);

        Image image = host.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = true;
        return image;
    }

    private static Button Hotspot(RectTransform panel, string name, Vector2 centerPx,
        Vector2 sizePx, Vector2 nativePanel)
    {
        GameObject host = UiObject(name, panel);
        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        Vector2 normalized = Px(centerPx.x, centerPx.y, nativePanel);
        rect.anchoredPosition = new Vector2(normalized.x * panel.sizeDelta.x,
            normalized.y * panel.sizeDelta.y);
        rect.sizeDelta = new Vector2(sizePx.x * panel.sizeDelta.x / nativePanel.x,
            sizePx.y * panel.sizeDelta.y / nativePanel.y);

        Image hit = host.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, .001f);
        hit.raycastTarget = true;

        Button button = host.AddComponent<Button>();
        button.targetGraphic = hit;
        return button;
    }

    private static Button FloatingButton(GameObject screen, string name, string path,
        Vector2 inset, TextAnchor anchor, float width)
    {
        Sprite sprite = Load(path);
        GameObject host = UiObject(name, screen.transform);
        RectTransform rect = (RectTransform)host.transform;
        rect.pivot = Pivot(anchor);
        rect.anchorMin = rect.anchorMax = Pivot(anchor);
        rect.anchoredPosition = inset;
        rect.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);

        Image image = host.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        Button button = host.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Button FloatingTextButton(GameObject screen, string name,
        string value, Vector2 inset, TextAnchor anchor, float width)
    {
        return FloatingLocalizedButton(screen, name, value, quitButtonArt,
            inset, anchor, width, 38);
    }

    private static Button FloatingLocalizedButton(GameObject screen, string name,
        string value, string art, Vector2 inset, TextAnchor anchor, float width,
        int fontSize)
    {
        Button button = FloatingButton(screen, name, art, inset, anchor, width);
        RectTransform rect = (RectTransform)button.transform;

        Label(button.transform, "Label", value, fontSize, Vector2.zero,
            rect.sizeDelta, Color.white, new Color(.35f, .03f, .05f), 2f);
        return button;
    }

    private static Vector2 Pivot(TextAnchor anchor)
    {
        if (anchor == TextAnchor.UpperLeft)
            return new Vector2(0f, 1f);
        if (anchor == TextAnchor.UpperRight)
            return new Vector2(1f, 1f);
        return new Vector2(.5f, .5f);
    }

    private static Vector2 Px(float x, float y, Vector2 native)
    {
        return new Vector2(x / native.x - .5f, .5f - y / native.y);
    }

    private static GameObject UiObject(string name, Transform parent)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));
        host.layer = 5;
        host.transform.SetParent(parent, false);
        return host;
    }

    private static Sprite Load(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Wire(HyperCasualGameMenu menu, GameScreens screens, Parts ui)
    {
        SerializedObject so = new SerializedObject(menu);
        Set(so, "screens", screens);
        Set(so, "mainMenu", ui.main);
        Set(so, "pauseMenu", ui.pause);
        Set(so, "settingsMenu", ui.settings);
        Set(so, "characterMenu", ui.character);
        Set(so, "quitMenu", ui.quit);
        Set(so, "gameButtons", ui.hud);
        Set(so, "skinPreview", ui.skinPreview);
        Set(so, "playButton", ui.play);
        Set(so, "characterButton", ui.shop);
        Set(so, "settingsButton", ui.settingsOpen);
        Set(so, "quitButton", ui.quitOpen);
        Set(so, "pauseButton", ui.pauseOpen);
        Set(so, "quickQuitButton", ui.quickQuit);
        Set(so, "retryButton", ui.retry);
        Set(so, "resumeButton", ui.resume);
        Set(so, "pauseSettingsButton", ui.pauseSettings);
        Set(so, "homeButton", ui.home);
        Set(so, "pauseQuitButton", ui.pauseQuit);
        Set(so, "soundButton", ui.sound);
        Set(so, "musicButton", ui.music);
        Set(so, "settingsOkButton", ui.settingsOk);
        Set(so, "soundToggle", ui.soundImage);
        Set(so, "musicToggle", ui.musicImage);
        Set(so, "toggleOn", Load(toggles + "Toggle_ON.png"));
        Set(so, "toggleOff", Load(toggles + "Toggle_Off.png"));
        Set(so, "effectsSlider", ui.effectsSlider);
        Set(so, "musicSlider", ui.musicSlider);
        Set(so, "qualityDropdown", ui.qualityDropdown);
        Set(so, "languageDropdown", ui.languageDropdown);
        Set(so, "chooseCharacterButton", ui.choose);
        Set(so, "characterBackButton", ui.characterBack);
        Set(so, "confirmQuitButton", ui.quitYes);
        Set(so, "cancelQuitButton", ui.quitCancel);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireSkinPreview(CharacterSkinPreview preview,
        RawImage display, Text name, Button previous, Button next)
    {
        SerializedObject so = new SerializedObject(preview);
        Set(so, "display", display);
        Set(so, "skinName", name);
        Set(so, "previousButton", previous);
        Set(so, "nextButton", next);
        Set(so, "sharedAvatar", AvatarIn(avatarSource));
        Set(so, "previewController",
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(playerController));

        SerializedProperty names = so.FindProperty("skinNames");
        names.arraySize = skinNames.Length;

        SerializedProperty models = so.FindProperty("skinModels");
        models.arraySize = skinNames.Length;

        for (int i = 0; i < skinNames.Length; i++)
        {
            names.GetArrayElementAtIndex(i).stringValue = skinNames[i];
            models.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    SkinPrefab(skinNames[i]));
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string SkinPrefab(string animal)
    {
        return animalPrefabFolder + "DGN_" + animal + "_Outline Variant.prefab";
    }

    private static Avatar AvatarIn(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Avatar avatar)
                return avatar;
        }

        return null;
    }

    private static void WireGameScreens(GameScreens screens, GameObject main)
    {
        SerializedObject so = new SerializedObject(screens);
        Set(so, "startPanel", main);
        Set(so, "playButton", null);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(screens);
    }

    private static void Set(SerializedObject so, string property, Object value)
    {
        SerializedProperty found = so.FindProperty(property);
        if (found != null)
            found.objectReferenceValue = value;
    }
}
#endif
