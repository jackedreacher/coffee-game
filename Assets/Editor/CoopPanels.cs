#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The six online screens, built once and dropped wherever they are needed.
//
// Shared rather than copied because there are two places that want them: the
// throwaway test room, and the real main menu. Two copies of a screen is two
// screens that drift apart, and the one nobody is looking at is always the one
// that stops matching the code that drives it.
public static class CoopPanels
{
    public const string rootName = "Online";

    private static readonly Color pink = new Color(.91f, .12f, .39f);
    private static readonly Color dim = new Color(.05f, .04f, .08f, .92f);
    private static readonly Color cream = new Color(.99f, .96f, .90f);
    private static readonly Color gold = new Color(.85f, .55f, .11f);
    private static readonly Color grey = new Color(.22f, .22f, .26f);

    // withBack: the main menu has somewhere to go back TO. The test room does
    // not -- a back button there hides the only thing on the screen and leaves
    // an empty floor with no way to return
    public static CoopMenu Build(Transform parent, bool withBack)
    {
        GameObject root = new GameObject(rootName, typeof(RectTransform));

        root.transform.SetParent(parent, false);

        Stretch((RectTransform)root.transform);

        GameObject choice = Panel(root, "Choice", dim);

        Label(choice, "Title", "ONLINE CO-OP", 110f, cream, new Vector2(0f, 520f), 950f);
        Label(choice, "Hint", "Iki kisi, ayni mutfak", 46f, gold, new Vector2(0f, 400f), 950f);

        MakeButton(choice, "Host", "ODA KUR", new Vector2(0f, 120f), pink);
        MakeButton(choice, "OpenJoin", "KODLA KATIL", new Vector2(0f, -110f), grey);

        if (withBack)
            MakeButton(choice, "Back", "GERI", new Vector2(0f, -340f), grey);

        GameObject hosting = Panel(root, "Host", dim);

        Label(hosting, "Title", "ODA KODU", 80f, cream, new Vector2(0f, 520f), 950f);
        Label(hosting, "Code", "...", 150f, gold, new Vector2(0f, 330f), 1000f);
        Label(hosting, "Players", "0/2 oyuncu", 46f, cream, new Vector2(0f, 180f), 950f);

        MakeButton(hosting, "Copy", "KODU KOPYALA", new Vector2(0f, -40f), pink);
        MakeButton(hosting, "CancelHost", "IPTAL", new Vector2(0f, -270f), grey);

        GameObject joining = Panel(root, "Join", dim);

        Label(joining, "Title", "KODU YAZ", 80f, cream, new Vector2(0f, 520f), 950f);

        TMP_InputField field = Field(joining, "Code", new Vector2(0f, 330f));

        MakeButton(joining, "Paste", "YAPISTIR", new Vector2(0f, 150f), grey);
        MakeButton(joining, "Join", "KATIL", new Vector2(0f, -70f), pink);
        MakeButton(joining, "CancelJoin", "IPTAL", new Vector2(0f, -300f), grey);

        GameObject busy = Panel(root, "Busy", dim);

        Label(busy, "Busy", "Baglaniliyor...", 70f, cream, Vector2.zero, 950f);

        GameObject failed = Panel(root, "Error", dim);

        Label(failed, "Title", "OLMADI", 90f, pink, new Vector2(0f, 420f), 950f);
        Label(failed, "Message", "", 46f, cream, new Vector2(0f, 180f), 900f);

        MakeButton(failed, "Ok", "TAMAM", new Vector2(0f, -120f), pink);

        // No dim on this one. Once both players are in, the screens are in the
        // way of the only thing worth looking at
        GameObject playing = Panel(root, "Game", Color.clear);

        Corner(playing, "Leave", "AYRIL");

        CoopMenu menu = Wire(root, choice, hosting, joining, busy, failed,
            playing, field, withBack);

        choice.SetActive(true);
        hosting.SetActive(false);
        joining.SetActive(false);
        busy.SetActive(false);
        failed.SetActive(false);
        playing.SetActive(false);

        return menu;
    }

    // ---- pieces -------------------------------------------------------------

    private static GameObject Panel(GameObject parent, string name, Color colour)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));

        panel.transform.SetParent(parent.transform, false);

        Stretch((RectTransform)panel.transform);

        // A clear panel gets no Image at all. An invisible full screen graphic
        // swallows every tap meant for whatever is behind it
        if (colour.a > .001f)
        {
            Image sheet = panel.AddComponent<Image>();

            sheet.color = colour;
        }

        return panel;
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
        rect.sizeDelta = new Vector2(width, size * 3.2f);

        TextMeshProUGUI label = host.AddComponent<TextMeshProUGUI>();

        label.text = text;
        label.fontSize = size;
        label.color = colour;
        label.alignment = TextAlignmentOptions.Center;

        // A label is something to read, not something to press. Left on, one
        // stretched over a button eats the press
        label.raycastTarget = false;

        return label;
    }

    // Not called Button. A method sharing its name with the type it returns
    // shadows that type for the whole class, and AddComponent<Button> stops
    // compiling -- one error here and no menu item in the project registers
    private static Button MakeButton(GameObject parent, string name, string text,
        Vector2 position, Color colour)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));

        host.transform.SetParent(parent.transform, false);

        RectTransform rect = (RectTransform)host.transform;

        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(700f, 180f);

        Image face = host.AddComponent<Image>();

        face.color = colour;

        Button button = host.AddComponent<Button>();

        button.targetGraphic = face;

        Label(host, "Label", text, 62f, Color.white, Vector2.zero, 660f);

        return button;
    }

    private static Button Corner(GameObject parent, string name, string text)
    {
        Button button = MakeButton(parent, name, text, Vector2.zero, grey);

        RectTransform rect = (RectTransform)button.transform;

        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(300f, 120f);

        return button;
    }

    // Built by hand rather than from the menu template, because the template
    // lives in a package this project does not have to have. Four objects: the
    // frame, a mask, the text that is typed and the text shown when nothing has
    // been -- a field missing any one of them silently refuses to take input
    private static TMP_InputField Field(GameObject parent, string name, Vector2 position)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));

        host.transform.SetParent(parent.transform, false);

        RectTransform rect = (RectTransform)host.transform;

        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(760f, 170f);

        Image face = host.AddComponent<Image>();

        face.color = cream;

        GameObject area = new GameObject("Text Area", typeof(RectTransform));

        area.transform.SetParent(host.transform, false);

        RectTransform areaRect = (RectTransform)area.transform;

        Stretch(areaRect);

        areaRect.offsetMin = new Vector2(24f, 12f);
        areaRect.offsetMax = new Vector2(-24f, -12f);

        area.AddComponent<RectMask2D>();

        TextMeshProUGUI typed = Inside(area, "Text", "", grey);
        TextMeshProUGUI hint = Inside(area, "Placeholder", "KOD", new Color(.45f, .45f, .5f));

        TMP_InputField field = host.AddComponent<TMP_InputField>();

        field.targetGraphic = face;
        field.textViewport = areaRect;
        field.textComponent = typed;
        field.placeholder = hint;
        field.characterLimit = 16;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.onFocusSelectAll = true;
        field.text = "";

        return field;
    }

    private static TextMeshProUGUI Inside(GameObject parent, string name, string text,
        Color colour)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));

        host.transform.SetParent(parent.transform, false);

        Stretch((RectTransform)host.transform);

        TextMeshProUGUI label = host.AddComponent<TextMeshProUGUI>();

        label.text = text;
        label.fontSize = 72f;
        label.color = colour;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = false;

        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---- wiring -------------------------------------------------------------

    private static CoopMenu Wire(GameObject root, GameObject choice, GameObject hosting,
        GameObject joining, GameObject busy, GameObject failed, GameObject playing,
        TMP_InputField field, bool withBack)
    {
        CoopMenu menu = root.AddComponent<CoopMenu>();

        SerializedObject so = new SerializedObject(menu);

        so.FindProperty("choicePanel").objectReferenceValue = choice;
        so.FindProperty("hostPanel").objectReferenceValue = hosting;
        so.FindProperty("joinPanel").objectReferenceValue = joining;
        so.FindProperty("busyPanel").objectReferenceValue = busy;
        so.FindProperty("errorPanel").objectReferenceValue = failed;
        so.FindProperty("gamePanel").objectReferenceValue = playing;

        so.FindProperty("hostButton").objectReferenceValue = Find<Button>(choice, "Host");
        so.FindProperty("openJoinButton").objectReferenceValue = Find<Button>(choice, "OpenJoin");

        if (withBack)
            so.FindProperty("backButton").objectReferenceValue = Find<Button>(choice, "Back");

        so.FindProperty("codeLabel").objectReferenceValue = Find<TextMeshProUGUI>(hosting, "Code");
        so.FindProperty("playersLabel").objectReferenceValue = Find<TextMeshProUGUI>(hosting, "Players");
        so.FindProperty("copyButton").objectReferenceValue = Find<Button>(hosting, "Copy");
        so.FindProperty("cancelHostButton").objectReferenceValue = Find<Button>(hosting, "CancelHost");

        so.FindProperty("codeInput").objectReferenceValue = field;
        so.FindProperty("joinButton").objectReferenceValue = Find<Button>(joining, "Join");
        so.FindProperty("pasteButton").objectReferenceValue = Find<Button>(joining, "Paste");
        so.FindProperty("cancelJoinButton").objectReferenceValue = Find<Button>(joining, "CancelJoin");

        so.FindProperty("busyLabel").objectReferenceValue = Find<TextMeshProUGUI>(busy, "Busy");
        so.FindProperty("errorLabel").objectReferenceValue = Find<TextMeshProUGUI>(failed, "Message");
        so.FindProperty("errorOkButton").objectReferenceValue = Find<Button>(failed, "Ok");

        so.FindProperty("leaveButton").objectReferenceValue = Find<Button>(playing, "Leave");

        so.ApplyModifiedProperties();

        return menu;
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
