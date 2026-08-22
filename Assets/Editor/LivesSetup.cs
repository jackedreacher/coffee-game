using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

// Builds the row of hearts at the top of the screen, and the counter behind it.
//
// A screen space canvas, which is not the thing this project keeps away from.
// The rule is about WORLD space canvases: their scale, their facing and their
// draw order are all decided elsewhere and every one of them fails without
// saying so. An overlay has none of those questions -- it is at the top of the
// screen because it is at the top of the screen -- and hearts are Images with
// no font to fail to resolve
public static class LivesSetup
{
    private const string iconFolder = "Assets/Layer Lab/2D Icons-PictoIconPack01/Icons/PictoIcon_128";

    private const string fullPath = iconFolder + "/Icon_PictoIcon_Heart.Png";
    private const string emptyPath = iconFolder + "/Icon_PictoIcon_Heart_Empty.Png";
    private const string brokenPath = "Assets/Tiny Coffee Shop/Sprites/UI/BrokenHeart.png";
    private const string sunburstPath = "Assets/food-icons/Sunburst.png";

    private const string rootName = "Lives HUD";

    private const int slots = 3;

    // The pack's heart is white artwork, so red is a tint, not a second file
    private static readonly Color heartColour = new Color(.91f, .26f, .30f);
    private static readonly Color lostColour = new Color(.32f, .27f, .29f, .5f);
    private static readonly Color brokenColour = new Color(.38f, .38f, .42f, 1f);

    // Screen pixels. The canvas is scaled by the reference resolution below, so
    // these hold their proportions on any phone
    private const float heartSize = 68f;
    private const float heartGap = 10f;

    // Clear of the notch, and no further. A hundred and seventy put the card
    // safely below the cutout and also well down into the kitchen; this is as
    // far up as it goes while still missing a cutout on a tall screen.
    //
    // The number to raise if a phone with a punch-hole camera clips the hearts
    private const float topMargin = 62f;

    [MenuItem("Cooked Fast/Oyun/Can: Slotlari Kur", priority = 220)]
    public static void Setup()
    {
        // Scene edits made while the game is running are thrown away the moment
        // it stops, and the command would report success either way
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Can Slotlari",
                "Oyun calisiyor. Play'den cik ve tekrar dene --\n" +
                "oyun sirasinda yapilan sahne degisiklikleri kayboluyor.", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        FoodIconBaker.MakeSprite(fullPath);
        FoodIconBaker.MakeSprite(emptyPath);
        FoodIconBaker.MakeSprite(brokenPath);
        FoodIconBaker.MakeSprite(sunburstPath);

        AssetDatabase.Refresh();

        Sprite full = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
        Sprite empty = AssetDatabase.LoadAssetAtPath<Sprite>(emptyPath);
        Sprite broken = AssetDatabase.LoadAssetAtPath<Sprite>(brokenPath);
        Sprite sunburst = AssetDatabase.LoadAssetAtPath<Sprite>(sunburstPath);

        report.AppendLine("Ikonlar");
        report.AppendLine("  dolu: " + (full == null ? "BULUNAMADI " + fullPath : full.name));
        report.AppendLine("  bos : " + (empty == null ? "BULUNAMADI " + emptyPath : empty.name));
        report.AppendLine("  kirik: " + (broken == null ? "BULUNAMADI " + brokenPath : broken.name));
        report.AppendLine("  patlama: " + (sunburst == null ? "BULUNAMADI " + sunburstPath : sunburst.name));
        report.AppendLine();

        if (full == null)
        {
            EditorUtility.DisplayDialog("Can Slotlari", report.ToString(), "Tamam");
            return;
        }

        // The canvas the money is already drawn on, not a new one.
        //
        // The hearts did not show up last time and there was no way to tell
        // which of the six reasons it was. This removes the question: the money
        // counter is on screen right now, so whatever canvas it belongs to
        // works -- settings, scaler, sort order, camera, all of it proven
        CurrencyText money = PickMoney(report);

        // GetComponentInParent skips inactive objects, so a counter switched off
        // in the hierarchy comes back with no canvas at all -- and this used to
        // walk straight into that null on the next line
        Canvas canvas = money == null ? null : money.GetComponentInParent<Canvas>(true);

        if (canvas == null)
            canvas = FindOverlayCanvas(report);

        report.AppendLine(money == null
            ? "CurrencyText BULUNAMADI -- kartin ortasi bos kalacak"
            : "Para sayaci: " + money.name);

        report.AppendLine("Canvas: " + canvas.name +
                          "  (" + canvas.renderMode + ", sort " + canvas.sortingOrder + ")");

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null)
            report.AppendLine("  DIKKAT: bu Canvas'in kamerasi yok, hicbir sey cizilmez");

        report.AppendLine();

        // Where the last card was left standing, if anybody moved it.
        //
        // Re-running this used to put the row back at the number written in this
        // file, so a card nudged down by hand in the Hierarchy jumped back the
        // next time the HUD was rebuilt for some entirely unrelated reason. How
        // high the hearts sit is a thing decided by looking at the screen, and
        // the Inspector is where that gets decided -- this keeps the decision
        float placed = float.NaN;

        // Rebuilt rather than added to, so running this twice does not leave two
        // rows of hearts stacked on each other.
        //
        // Searched across the whole scene rather than among this canvas's direct
        // children: an older run may have put it somewhere else, and two HUDs
        // both drawing the same count is worse than none
        foreach (LivesHud old in Object.FindObjectsByType<LivesHud>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Only when it was hung off the top edge, which is what the new one
            // will be. Against any other anchor the same number means a distance
            // from somewhere else, and copying it across would move the card to
            // a place nobody chose
            if (old.transform is RectTransform oldRect &&
                Mathf.Approximately(oldRect.anchorMin.y, 1f) &&
                Mathf.Approximately(oldRect.anchorMax.y, 1f))
                placed = oldRect.anchoredPosition.y;

            // The money was moved INTO the old card. Destroying the card with it
            // still inside destroys the money counter -- permanently, in a saved
            // scene, for the crime of running a setup command twice
            CurrencyText rescued = old.GetComponentInChildren<CurrencyText>(true);

            if (rescued != null)
            {
                // Back onto ITS OWN canvas, not onto whichever one is being
                // built on now. The one in here belongs to a panel somewhere
                // else, and moving it again would only move the problem
                Transform home = rescued.GetComponentInParent<Canvas>(true).transform;

                // The whole block that was moved, not the text inside it. A
                // money display is an icon and a number in a pill, and lifting
                // the number out of it leaves the pill behind
                Transform block = Block(rescued.transform, old.transform);

                block.SetParent(home, false);

                report.AppendLine("Eski karttan kurtarildi: " + block.name +
                                  " -> " + home.name);
                report.AppendLine("  Bu " + home.name + " panelinin kendi sayaci --");
                report.AppendLine("  paneli acip yerine surukleman gerekebilir.");
            }

            Object.DestroyImmediate(old.gameObject);
        }

        GameObject root = new GameObject(rootName, typeof(RectTransform), typeof(LivesHud));

        root.transform.SetParent(canvas.transform, false);

        // Last, so it draws over whatever else is on this canvas. Sibling order
        // is what decides that inside a canvas -- there is no sorting order on
        // an Image the way there is on a sprite
        root.transform.SetAsLastSibling();

        RectTransform rect = root.GetComponent<RectTransform>();

        // Pinned to the top centre, so it stays there whatever the screen is.
        // Anchors do this; a position does not
        rect.anchorMin = new Vector2(.5f, 1f);
        rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);

        float span = slots * heartSize + (slots - 1) * heartGap;

        float top = float.IsNaN(placed) ? -topMargin : placed;

        rect.anchoredPosition = new Vector2(0f, top);
        rect.sizeDelta = new Vector2(Mathf.Max(span, cardWidth) + cardPadding * 2f, cardHeight);

        report.AppendLine(Mathf.Abs(top + topMargin) < .5f
            ? "Yukseklik: Y = " + top.ToString("0") + " (varsayilan)"
            : "Yukseklik: Y = " + top.ToString("0") + " -- elle tasinmis, korundu.\n" +
              "  Varsayilana donmek icin " + (-topMargin).ToString("0") + " yaz.");
        report.AppendLine();

        Card(root);

        if (money != null)
            Centre(money, root.transform, report);

        Image[] hearts = new Image[slots];

        for (int i = 0; i < slots; i++)
        {
            GameObject heart = new GameObject("Heart " + (i + 1), typeof(RectTransform), typeof(Image));

            heart.transform.SetParent(root.transform, false);

            RectTransform heartRect = heart.GetComponent<RectTransform>();

            heartRect.anchorMin = new Vector2(.5f, .5f);
            heartRect.anchorMax = new Vector2(.5f, .5f);
            heartRect.pivot = new Vector2(.5f, .5f);
            heartRect.sizeDelta = new Vector2(heartSize, heartSize);

            // Under the money, across the bottom of the card. Laid out by hand
            // instead of by a HorizontalLayoutGroup -- a layout group decides
            // the size too, and then the size in the inspector is a number that
            // does nothing
            heartRect.anchoredPosition = new Vector2(
                -span * .5f + heartSize * .5f + i * (heartSize + heartGap),
                -cardHeight * .5f + heartSize * .5f + cardPadding);

            Image image = heart.GetComponent<Image>();

            image.sprite = full;

            // Matches LivesHud's own full colour, so the row does not change
            // shade on the first frame of play
            image.color = heartColour;

            image.raycastTarget = false;

            hearts[i] = image;
        }

        SerializedObject so = new SerializedObject(root.GetComponent<LivesHud>());
        SerializedProperty list = so.FindProperty("hearts");

        list.arraySize = hearts.Length;

        for (int i = 0; i < hearts.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = hearts[i];

        so.FindProperty("fullHeart").objectReferenceValue = full;
        so.FindProperty("emptyHeart").objectReferenceValue = empty == null ? full : empty;
        so.FindProperty("brokenHeart").objectReferenceValue = broken;
        so.FindProperty("sunburst").objectReferenceValue = sunburst;

        // Written rather than left to the field initialisers, for the reason
        // every serialised default in this project is written: the component
        // may already exist in the scene from an earlier run, and a field added
        // to one of those does not reliably come back carrying its initialiser
        so.FindProperty("fullColour").colorValue = heartColour;
        so.FindProperty("emptyColour").colorValue = lostColour;
        so.FindProperty("brokenColour").colorValue = brokenColour;
        so.FindProperty("brokenScale").floatValue = .78f;

        so.ApplyModifiedProperties();

        report.AppendLine(slots + " can slotu kuruldu: " + canvas.name + "/" + rootName);

        report.Append(EnsureCounter());
        report.Append(Verify(root, hearts));

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        report.AppendLine();
        report.AppendLine("Musteri sabri bitince kaciyor ve bir can gidiyor.");
        report.AppendLine("Can sayisi: Lives > Max Lives");
        report.AppendLine("Sabir suresi: musteri prefabi > Customer Order > Patience");
        report.AppendLine();
        report.AppendLine("Kartin yuksekligi: Hierarchy > " + rootName + " >");
        report.AppendLine("  Rect Transform > Pos Y. Kucultmek asagi indirir,");
        report.AppendLine("  cunku kart ekranin ust kenarina asili. Elle yazdigin");
        report.AppendLine("  deger bu komut tekrar calisinca korunur.");
        report.AppendLine();
        report.AppendLine("Sahneyi kaydet: Ctrl+S");

        Debug.Log("[Can]\n" + report);
        EditorUtility.DisplayDialog("Can Slotlari", report.ToString(), "Tamam");
    }

    // Wires only the new artwork. It deliberately does not rebuild, move or
    // destroy the existing HUD, so hand-positioned UI survives this upgrade.
    [MenuItem("Cooked Fast/Oyun/Can: Kayip Efektini Bagla", priority = 221)]
    public static void WireLossEffect()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Can Kaybi Efekti",
                "Play'den cik ve tekrar dene. Sahne degisiklikleri oyun kapaninca kaybolur.",
                "Tamam");
            return;
        }

        FoodIconBaker.MakeSprite(brokenPath);
        FoodIconBaker.MakeSprite(sunburstPath);
        AssetDatabase.Refresh();

        Sprite broken = AssetDatabase.LoadAssetAtPath<Sprite>(brokenPath);
        Sprite burst = AssetDatabase.LoadAssetAtPath<Sprite>(sunburstPath);
        LivesHud hud = Object.FindFirstObjectByType<LivesHud>(FindObjectsInactive.Include);

        if (hud == null || broken == null || burst == null)
        {
            EditorUtility.DisplayDialog("Can Kaybi Efekti",
                (hud == null ? "Sahnede LivesHud bulunamadi.\n" : "") +
                (broken == null ? "Kirik kalp bulunamadi: " + brokenPath + "\n" : "") +
                (burst == null ? "Sunburst bulunamadi: " + sunburstPath : ""),
                "Tamam");
            return;
        }

        Undo.RecordObject(hud, "Wire life loss effect");

        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("brokenHeart").objectReferenceValue = broken;
        so.FindProperty("sunburst").objectReferenceValue = burst;
        so.FindProperty("brokenColour").colorValue = brokenColour;
        so.FindProperty("brokenScale").floatValue = .78f;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        Selection.activeGameObject = hud.gameObject;

        Debug.Log("[Can] Kayip efekti baglandi. HUD yeniden kurulmadi; konumu korundu. Ctrl+S.", hud);
        EditorUtility.DisplayDialog("Can Kaybi Efekti",
            "Kirik kalp ve sunburst baglandi.\nHUD konumu degistirilmedi.\n\nCtrl+S ile kaydet.",
            "Tamam");
    }

    // Which money counter is the one on screen.
    //
    // There is more than one. CurrencyManager writes to every CurrencyText it
    // can find, so the upgrade panels each carry their own -- and the first one
    // FindFirstObjectByType happened to return was the hiring panel's, on a
    // canvas that is switched off for all of the game except the moment it is
    // open. The card was built there: correctly, invisibly, on a hidden panel.
    //
    // The rule is the one the player uses: the counter you can actually see
    private static CurrencyText PickMoney(StringBuilder report)
    {
        CurrencyText[] all = Object.FindObjectsByType<CurrencyText>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        report.AppendLine("Para sayaclari (" + all.Length + ")");

        CurrencyText best = null;
        int bestSort = int.MinValue;

        foreach (CurrencyText candidate in all)
        {
            Canvas own = candidate.GetComponentInParent<Canvas>(true);

            // Given a value up front, because the && short circuits: with no
            // canvas IsVisible never runs and never assigns it
            string why = "Canvas yok";

            bool live = own != null && IsVisible(candidate.transform, out why);

            report.AppendLine("  " + Path(candidate.transform) +
                              "  [" + (own == null ? "Canvas yok" : own.name) + "]" +
                              (live ? "  GORUNUR" : "  GORUNMEZ: " + why));

            if (!live)
                continue;

            // Drawn last wins. Two visible canvases means the HUD is the one on
            // top, which is what sorting order says and nothing else does
            int sort = own.sortingOrder;

            if (best != null && sort <= bestSort)
                continue;

            best = candidate;
            bestSort = sort;
        }

        report.AppendLine();

        if (best != null)
            return best;

        report.AppendLine("HICBIRI GORUNUR DEGIL -- hepsi kapali bir panelde.");
        report.AppendLine("  Kart ekranin ustune kurulacak, para tasinmayacak.");
        report.AppendLine();

        return null;
    }

    // Everything between an object and the screen that can stop it being drawn
    // while leaving it looking perfectly healthy in the inspector.
    //
    // activeInHierarchy alone said the hiring panel's counter was visible. It
    // is not: the panel is faded out by a CanvasGroup at zero alpha, which is
    // how a panel that is "open but hidden" is normally built, and nothing
    // about the object itself reflects that
    private static bool IsVisible(Transform target, out string why)
    {
        why = "";

        if (!target.gameObject.activeInHierarchy)
        {
            why = "kapali obje";
            return false;
        }

        for (Transform walk = target; walk != null; walk = walk.parent)
        {
            if (walk.TryGetComponent(out CanvasGroup group) && group.alpha < .01f)
            {
                why = walk.name + " CanvasGroup alpha 0";
                return false;
            }

            if (walk.TryGetComponent(out Canvas canvas) && !canvas.enabled)
            {
                why = walk.name + " Canvas kapali";
                return false;
            }
        }

        return true;
    }

    private static string Path(Transform target)
    {
        string path = target.name;

        for (Transform walk = target.parent; walk != null; walk = walk.parent)
            path = walk.name + "/" + path;

        return path;
    }

    // Says whether the thing just built will actually be seen, in the terms
    // that decide it.
    //
    // Building it and reporting "done" is the report that has now been wrong
    // twice. Every one of these has been the answer at some point in this
    // project: switched off, no sprite, zero size, off the top of the screen,
    // fully transparent. None of them says anything on its own
    private static string Verify(GameObject root, Image[] hearts)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine();
        report.AppendLine("Kontrol");

        RectTransform rect = root.GetComponent<RectTransform>();

        Vector3[] corners = new Vector3[4];

        rect.GetWorldCorners(corners);

        report.AppendLine("  aktif    : " + root.activeInHierarchy);
        report.AppendLine("  olcu     : " + rect.rect.width.ToString("0") + " x " +
                          rect.rect.height.ToString("0"));
        report.AppendLine("  ekranda  : alt " + corners[0].ToString("0") +
                          "  ust " + corners[2].ToString("0"));
        report.AppendLine("  ekran    : " + Screen.width + " x " + Screen.height);

        int drawn = 0;

        foreach (Image heart in hearts)
        {
            if (heart != null && heart.sprite != null && heart.color.a > .01f)
                drawn++;
        }

        report.AppendLine("  kalpler  : " + drawn + "/" + hearts.Length + " cizilebilir");

        // Everything above it that can hide it while leaving it "active".
        //
        // A CanvasGroup at zero alpha, a disabled Canvas component, a switched
        // off parent -- all three draw nothing and none of them makes the
        // object itself look wrong when you click on it
        for (Transform walk = root.transform.parent; walk != null; walk = walk.parent)
        {
            if (!walk.gameObject.activeSelf)
                report.AppendLine("  KAPALI ust obje: " + walk.name + " -- HUD gorunmez");

            if (walk.TryGetComponent(out CanvasGroup group) && group.alpha < .01f)
                report.AppendLine("  CanvasGroup alpha 0: " + walk.name + " -- HUD gorunmez");

            if (walk.TryGetComponent(out Canvas own) && !own.enabled)
                report.AppendLine("  Canvas KAPALI: " + walk.name + " -- HUD gorunmez");
        }

        // Anything drawing over the top of it
        Canvas mine = root.GetComponentInParent<Canvas>(true);

        foreach (Canvas other in Object.FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == mine || !other.isActiveAndEnabled || other.transform.parent != null)
                continue;

            if (mine != null && other.sortingOrder > mine.sortingOrder)
                report.AppendLine("  ustte cizen Canvas: " + other.name +
                                  " (sort " + other.sortingOrder + " > " +
                                  mine.sortingOrder + ")");
        }

        if (corners[2].y > Screen.height || corners[0].y < 0f)
            report.AppendLine("  UYARI: kart ekranin disina tasiyor olabilir");

        return report.ToString();
    }

    private const float cardWidth = 300f;
    private const float cardHeight = 150f;
    private const float cardPadding = 12f;

    // Behind everything, filling the whole holder. Built from the GUI pack's
    // rounded panel when it is there, and a flat rounded-ish block when it is
    // not -- a card that fails to load is a row of hearts floating on nothing,
    // which is what a missing background looks like and not what it is
    private static void Card(GameObject holder)
    {
        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));

        card.transform.SetParent(holder.transform, false);
        card.transform.SetAsFirstSibling();

        RectTransform rect = card.GetComponent<RectTransform>();

        // Stretched to the holder rather than sized. The card is the holder's
        // background, so it should not have a size of its own to disagree with
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = card.GetComponent<Image>();

        Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(cardSpritePath);

        if (panel != null)
        {
            image.sprite = panel;
            image.type = Image.Type.Sliced;
        }

        image.color = new Color(1f, 1f, 1f, .82f);
        image.raycastTarget = false;
    }

    private const string cardSpritePath =
        "Assets/Skyden_Games/Free_Casual_GUI/Demo/Sprites/Others/Shapes/Shape_01.png";

    // The money moves into the card and sits in the middle of it.
    //
    // Reparented rather than copied: CurrencyManager finds every CurrencyText in
    // the scene and writes to it, so a second one would be a second thing to
    // keep in step -- and the first would still be wherever it was
    // The whole money display, not the number inside it.
    //
    // On screen the money is a pill: a bill icon and a figure sitting on a
    // rounded background. Those are siblings under a wrapper, so moving the
    // CurrencyText alone takes the digits out of their own frame and leaves the
    // frame where it was
    private static Transform Block(Transform text, Transform stopAt)
    {
        Transform parent = text.parent;

        if (parent == null || parent == stopAt || parent.GetComponent<Canvas>() != null)
            return text;

        // A wrapper holds a handful of things. Anything bigger is the screen's
        // layout, and taking that would take half the interface with it
        return parent.childCount <= 4 ? parent : text;
    }

    private static void Centre(CurrencyText money, Transform holder, StringBuilder report)
    {
        Transform block = Block(money.transform, holder);

        RectTransform rect = block as RectTransform;

        if (rect == null)
        {
            report.AppendLine("  para sayacinda RectTransform yok, tasinmadi");
            return;
        }

        report.AppendLine("  tasinan: " + Path(block));

        Undo.SetTransformParent(rect, holder, "Move currency into card");

        rect.anchorMin = new Vector2(.5f, 1f);
        rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);

        rect.anchoredPosition = new Vector2(0f, -cardPadding);
        rect.localScale = Vector3.one;

        report.AppendLine("  para sayaci kartin ortasina alindi");
    }

    // The count itself lives on its own object, not on the hearts. There may be
    // a second thing that draws it later, and a counter owned by one display is
    // a counter that disappears with it
    private static string EnsureCounter()
    {
        Lives lives = Object.FindFirstObjectByType<Lives>(FindObjectsInactive.Include);

        if (lives != null)
            return "Can sayaci zaten var: " + lives.name + "\n";

        GameObject holder = new GameObject("Lives", typeof(Lives));

        EditorSceneManager.MarkSceneDirty(holder.scene);

        return "Can sayaci olusturuldu: Lives\n";
    }

    private static Canvas FindOverlayCanvas(StringBuilder report)
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            // The topmost one. A canvas nested inside another is a sub-canvas
            // for batching, and hanging a HUD off one of those inherits whatever
            // that piece of UI does with itself
            if (canvas.transform.parent != null &&
                canvas.transform.parent.GetComponentInParent<Canvas>() != null)
                continue;

            report.AppendLine("Canvas bulundu: " + canvas.name);

            return canvas;
        }

        report.AppendLine("Ekran Canvas'i yoktu, olusturuldu: HUD Canvas");

        GameObject made = new GameObject("HUD Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas created = made.GetComponent<Canvas>();

        created.renderMode = RenderMode.ScreenSpaceOverlay;

        // Behind whatever else the game already draws, unless that is nothing
        created.sortingOrder = 10;

        CanvasScaler scaler = made.GetComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Portrait, matched on WIDTH. Matching height on a phone means the
        // hearts change size with every different aspect ratio
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;

        return created;
    }
}
