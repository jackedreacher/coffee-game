#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Puts co-op on the front menu: a second slot beside your character, with a
// plus sign in it until somebody is standing there.
//
// Additive on purpose. It creates two objects and touches nothing that was
// already in the scene -- the menu, the wardrobe, the buttons and the hand
// placed layout are all left exactly as they are. Run it twice and it rebuilds
// its own two objects and only those.
public static class CoopMenuSetup
{
    private const string canvasName = "Hyper Casual GUI";
    private const string mainName = "Main Menu";
    private const string slotName = "Coop Mate";

    private const string frameArt =
        "Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/lightgrey.png";
    private const string menuFont =
        "Assets/Hyper_Casual_UI/Fonts/Baloo2-ExtraBold.ttf";

    private static readonly Color cream = new Color(1f, .88f, .58f);
    private static readonly Color ink = new Color(.20f, .035f, .035f);

    [MenuItem("Cooked Fast/Online/4 - Ana Menuye Ekle", priority = 243)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Online",
                "Play modundayken calismaz. Once durdur.", "Tamam");

            return;
        }

        GameObject canvas = GameObject.Find(canvasName);

        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Online",
                "Bu sahnede " + canvasName + " yok.\n\n" +
                "Once: Cooked Fast > GUI > Hyper Casual GUI Kur", "Tamam");

            return;
        }

        Transform main = canvas.transform.Find(mainName);

        if (main == null)
        {
            EditorUtility.DisplayDialog("Online",
                canvasName + " icinde " + mainName + " bulunamadi.\n\n" +
                "Menu elle yeniden adlandirilmis olabilir.", "Tamam");

            return;
        }

        string report = "";

        // The player prefab first. Connecting from the menu spawns it exactly
        // like the test room does, and it is the badge riding on it that tells
        // this menu which animal the other player picked -- so a menu built
        // against an older prefab is a plus sign that never turns into anybody
        CoopTestSetup.Prefab(ref report);

        // Rebuilt rather than patched, both of them. These two objects are
        // generated, and a half updated generated object is a shape nobody
        // authored and nobody can debug
        Wipe(canvas.transform, CoopPanels.rootName, ref report);
        Wipe(main, slotName, ref report);

        CoopMenu menu = CoopPanels.Build(canvas.transform, true);

        // Off. The online screens are a place you go, not the first thing you
        // see -- and left on at edit time they cover the whole menu in the Game
        // view with no way to tell what is underneath
        menu.gameObject.SetActive(false);

        report += "- Online ekranlari kuruldu (kapali)\n";

        GameObject slot = Slot(main, ref report);

        Wire(slot, menu.gameObject, ref report);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        report += "\nARTI'ya basinca online ekrani aciliyor. Ikinci oyuncu\n" +
                  "baglanir baglanmaz ekran kapaniyor ve o karenin icinde\n" +
                  "onun sectigi hayvan duruyor.\n\n" +
                  "Kare sag tarafta, Shop'un altinda. Yeri begenmezsen\n" +
                  "Hierarchy'den surukle, sonra:\n" +
                  "Cooked Fast > GUI > Mevcut GUI Duzenini Kaydet";

        Debug.Log("Online ana menu\n" + report);
        EditorUtility.DisplayDialog("Online ana menu", report, "Tamam");
    }

    private static void Wipe(Transform parent, string name, ref string report)
    {
        Transform old = parent.Find(name);

        if (old == null)
            return;

        Undo.DestroyObjectImmediate(old.gameObject);

        report += "- Eski " + name + " silindi\n";
    }

    // ---- the square beside your character -----------------------------------

    private static GameObject Slot(Transform main, ref string report)
    {
        GameObject slot = Ui(slotName, main);

        RectTransform rect = (RectTransform)slot.transform;

        // Right hand column, under Shop. The character preview owns the left
        // side of this menu and the mode card owns the bottom; this is the one
        // rectangle that is free at any phone aspect
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(300f, -140f);
        rect.sizeDelta = new Vector2(340f, 430f);

        // ---- empty: the plus ----
        GameObject add = Ui("Add", slot.transform);

        Stretch((RectTransform)add.transform);

        GameObject frameHost = Ui("Frame", add.transform);

        Stretch((RectTransform)frameHost.transform);

        Image frame = frameHost.AddComponent<Image>();

        frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(frameArt);
        frame.type = Image.Type.Sliced;
        frame.color = new Color(1f, 1f, 1f, .55f);

        Label(add.transform, "Plus", "+", 150, new Vector2(0f, 40f),
            new Vector2(300f, 220f), Color.white);
        Label(add.transform, "Add Label", "ARKADAS EKLE", 30,
            new Vector2(0f, -140f), new Vector2(320f, 70f), cream);

        // ---- filled: the other player ----
        GameObject shot = Ui("Mate 3D", slot.transform);

        Stretch((RectTransform)shot.transform);

        RawImage display = shot.AddComponent<RawImage>();

        display.color = Color.white;

        // Left ON, so the portrait itself is the way back to the co-op screen
        // once it has closed. Without it there is no route to the leave button
        // short of restarting the game
        display.raycastTarget = true;
        display.enabled = false;

        Label(slot.transform, "Mate Name", "", 34,
            new Vector2(0f, -240f), new Vector2(340f, 70f), cream);

        Button press = slot.AddComponent<Button>();

        press.targetGraphic = frame;

        report += "- " + slotName + " karesi kuruldu (Main Menu icinde)\n";

        return slot;
    }

    // ---- wiring -------------------------------------------------------------

    private static void Wire(GameObject slot, GameObject online, ref string report)
    {
        CoopMateSlot mate = slot.AddComponent<CoopMateSlot>();

        CharacterSkinPreview wardrobe =
            Object.FindFirstObjectByType<CharacterSkinPreview>(FindObjectsInactive.Include);

        SerializedObject so = new SerializedObject(mate);

        so.FindProperty("wardrobe").objectReferenceValue = wardrobe;
        so.FindProperty("onlineScreen").objectReferenceValue = online;
        so.FindProperty("display").objectReferenceValue = Find<RawImage>(slot, "Mate 3D");
        so.FindProperty("addRoot").objectReferenceValue = slot.transform.Find("Add").gameObject;
        so.FindProperty("addButton").objectReferenceValue = slot.GetComponent<Button>();
        so.FindProperty("label").objectReferenceValue = Find<Text>(slot, "Mate Name");

        so.ApplyModifiedProperties();

        report += wardrobe != null
            ? "- Hayvan listesi vitrinden aliniyor, ikinci kopya yok\n"
            : "- UYARI: CharacterSkinPreview bulunamadi, kare bos kalir\n";
    }

    // ---- pieces -------------------------------------------------------------

    private static GameObject Ui(string name, Transform parent)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));

        host.layer = 5;
        host.transform.SetParent(parent, false);

        Undo.RegisterCreatedObjectUndo(host, "Create " + name);

        return host;
    }

    private static Text Label(Transform parent, string name, string value,
        int size, Vector2 position, Vector2 box, Color colour)
    {
        GameObject host = Ui(name, parent);

        RectTransform rect = (RectTransform)host.transform;

        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;

        Text text = host.AddComponent<Text>();

        text.font = AssetDatabase.LoadAssetAtPath<Font>(menuFont);
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = colour;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // Off, so the words never eat the press meant for the square they sit
        // on. The button is the whole slot, not the frame behind the text
        text.raycastTarget = false;

        Outline outline = host.AddComponent<Outline>();

        outline.effectColor = ink;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;

        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T Find<T>(GameObject root, string name) where T : Component
    {
        foreach (T found in root.GetComponentsInChildren<T>(true))
            if (found.gameObject.name == name)
                return found;

        return null;
    }
}
#endif
