#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds the start screen, the round banner and the game over screen.
//
// Its own canvas, above everything else the game draws. These three are the
// only UI that has to be in front of the HUD -- a "you died" behind the hearts
// is a screen nobody sees
public static class GameScreensSetup
{
    private const string rootName = "Game Screens";

    private static readonly Color pink = new Color(.91f, .12f, .39f);
    private static readonly Color dim = new Color(.05f, .04f, .08f, .82f);
    private static readonly Color cream = new Color(.99f, .96f, .90f);
    private static readonly Color gold = new Color(.85f, .55f, .11f);

    [MenuItem("Cooked Fast/Oyun/Ekran: Basla ve Oldun Ekranlarini Kur", priority = 232)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Ekranlar",
                "Play modundayken calismaz. Once durdur.", "Tamam");
            return;
        }

        string report = "";

        EnsureEventSystem(ref report);

        GameObject root = Root(ref report);

        GameObject start = StartPanel(root);
        GameObject round = RoundPanel(root);
        GameObject over = OverPanel(root);

        // Switched off as they are built, all three of them.
        //
        // Which one is up is decided in Awake, and Awake does not run while the
        // scene is only being edited -- so a command that left them on left
        // three full screen panels stacked over the Game view, and the kitchen
        // could not be seen at all. Awake turns the start screen back on the
        // moment play begins
        start.SetActive(false);
        round.SetActive(false);
        over.SetActive(false);

        Wire(root, start, round, over, ref report);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Ekranlar\n" + report);
        EditorUtility.DisplayDialog("Ekranlar", report, "Tamam");
    }

    private static void EnsureEventSystem(ref string report)
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject events = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(events, "Create EventSystem");

        events.AddComponent<EventSystem>();
        events.AddComponent<StandaloneInputModule>();

        report += "- EventSystem yoktu, kuruldu (yoksa tuslar calismaz)\n";
    }

    private static GameObject Root(ref string report)
    {
        GameObject existing = GameObject.Find(rootName);

        if (existing != null)
        {
            // Rebuilt from scratch rather than patched. These panels are
            // generated, so a half updated one is a shape nobody authored
            Undo.DestroyObjectImmediate(existing);
            report += "- Eski " + rootName + " silindi ve yeniden kuruldu\n";
        }

        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create " + rootName);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above the HUD, below the money flights. A "you died" behind the
        // hearts is a screen nobody sees
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);

        // Portrait, so height is what the layout is measured against
        scaler.matchWidthOrHeight = 1f;

        root.AddComponent<GraphicRaycaster>();

        return root;
    }

    // ---- the three panels ---------------------------------------------------

    private static GameObject StartPanel(GameObject root)
    {
        GameObject panel = Panel(root, "Start", dim);

        Label(panel, "Title", "COOKED FAST", 140f, cream, new Vector2(0f, 420f), 900f);
        Label(panel, "Hint", "Musteriye tikla, siparisini ver", 46f, cream,
            new Vector2(0f, 280f), 900f);

        MakeButton(panel, "Play", "OYNA", new Vector2(0f, -60f));

        return panel;
    }

    // No dim behind it: the point of "get ready" is that you can see what you
    // are getting ready for
    private static GameObject RoundPanel(GameObject root)
    {
        GameObject panel = Panel(root, "Round", Color.clear);

        GameObject bar = Block(panel, "Bar", pink, new Vector2(0f, 60f), new Vector2(1100f, 210f));

        Label(bar, "Ready", "HAZIRLAN", 120f, Color.white, new Vector2(0f, 34f), 1000f);
        Label(bar, "Round", "ROUND 1", 66f, gold, new Vector2(0f, -62f), 1000f);

        return panel;
    }

    private static GameObject OverPanel(GameObject root)
    {
        GameObject panel = Panel(root, "Over", dim);

        Label(panel, "Title", "OLDUN", 150f, cream, new Vector2(0f, 380f), 900f);
        Label(panel, "Detail", "Canlarin bitti", 52f, cream, new Vector2(0f, 200f), 900f);

        MakeButton(panel, "Restart", "TEKRAR DENE", new Vector2(0f, -60f));

        return panel;
    }

    // ---- pieces -------------------------------------------------------------

    private static GameObject Panel(GameObject parent, string name, Color colour)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent.transform, false);

        RectTransform rect = (RectTransform)panel.transform;

        Stretch(rect);

        // A clear panel still needs an Image on the two that dim, and must NOT
        // have one on the banner -- an invisible full screen graphic swallows
        // every tap meant for the kitchen behind it
        if (colour.a > .001f)
        {
            Image sheet = panel.AddComponent<Image>();
            sheet.color = colour;
        }

        return panel;
    }

    private static GameObject Block(GameObject parent, string name, Color colour,
        Vector2 position, Vector2 size)
    {
        GameObject block = new GameObject(name, typeof(RectTransform));
        block.transform.SetParent(parent.transform, false);

        RectTransform rect = (RectTransform)block.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image fill = block.AddComponent<Image>();
        fill.color = colour;
        fill.raycastTarget = false;

        return block;
    }

    private static TextMeshProUGUI Label(GameObject parent, string name, string text,
        float size, Color colour, Vector2 position, float width)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));
        host.transform.SetParent(parent.transform, false);

        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, size * 2.2f);

        TextMeshProUGUI label = host.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = colour;
        label.alignment = TextAlignmentOptions.Center;

        // Off on purpose. A label is something to read, not something to press,
        // and a raycast target stretched over a button eats the press
        label.raycastTarget = false;

        return label;
    }

    // Not called Button. A method sharing its name with the type it returns
    // shadows that type everywhere inside this class -- AddComponent<Button>
    // and Find<Button> both stop compiling, and one error here keeps every new
    // menu item in the project from ever registering
    private static Button MakeButton(GameObject parent, string name, string text, Vector2 position)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));
        host.transform.SetParent(parent.transform, false);

        RectTransform rect = (RectTransform)host.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;

        // Big. This is a phone and the finger is not a mouse pointer
        rect.sizeDelta = new Vector2(640f, 190f);

        Image face = host.AddComponent<Image>();
        face.color = pink;

        Button button = host.AddComponent<Button>();
        button.targetGraphic = face;

        Label(host, "Label", text, 68f, Color.white, Vector2.zero, 600f);

        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---- wiring -------------------------------------------------------------

    private static void Wire(GameObject root, GameObject start, GameObject round,
        GameObject over, ref string report)
    {
        GameScreens screens = Undo.AddComponent<GameScreens>(root);

        SerializedObject so = new SerializedObject(screens);

        so.FindProperty("startPanel").objectReferenceValue = start;
        so.FindProperty("roundPanel").objectReferenceValue = round;
        so.FindProperty("overPanel").objectReferenceValue = over;

        so.FindProperty("readyLabel").objectReferenceValue = Find<TextMeshProUGUI>(round, "Ready");
        so.FindProperty("roundLabel").objectReferenceValue = Find<TextMeshProUGUI>(round, "Round");
        so.FindProperty("overTitle").objectReferenceValue = Find<TextMeshProUGUI>(over, "Title");
        so.FindProperty("overDetail").objectReferenceValue = Find<TextMeshProUGUI>(over, "Detail");

        so.FindProperty("playButton").objectReferenceValue = Find<Button>(start, "Play");
        so.FindProperty("restartButton").objectReferenceValue = Find<Button>(over, "Restart");

        so.ApplyModifiedProperties();

        report += "- Basla, Hazirlan ve Oldun ekranlari kuruldu\n";

        if (Object.FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include) == null)
            report += "- UYARI: RoundManager yok, HAZIRLAN ekrani hic cikmaz\n" +
                      "  Cooked Fast > Raund: 50 Raundu Uret\n";

        if (Object.FindFirstObjectByType<Lives>(FindObjectsInactive.Include) == null)
            report += "- UYARI: Lives yok, OLDUN ekrani hic cikmaz\n" +
                      "  Cooked Fast > Can: Slotlari Kur\n";

        report += "\nOyun artik OYNA'ya basilana kadar duruyor: raundlar bekliyor,\n" +
                  "zaman durmus halde basliyor.\n\n" +
                  "Uc panel de KAPALI kuruldu. Edit modunda hangisinin acik olacagina\n" +
                  "kimse karar vermiyor, o yuzden acik birakilirlarsa ucu birden\n" +
                  "Game view'i kaplar. Play'e basinca Awake dogru olani aciyor.\n\n" +
                  "Elle bakmak istersen Hierarchy'den ac, kapatmayi unutsan da\n" +
                  "Play sirasinda kendini duzeltir.";
    }

    private static T Find<T>(GameObject root, string name) where T : Component
    {
        foreach (T found in root.GetComponentsInChildren<T>(true))
        {
            if (found.gameObject.name == name)
                return found;
        }

        return null;
    }
}
#endif
