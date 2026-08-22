using System.Text;
using TMPro;
using UnityEngine;
using UnityEditor;

// Builds the order bubble on every customer prefab.
//
// Built from world objects and not from a world space Canvas. That is not a
// style preference: the oven's timer went through two canvas versions that drew
// nothing, once for a font that failed to resolve and once for a scale decided
// somewhere else, and neither could be checked without entering play mode. A
// quad, a sprite and a TextMeshPro are visible in the scene view the moment
// they exist
public static class OrderBubbleSetup
{
    private const string customersFolder = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";
    private const string materialsFolder = "Assets/Tiny Coffee Shop/Materials";

    private const string emojiFolder =
        "Assets/Arlan Trindade/Free emojis pixel art/emojis-x4-128x128";

    // Read once and checked: E1 is a plain smile, E72 a nervous one with a bead
    // of sweat, E73 a face that has had enough. Fields on the component, so
    // disagreeing is one drag rather than a code change
    private const string happyEmoji = "E1";
    private const string neutralEmoji = "E72";
    private const string angryEmoji = "E73";

    private const string bubbleName = "Order Bubble";

    // The one number the whole card is built from -- every offset, the icon, the
    // ring and the font size are fractions of it. Making the bubble bigger is
    // changing this and nothing else, which is the entire point of building it
    // this way rather than out of seven offsets that have to agree.
    //
    // Public because it stopped being only this file's business the moment the
    // bubbles grew wider than the customers: CustomerSetup reads it to check its
    // queue spacing still clears them.
    //
    // The WHOLE image's height -- panel plus the tail hanging under it. Roughly
    // 70% of it is the panel, so the box the player actually reads comes out
    // near 1.2 tall
    public const float bubbleSize = 1.75f;

    // The whole image's width over its height. One, because the file is 540x540
    // and any other number here stretches it.
    //
    // The rectangle is already in the art: the drawn panel is about 1.35 wide
    // for every 1 tall, and the tail beneath it is what makes the FILE square.
    // Forcing the file to 1.35 would have stretched that panel to nearly 2:1
    public const float bubbleAspect = 1f;

    // What actually has to clear the neighbours
    public const float CardWidth = bubbleSize * bubbleAspect;

    // The card's proportions, every one a fraction of bubbleSize.
    //
    // These used to be locals inside Build, which was fine while nothing
    // outside needed to know how tall the finished card stands. It does now:
    // the badge hangs ABOVE the panel, so the panel's own height is not what a
    // bubble in the row behind has to clear, and CustomerSetup was lifting the
    // back row by the panel alone.
    //
    // Measured off the file: the drawn box runs from 4% to 73% of the image
    // height and the spike carries on to 95%. Centring content on the IMAGE
    // would drop the food into the spike, so every offset is taken from the box.
    private const float boxTopShare = .46f;
    private const float boxBottomShare = -.23f;
    private const float boxMiddleShare = (boxTopShare + boxBottomShare) * .5f;

    // The food picture.
    private const float iconShare = .34f;

    // The drawn face -- the emoji, and the number the whole badge is measured
    // from. Face rather than ring, because the face is the thing being read and
    // the ring is trim around it.
    //
    // THE knob for "make the timer and the emoji bigger". Everything in the
    // badge is a fraction of it: the ring, the countdown disc, the digits, the
    // celebration burst and how high the badge has to sit. Raised from .40
    // because the bubbles went back to full size and the badge did not keep up
    // -- at .40 the clock was a detail on a card rather than the thing the
    // player is supposed to be watching.
    public const float badgeFaceShare = .52f;

    private const float ringInnerShare = .40f;
    private const float ringOuterShare = .72f;

    // Where the badge's centre lands, and how far the whole card reaches above
    // its own origin. The badge deliberately overhangs the panel's top edge, so
    // CardTopShare is meaningfully more than boxTopShare.
    private const float badgeYShare = boxMiddleShare + iconShare * .5f
                                      + badgeFaceShare * ringOuterShare + .02f;

    public const float CardTopShare =
        badgeYShare + badgeFaceShare * ringOuterShare;

    // Panel plus the badge hanging over it, in world units. What a bubble
    // standing in the row behind has to be lifted clear of. The tail below the
    // panel is left out on purpose -- it is a thin spike, and demanding room
    // for it stacks the rows twice as high as they need to be.
    public const float CardHeight = bubbleSize * (CardTopShare - boxBottomShare);

    [MenuItem("Cooked Fast/Musteri/Siparis Balonunu Kur", priority = 600)]
    public static void Setup()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { customersFolder });

        if (guids.Length <= 0)
        {
            EditorUtility.DisplayDialog("Siparis Balonu",
                "Musteri prefabi bulunamadi:\n" + customersFolder, "Tamam");
            return;
        }

        // Dropped into the project as a plain texture, which loads as a Sprite
        // exactly never. Settled here rather than asked for, because a null
        // sprite shows up as a bubble with no burst and no reason given
        // Imported as a Sprite before it is loaded as one. A PNG left at the
        // default texture type answers null to LoadAssetAtPath<Sprite> and says
        // nothing about why
        bool reimported = FoodIconBaker.MakeSprite(tickPath);

        reimported |= FoodIconBaker.MakeSprite(undecidedPath);

        reimported |= FoodIconBaker.MakeSprite(sunburstPath);

        reimported |= FoodIconBaker.MakeSprite(cardPath);

        if (reimported)
            AssetDatabase.Refresh();

        Sprite happy = LoadEmoji(happyEmoji);
        Sprite neutral = LoadEmoji(neutralEmoji);
        Sprite angry = LoadEmoji(angryEmoji);

        StringBuilder report = new StringBuilder();

        report.AppendLine("Kararsiz isareti: " +
            (AssetDatabase.LoadAssetAtPath<Sprite>(undecidedPath) == null
                ? "BULUNAMADI " + undecidedPath
                : "Mark_Question-1"));

        report.AppendLine("Isin: " +
            (AssetDatabase.LoadAssetAtPath<Sprite>(sunburstPath) == null
                ? "BULUNAMADI " + sunburstPath
                : "Sunburst.png"));
        report.AppendLine();

        report.AppendLine("Emojiler");
        report.AppendLine("  mutlu : " + (happy == null ? "BULUNAMADI " + happyEmoji : happy.name));
        report.AppendLine("  idare : " + (neutral == null ? "BULUNAMADI " + neutralEmoji : neutral.name));
        report.AppendLine("  kizgin: " + (angry == null ? "BULUNAMADI " + angryEmoji : angry.name));

        // How much of the file is actually face. The emoji is fitted by THIS and
        // not by the picture it came in, so a hundred here means the trim did
        // not happen -- and a gap between the yellow and the green collar is
        // exactly what that looks like on the card
        float drawnFace = FaceSize(happy, neutral, angry);
        float wholeFile = happy != null ? happy.bounds.size.y : 0f;

        if (drawnFace > .0001f && wholeFile > .0001f)
        {
            report.AppendLine("  cizili alan: dosyanin %" +
                              Mathf.RoundToInt(drawnFace / wholeFile * 100f) + "'i");
            report.AppendLine("  yuz bu olcuye gore delige oturuyor, %100 cikarsa");
            report.AppendLine("  kirpma olmamis demektir ve bosluk kalir");
        }

        report.AppendLine();

        int built = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Build(path, happy, neutral, angry, report))
                built++;
        }

        AssetDatabase.SaveAssets();

        report.AppendLine();
        report.AppendLine(built + " musteri prefabi guncellendi.");
        report.AppendLine();
        report.AppendLine("Nasil isliyor");
        report.AppendLine("  musteri girer   -> balon acilir, istedigi yemegin ikonu");
        report.AppendLine("                     ve adedi gorunur, sayac 5'te");
        report.AppendLine("  bekledikce      -> sayac geri sayar, yesil-sari-kirmizi");
        report.AppendLine("  sayac 0 olunca  -> musteri kacar, bir can gider");
        report.AppendLine("  bazen           -> ne istedigini bilmeyen musteri gelir:");
        report.AppendLine("                     balonda soru isareti, adet yazmaz,");
        report.AppendLine("                     ne verirsen alir ve gider");
        report.AppendLine("  siparis biter   -> kart kapanir, o andaki emoji buyur,");
        report.AppendLine("                     arkasinda isin doner, alt tarafta kazanc");
        report.AppendLine("                     yazar. Musteri o yuzle cikar gider");
        report.AppendLine();
        report.AppendLine("Ayarlar: musteri prefabi > Customer Order");
        report.AppendLine("  Patience           toplam sabir, saniye (45)");
        report.AppendLine("  Happy Above        sabrin bu oraninin ustunde servis = mutlu (0.6)");
        report.AppendLine("  Angry Below        bu oranin altinda = kizgin (0.25)");
        report.AppendLine("  Happy Reward       mutlu musteri kac kat oder (2)");
        report.AppendLine("  Angry Reward       kizgin musteri kac kat oder (0.5)");
        report.AppendLine("  Celebration Scale  emoji kac katina buyur (1.7)");
        report.AppendLine("  Celebration Time   acilis ve kapanis suresi (0.25)");
        report.AppendLine("  Celebration Hold   bastan sona kac saniye durur (2)");
        report.AppendLine("  Sunburst Spin      isinin donusu, derece/sn (140)");
        report.AppendLine("  Appear Time        balonun acilis pop suresi (0.25)");
        report.AppendLine("  Pop Delay          emoji ve isin kac sn sonra patlar (0.12)");
        report.AppendLine("  Fade Time          kaybolurken sonme suresi (0.3)");
        report.AppendLine("  Undecided Icon     kararsiz musterinin isareti");
        report.AppendLine("  Undecided Tint     o isaretin rengi");
        report.AppendLine();
        report.AppendLine("Kararsiz musteri sikligi: tezgahin kendisinde --");
        report.AppendLine("  Food Serving Customer Manager > Undecided Chance");
        report.AppendLine("  yuzde olarak. 0 = varsayilan 12, eksi bir sayi = hic.");
        report.AppendLine();
        // Both figures, because they are different questions. The file is what
        // the neighbours have to clear; the panel is what the player reads
        report.AppendLine("Balon dosyasi: " + CardWidth.ToString("0.00") + " en x " +
                          bubbleSize.ToString("0.00") + " boy (kuyruk dahil).");
        report.AppendLine("  Okunan kutu: " + (CardWidth * .94f).ToString("0.00") + " x " +
                          (bubbleSize * .69f).ToString("0.00"));
        report.AppendLine("  Yemek balonun tam ortasinda. Rozet ona gore yer");
        report.AppendLine("  buluyor: yemege degmeyecek kadar iniyor, daha");
        report.AppendLine("  fazla degil. Rozeti kucultursen kendi kendine");
        report.AppendLine("  daha asagi oturur.");
        report.AppendLine("  Halka artik emojinin ic tarafindan basliyor --");
        report.AppendLine("  ic kismi yuzun arkasinda kaliyor, disarida sadece");
        report.AppendLine("  cepecevre bir serit gorunuyor.");
        report.AppendLine();
        report.AppendLine("  Elle degistirmek icin prefabi ac, " + bubbleName + "'in");
        report.AppendLine("  Scale'ini buyut -- ic olculer kendiliginden uyar.");
        report.AppendLine("  Kalici olsun istersen OrderBubbleSetup > bubbleSize.");
        report.AppendLine("  Sayac + emoji buyuklugu: OrderBubbleSetup > badgeFaceShare");
        report.AppendLine("    su an " + badgeFaceShare.ToString("0.00") +
                          " -- halka, disk, rakamlar ve isin hepsi buna bagli.");
        report.AppendLine("    kartin tepesi " + CardHeight.ToString("0.00") +
                          " birim (rozet dahil).");
        report.AppendLine("  Yemek modelinin boyu Icon Anchor'un Scale'inden geliyor.");

        Debug.Log("[Siparis]\n" + report);
        EditorUtility.DisplayDialog("Siparis Balonu", report.ToString(), "Tamam");
    }

    private static bool Build(string path, Sprite happy, Sprite neutral, Sprite angry,
        StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            if (!root.TryGetComponent(out Customer customer))
            {
                report.AppendLine(root.name + ": Customer yok, atlandi");
                return false;
            }

            report.AppendLine(root.name);

            // Re-running replaces rather than stacking a second bubble on the
            // first, and rebuilding is how a changed layout reaches prefabs that
            // already have one
            Transform existing = root.transform.Find(bubbleName);

            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            if (!root.TryGetComponent(out CustomerOrder order))
                order = root.AddComponent<CustomerOrder>();

            float head = HeadHeight(root);

            GameObject bubble = new GameObject(bubbleName);

            bubble.transform.SetParent(root.transform, false);

            // Lifted so the TAIL ends just above the ears. The tail is what says
            // whose order this is -- the card no longer rides on their head --
            // so the gap it leaves is the whole of the connection
            bubble.transform.localPosition = new Vector3(0f, head + bubbleSize * .55f, 0f);
            bubble.transform.localRotation = Quaternion.identity;
            bubble.transform.localScale = Vector3.one;

            // Turned to the camera every frame. Everything under it is flat, and
            // the kitchen camera is isometric, so a fixed facing is seen edge on
            bubble.AddComponent<FaceCamera>();

            // Everything below is a fraction of one number, so the whole card
            // keeps its proportions. Resizing it is scaling Order Bubble, not
            // editing seven offsets that have to agree with each other.
            //
            // Layered along Z in millimetres. Negative is towards the camera,
            // because FaceCamera points the holder's forward the way the camera
            // looks -- so the card sits furthest back and everything on it in
            // front, and nothing fights for the same depth
            const float size = bubbleSize;
            const float width = CardWidth;

            // The art is a panel with a tail hanging under it, and only the
            // panel can hold anything -- see the shares at the top of the file
            // for where these come from.
            const float boxTop = size * boxTopShare;
            const float boxBottom = size * boxBottomShare;

            // Dead centre of the drawn box, and the food gets it.
            //
            // It was being centred in whatever the badge left over instead,
            // which parked the picture along the bottom edge with a band of
            // empty card above it. The panel was drawn for the food to sit in
            // the middle of it, so that is where the food sits and everything
            // else works around that
            const float boxMiddle = (boxTop + boxBottom) * .5f;

            // The anchor's scale IS the icon size. CustomerOrder fits the food
            // model into it, so resizing the picture is dragging one number
            const float iconSize = size * iconShare;

            // The drawn face, and the number the badge is measured from -- the
            // face rather than the ring, because the face is the thing being
            // read and the ring is trim around it
            const float face = size * badgeFaceShare;

            // A rim around the face, not a hoop with the face sitting inside it.
            //
            // The inner edge is well within the face and hidden behind it, so
            // what shows is a band around the outside. That also keeps the arc
            // readable as it drains: a thin ring left in the open reads as a
            // broken circle at low fill, a rim behind a face reads as a rim
            // Only the part past the face's own edge at .5 is ever seen, so the
            // outer radius is the whole of how big the clock looks -- and the
            // step from .56 to .62 is not a tenth bigger, it is twice the band
            const float ringInner = face * ringInnerShare;
            const float ringOuter = face * ringOuterShare;

            // Hung over the top edge of the panel, as low as the food allows.
            //
            // Two things want the same strip of card. The food is what the
            // bubble is for, so the badge is the one that gives way: it drops
            // until it is just clear of the picture and no further. Written as
            // arithmetic rather than as a number, so shrinking the badge lets it
            // settle deeper on its own instead of leaving a gap nobody notices
            float badgeY = size * badgeYShare;

            Vector3 badge = new Vector3(0f, badgeY, -.03f);

            Sprite cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(cardPath);

            GameObject card = cardSprite != null
                ? Card(bubble, cardSprite, width, size)
                : Quad(bubble, "Card", Color.white,
                    new Vector3(width, size, 1f), new Vector3(0f, 0f, .004f));

            GameObject anchor = new GameObject("Icon Anchor");

            anchor.transform.SetParent(bubble.transform, false);

            anchor.transform.localPosition = new Vector3(0f, boxMiddle, -size * .3f);

            anchor.transform.localScale = Vector3.one * iconSize;

            // Bottom right OF THE FOOD, not of the card -- so it reads as a
            // label on the thing rather than as a number sitting near it
            GameObject count = Text(bubble, "Count",
                new Vector3(iconSize * .55f, boxMiddle - iconSize * .5f, -.02f), size, 115);

            TextMeshPro quantity = count.GetComponent<TextMeshPro>();

            quantity.fontSize = size * 2.6f;

            // Narrow, so the box stays around the number instead of reaching
            // back to the middle of the card and dragging the text with it
            RectTransform countRect = count.GetComponent<RectTransform>();

            if (countRect != null)
                countRect.sizeDelta = new Vector2(size * .45f, size * .28f);

            // White, because the card is now a mid blue. The old near-black was
            // chosen against a cream card and would sit on this one as a smudge
            quantity.color = Color.white;

            GameObject timer = Radial(bubble, badge + new Vector3(0f, 0f, .012f),
                ringInner, ringOuter);

            // In the emoji's place, not above it. For the last few seconds the
            // number IS the face -- it takes the spot the eye is already on
            // rather than asking it to look somewhere else at the one moment
            // there is no time to
            Vector3 clock = badge + new Vector3(0f, 0f, -.01f);

            // A hair bigger than the face it replaces, so it covers the emoji
            // completely and still leaves the same rim of ring showing round it.
            // The badge should not change size when the last seconds arrive --
            // it changes what it says, which is a different thing
            GameObject disc = Disc(bubble, clock, face * 1.04f);

            GameObject number = Text(bubble, "Countdown",
                clock + new Vector3(0f, 0f, -.01f), size, 130);

            TextMeshPro digits = number.GetComponent<TextMeshPro>();

            // Measured off the face, not off the card. The number stands in for
            // the emoji, so it should be the size the emoji is -- tied to the
            // card instead, resizing the badge leaves the digits rattling
            // around inside it
            digits.fontSize = face * 10.5f;
            digits.color = Color.white;

            RectTransform digitRect = number.GetComponent<RectTransform>();

            if (digitRect != null)
                digitRect.sizeDelta = new Vector2(face, face);

            // Behind the emoji as well, and that is settled by SORTING ORDER,
            // not by where these sit in the hierarchy. Sprites are sorted by
            // their order first and their place in the list never comes into it.
            //
            // Kept at about the width of the card whatever the face does. A
            // burst wider than the card is a burst reaching into the customer
            // standing beside this one
            GameObject burst = Sunburst(bubble, badge + new Vector3(0f, 0f, .005f), face * 2.4f);

            // Fitted to the DRAWN face, not to the file it arrived in -- the
            // file is a square with transparent margins, and fitting that is
            // what used to leave a gap between the emoji and the ring
            float drawn = FaceSize(happy, neutral, angry);

            GameObject mood = Emoji(bubble, happy, badge, face, drawn);

            // Under the emoji, where the box used to be -- by the time this is
            // on screen the card is gone and the space is free
            GameObject earnings = Text(bubble, "Earnings",
                new Vector3(0f, boxMiddle, -.05f), size, 118);

            TextMeshPro money = earnings.GetComponent<TextMeshPro>();

            // Smaller than the count, not bigger. It is a result, and the thing
            // the eye should land on at that moment is the face
            money.fontSize = size * 4.5f;
            money.color = new Color(.15f, .62f, .24f);

            earnings.SetActive(false);

            SerializedObject so = new SerializedObject(order);

            so.FindProperty("bubbleRoot").objectReferenceValue = bubble;
            so.FindProperty("card").objectReferenceValue = card;
            so.FindProperty("iconAnchor").objectReferenceValue = anchor.transform;
            so.FindProperty("countText").objectReferenceValue = count.GetComponent<TextMeshPro>();
            so.FindProperty("timerRing").objectReferenceValue = timer.GetComponent<RadialTimer>();
            so.FindProperty("countdownText").objectReferenceValue = digits;
            so.FindProperty("countdownDisc").objectReferenceValue =
                disc.GetComponent<SpriteRenderer>();
            so.FindProperty("emoji").objectReferenceValue = mood.GetComponent<SpriteRenderer>();
            so.FindProperty("sunburst").objectReferenceValue = burst.transform;
            so.FindProperty("earningsText").objectReferenceValue = money;

            // The bubble makes one of these per row at runtime, so all it needs
            // is the picture. Left null it simply does not tick, which is the
            // old behaviour rather than a hole in the card
            so.FindProperty("readyIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(tickPath);

            // Left null it simply never appears, and the undecided customer
            // shows an empty bubble instead -- which is the one failure this
            // whole feature could be mistaken for
            so.FindProperty("undecidedIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(undecidedPath);

            // Written rather than left alone, because the field's MEANING
            // changed: it used to read zero as "use 40", and now zero is a still
            // burst that only pops. The 40 already saved on these prefabs would
            // otherwise keep spinning under the new rule
            so.FindProperty("sunburstSpin").floatValue = 140f;

            so.FindProperty("happySprite").objectReferenceValue = happy;
            so.FindProperty("neutralSprite").objectReferenceValue = neutral;
            so.FindProperty("angrySprite").objectReferenceValue = angry;

            so.ApplyModifiedProperties();

            // Rebuilding the bubble must not silently put the retired pixel
            // faces back. If the purchased pack is present, install the three
            // animated moods into the same authored badge position.
            Emoji45Setup.Install(bubble, order, face, report);

            // The customer's own field, so Initialize can open the bubble
            SerializedObject customerSo = new SerializedObject(customer);

            customerSo.FindProperty("order").objectReferenceValue = order;
            customerSo.ApplyModifiedProperties();

            bubble.SetActive(false);

            report.AppendLine("  balon kuruldu, kafa yuksekligi " + head.ToString("0.00"));

            PrefabUtility.SaveAsPrefabAsset(root, path);

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The top of the rabbit, so the bubble clears the head on a character of any
    // size rather than at a height that suits one of them
    private static float HeadHeight(GameObject root)
    {
        float top = 0f;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            float local = renderer.bounds.max.y - root.transform.position.y;

            if (local > top)
                top = local;
        }

        return top > .2f ? top : 1f;
    }

    // An actual speech bubble, tail and all, instead of the GUI pack's rounded
    // rectangle. The tail points down at the customer, which is what says whose
    // order this is now that the card no longer rides on their head
    private const string cardPath = "Assets/food-icons/speech-bubble 1.png";

    // Stretched to an exact size in both directions rather than fitted by one.
    //
    // Fitting by height alone left the width to whatever the art happened to be
    // -- the last card was 1.26:1 without anybody asking for it, and the spacing
    // arithmetic was done against a square that did not exist
    private static GameObject Card(GameObject parent, Sprite sprite, float width, float height)
    {
        GameObject piece = new GameObject("Card");

        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = new Vector3(0f, 0f, .004f);
        piece.transform.localRotation = Quaternion.identity;

        SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.sortingOrder = 90;

        // Explicit, not inherited. A sprite renderer defaults to white anyway,
        // but a card that comes out grey is the kind of thing that gets blamed
        // on the art instead of on whatever tinted it.
        //
        // Slightly see-through, so the kitchen behind it stays readable through
        // three of these -- but only slightly: past about a fifth the outline
        // goes soft and the card stops looking like an object
        renderer.color = new Color(1f, 1f, 1f, .88f);

        Vector3 own = renderer.bounds.size;

        piece.transform.localScale = new Vector3(
            own.x > .0001f ? width / own.x : 1f,
            own.y > .0001f ? height / own.y : 1f,
            1f);

        return piece;
    }

    private const string sunburstPath = "Assets/food-icons/Sunburst.png";

    // Tinted green in code rather than tinted here: the pack's mark is white,
    // and a white source can be any colour later without a second file
    private const string tickPath =
        "Assets/Layer Lab/2D Icons-PictoIconPack01/Icons/PictoIcon_256/Icon_PictoIcon_Check.Png";

    // The customer who has not decided. From the same pack as the tick on
    // purpose: they are both marks rather than pictures of things, and a
    // question mark drawn in one language beside food drawn in another reads as
    // two cards stuck together
    private const string undecidedPath =
        "Assets/Layer Lab/2D Icons-PictoIconPack01/Icons/PictoIcon_256/" +
        "Icon_PictoIcon_Mark_Question-1.Png";

    // The rays behind the emoji at the end. Built switched off -- it belongs to
    // the last two seconds of a customer's visit and nothing else
    private static GameObject Sunburst(GameObject parent, Vector3 offset, float height)
    {
        GameObject piece = new GameObject("Sunburst");

        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = offset;
        piece.transform.localRotation = Quaternion.identity;

        SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();

        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sunburstPath);

        // Over the card's 90, under the emoji's 120
        renderer.sortingOrder = 105;

        // The art is very nearly white, so it wants colouring rather than only
        // dimming. Left as it comes, over a pale kitchen floor, it is invisible
        renderer.color = new Color(1f, .84f, .32f, .75f);

        float own = renderer.bounds.size.y;

        piece.transform.localScale = own > .0001f ? Vector3.one * (height / own) : Vector3.one;

        piece.SetActive(false);

        return piece;
    }

    private const string discPath = "Assets/food-icons/Countdown_Disc.png";

    // The disc behind the number, drawn here rather than found.
    //
    // Every pack in the project has a folder of shapes and one of them is
    // probably a circle, but "probably" means opening twenty files to find out
    // and then depending on that pack forever. A circle is four lines of code
    // and it comes out white, which is what a thing that gets tinted green,
    // amber and red at runtime needs to be
    private static Sprite DiscSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(discPath);

        if (existing != null)
            return existing;

        const int pixels = 128;

        Texture2D texture = new Texture2D(pixels, pixels, TextureFormat.ARGB32, false);

        Vector2 middle = new Vector2(pixels * .5f - .5f, pixels * .5f - .5f);

        float radius = pixels * .5f - 2f;

        for (int y = 0; y < pixels; y++)
        {
            for (int x = 0; x < pixels; x++)
            {
                // One pixel of feather at the rim. Without it the edge is a
                // staircase, and a staircase is what a circle is not
                float edge = radius - Vector2.Distance(new Vector2(x, y), middle);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge)));
            }
        }

        texture.Apply();

        System.IO.File.WriteAllBytes(discPath, texture.EncodeToPNG());

        Object.DestroyImmediate(texture);

        AssetDatabase.Refresh();
        FoodIconBaker.MakeSprite(discPath);

        return AssetDatabase.LoadAssetAtPath<Sprite>(discPath);
    }

    // The draining collar. A mesh rather than a filled Image, for the reason
    // written on RadialTimer itself -- a radial fill needs a Canvas, and this
    // bubble has spent two rounds proving it does not want one
    private static GameObject Radial(GameObject parent, Vector3 offset, float inner, float outer)
    {
        GameObject piece = new GameObject("Patience Ring",
            typeof(MeshFilter), typeof(MeshRenderer), typeof(RadialTimer));

        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = offset;
        piece.transform.localRotation = Quaternion.identity;

        MeshRenderer ring = piece.GetComponent<MeshRenderer>();

        // White on the asset, tinted per customer at runtime off an instanced
        // copy -- so one customer running out of time does not turn every other
        // ring in the queue red
        ring.sharedMaterial = UnlitMaterial("Timer", Color.white);

        // Said out loud now that the collar sits half over the card.
        //
        // It never crossed anything before, so its order stayed at the default
        // zero -- and zero is behind the card's 90. Depth alone would probably
        // have carried it, the ring being nearer the camera and drawn opaque,
        // but "probably" is decided by a material setting in another file.
        // Between the card and the emoji, which is where a collar belongs
        ring.sortingOrder = 119;

        SerializedObject so = new SerializedObject(piece.GetComponent<RadialTimer>());

        so.FindProperty("innerRadius").floatValue = inner;
        so.FindProperty("outerRadius").floatValue = outer;

        so.ApplyModifiedProperties();

        return piece;
    }

    private static GameObject Disc(GameObject parent, Vector3 offset, float height)
    {
        GameObject piece = new GameObject("Countdown Disc");

        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = offset;
        piece.transform.localRotation = Quaternion.identity;

        SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();

        renderer.sprite = DiscSprite();

        // Over the card and under its own number
        renderer.sortingOrder = 125;

        float own = renderer.bounds.size.y;

        piece.transform.localScale = own > .0001f ? Vector3.one * (height / own) : Vector3.one;

        return piece;
    }

    private static GameObject Quad(GameObject parent, string name, Color colour,
        Vector3 scale, Vector3 offset)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Quad);

        piece.name = name;

        Object.DestroyImmediate(piece.GetComponent<Collider>());

        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = offset;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = scale;

        piece.GetComponent<Renderer>().sharedMaterial = UnlitMaterial(name, colour);

        return piece;
    }

    private static GameObject Text(GameObject parent, string name, Vector3 offset, float size,
        int sortingOrder)
    {
        // Created WITH the component. TextMeshPro needs a RectTransform, and
        // adding it afterwards makes Unity swap the Transform out underneath a
        // parenting and a position that were already set
        GameObject piece = new GameObject(name, typeof(TextMeshPro));

        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = offset;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = Vector3.one;

        TextMeshPro text = piece.GetComponent<TextMeshPro>();

        text.text = "";

        // Scaled with the card. A point size fixed in code stops matching the
        // moment anybody resizes the bubble, which is the first thing they do
        text.fontSize = size * 6f;
        text.color = new Color(.13f, .13f, .16f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;

        // The reason the count was invisible.
        //
        // Text draws through a MeshRenderer whose sorting order starts at zero,
        // while the card is a sprite at 90 -- and between two transparent
        // renderers the ORDER decides, not which is nearer the camera. The
        // number was in front of the card in space and behind it on screen
        Renderer renderer = piece.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sortingOrder = sortingOrder;

        RectTransform rect = piece.GetComponent<RectTransform>();

        // Wide enough that "x3" is not clipped down to "3". It was, and the
        // missing letter read as a design choice rather than as a cut off box
        if (rect != null)
            rect.sizeDelta = new Vector2(size * 1.1f, size * .4f);

        return piece;
    }

    private static GameObject Emoji(GameObject parent, Sprite sprite, Vector3 offset, float height,
        float drawn)
    {
        GameObject piece = new GameObject("Emoji");

        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = offset;
        piece.transform.localRotation = Quaternion.identity;

        SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.sortingOrder = 120;

        // Pixel art at a hundred pixels per unit comes out over a unit across,
        // which is bigger than the whole bubble.
        //
        // Scaled by the drawn face where it could be measured, by the file
        // where it could not. The file is a square with a circle in it and
        // transparent margins all round -- fitting THAT to the hole is what
        // left a ring of empty card between the face and the collar
        float own = drawn > .0001f ? drawn : renderer.bounds.size.y;

        piece.transform.localScale = own > .0001f ? Vector3.one * (height / own) : Vector3.one;

        return piece;
    }

    // How big the face actually is inside its file, in the sprite's own units.
    //
    // The tight sprite mesh is Unity's own outline of the opaque pixels, so this
    // is the drawn circle rather than the picture it was saved in -- and it
    // costs nothing, unlike reading the texture, which needs the file marked
    // readable and re-imported first.
    //
    // The BIGGEST of the three moods, because the sprite is swapped at runtime
    // and the transform is not. Fitting to the smile would let a wider angry
    // face grow out past the collar the moment the customer's patience turned
    private static float FaceSize(params Sprite[] moods)
    {
        float biggest = 0f;

        foreach (Sprite mood in moods)
        {
            if (mood == null)
                continue;

            float drawn = DrawnSize(mood);

            if (drawn > biggest)
                biggest = drawn;
        }

        return biggest;
    }

    private static float DrawnSize(Sprite sprite)
    {
        Vector2[] corners = sprite.vertices;

        // FullRect sprites -- and anything under 32 pixels, which Unity refuses
        // to trim -- hand back the four corners of the file. Then this is the
        // file's own size, which is the old behaviour rather than a wrong number
        if (corners == null || corners.Length <= 0)
            return sprite.bounds.size.y;

        Vector2 low = corners[0];
        Vector2 high = corners[0];

        foreach (Vector2 corner in corners)
        {
            low = Vector2.Min(low, corner);
            high = Vector2.Max(high, corner);
        }

        // The WIDER of the two. The hole is a circle, so what has to fit in it
        // is the face's longest side -- a mood drawn with a tear off to one side
        // is wider than it is tall, and fitting it by height puts the tear in
        // the collar
        float size = Mathf.Max(high.x - low.x, high.y - low.y);

        return size > .0001f ? size : sprite.bounds.size.y;
    }

    private static Material UnlitMaterial(string name, Color colour)
    {
        string path = materialsFolder + "/Bubble " + name + ".mat";

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (existing != null)
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", colour);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", colour);

        if (!AssetDatabase.IsValidFolder(materialsFolder))
            AssetDatabase.CreateFolder("Assets/Tiny Coffee Shop", "Materials");

        AssetDatabase.CreateAsset(material, path);

        return material;
    }

    private static Sprite LoadEmoji(string name)
    {
        string path = emojiFolder + "/" + name + ".png";

        // Imported TIGHT, not merely as a sprite.
        //
        // The face is fitted to the outline of its opaque pixels, and that
        // outline only exists if Unity was asked to trim one. Left at Full Rect
        // the mesh is the four corners of the file, the measurement comes back
        // as the whole square, and the face sits in the middle of the collar
        // with a ring of empty card around it -- which is the gap all of this
        // is for. The setting ships with the pack, so it is not ours to assume
        Tighten(path);

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Tighten(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            return;

        TextureImporterSettings settings = new TextureImporterSettings();

        importer.ReadTextureSettings(settings);

        // Re-importing what is already right is slow for nothing, and this runs
        // three times per command
        if (importer.textureType == TextureImporterType.Sprite &&
            importer.spriteImportMode == SpriteImportMode.Single &&
            settings.spriteMeshType == SpriteMeshType.Tight &&
            importer.alphaIsTransparency)
            return;

        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.Tight;
        settings.alphaIsTransparency = true;

        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
}

public static class Emoji45Setup
{
    private const string customersFolder =
        "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";
    private const string holderName = "Emoji 45 Animated";

    private const string happyPath =
        "Assets/Emojis 45/Prefabs/Stroked emojis in World Space Canvas/" +
        "Canvas Emoji happy 2 Stroke.prefab";
    private const string neutralPath =
        "Assets/Emojis 45/Prefabs/Basic emojis in World Space Canvas/" +
        "Canvas Emoji beg.prefab";
    private const string angryPath =
        "Assets/Emojis 45/Prefabs/Stroked emojis in World Space Canvas/" +
        "Canvas Emoji angry Stroke.prefab";

    [MenuItem("Cooked Fast/Musteri/Emoji 45 Paketini Entegre Et", priority = 601)]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Emoji 45",
                "Once Play'i kapat. Musteri prefablarini guvenli sekilde " +
                "duzenlemek icin oyun calismiyor olmali.", "Tamam");
            return;
        }

        if (!Assets(out GameObject happy, out GameObject neutral,
                out GameObject angry, out string problem))
        {
            EditorUtility.DisplayDialog("Emoji 45", problem, "Tamam");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab",
            new[] { customersFolder });
        StringBuilder report = new StringBuilder();
        int changed = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                CustomerOrder order = root.GetComponent<CustomerOrder>();

                if (order == null)
                    continue;

                SerializedObject so = new SerializedObject(order);
                GameObject bubble = so.FindProperty("bubbleRoot")
                    .objectReferenceValue as GameObject;

                if (bubble == null)
                {
                    report.AppendLine(root.name + ": Order Bubble yok, atlandi");
                    continue;
                }

                float face = ExistingFaceSize(so);

                if (Install(bubble, order, face, report,
                        happy, neutral, angry))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();

        report.Insert(0,
            "Emoji 45 entegrasyonu\n\n" +
            "Mutlu  -> happy 2\n" +
            "Idare  -> beg (Basic/Emoji beg.psd)\n" +
            "Kizgin -> angry\n\n");
        report.AppendLine();
        report.AppendLine(changed + " musteri prefabi guncellendi.");
        report.AppendLine("Eski statik emojiler silinmedi; kapali yedek olarak duruyor.");

        EditorUtility.DisplayDialog("Emoji 45", report.ToString(), "Tamam");
    }

    // Called by OrderBubbleSetup too, so rebuilding the whole card later keeps
    // the purchased animated faces instead of quietly reverting to pixel art.
    public static bool Install(GameObject bubble, CustomerOrder order,
        float faceHeight, StringBuilder report)
    {
        if (!Assets(out GameObject happy, out GameObject neutral,
                out GameObject angry, out string problem))
        {
            report?.AppendLine("  Emoji 45: " + problem);
            return false;
        }

        return Install(bubble, order, faceHeight, report,
            happy, neutral, angry);
    }

    private static bool Install(GameObject bubble, CustomerOrder order,
        float faceHeight, StringBuilder report, GameObject happyPrefab,
        GameObject neutralPrefab, GameObject angryPrefab)
    {
        if (bubble == null || order == null)
            return false;

        SerializedObject so = new SerializedObject(order);
        SpriteRenderer old = so.FindProperty("emoji").objectReferenceValue
            as SpriteRenderer;

        Transform previous = bubble.transform.Find(holderName);
        Vector3 position = old != null
            ? old.transform.localPosition
            : previous != null ? previous.localPosition : Vector3.zero;
        Quaternion rotation = old != null
            ? old.transform.localRotation
            : previous != null ? previous.localRotation : Quaternion.identity;

        if (previous != null)
            Object.DestroyImmediate(previous.gameObject);

        faceHeight = faceHeight > .01f ? faceHeight : .7f;

        GameObject holder = new GameObject(holderName);
        holder.transform.SetParent(bubble.transform, false);
        holder.transform.localPosition = position;
        holder.transform.localRotation = rotation;
        holder.transform.localScale = Vector3.one;

        int sortingLayer = old != null ? old.sortingLayerID : 0;

        GameObject happy = Add(happyPrefab, holder.transform, "Happy",
            faceHeight, sortingLayer);
        GameObject neutral = Add(neutralPrefab, holder.transform, "Neutral",
            faceHeight, sortingLayer);
        GameObject angry = Add(angryPrefab, holder.transform, "Angry",
            faceHeight, sortingLayer);

        if (happy == null || neutral == null || angry == null)
        {
            Object.DestroyImmediate(holder);
            report?.AppendLine("  Emoji 45 prefab ornegi olusturulamadi");
            return false;
        }

        happy.SetActive(true);
        neutral.SetActive(false);
        angry.SetActive(false);

        // Preserve the old, hand-positioned object as a recoverable backup.
        // CustomerOrder no longer references it, so it cannot draw on top of
        // the animated canvas or keep receiving sprite swaps.
        if (old != null)
        {
            old.gameObject.SetActive(false);

            if (!old.gameObject.name.Contains("ESKI"))
                old.gameObject.name += " (ESKI - kapali)";
        }

        so.Update();
        so.FindProperty("emoji").objectReferenceValue = null;
        so.FindProperty("animatedEmojiRoot").objectReferenceValue =
            holder.transform;
        so.FindProperty("animatedHappyEmoji").objectReferenceValue = happy;
        so.FindProperty("animatedNeutralEmoji").objectReferenceValue = neutral;
        so.FindProperty("animatedAngryEmoji").objectReferenceValue = angry;
        so.ApplyModifiedPropertiesWithoutUndo();

        report?.AppendLine("  " + order.name + ": Emoji 45 hazir");
        return true;
    }

    private static GameObject Add(GameObject source, Transform parent,
        string label, float faceHeight, int sortingLayer)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(source, parent)
            as GameObject;

        if (instance == null)
            return null;

        instance.name = label;

        RectTransform rect = instance.transform as RectTransform;
        float authored = rect != null
            ? Mathf.Max(rect.rect.width, rect.rect.height)
            : 512f;

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one *
            (faceHeight / Mathf.Max(1f, authored));

        Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);

        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].overrideSorting = true;
            canvases[i].sortingLayerID = sortingLayer;
            canvases[i].sortingOrder = 120;
        }

        UnityEngine.UI.Graphic[] graphics =
            instance.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        return instance;
    }

    private static float ExistingFaceSize(SerializedObject so)
    {
        SpriteRenderer old = so.FindProperty("emoji").objectReferenceValue
            as SpriteRenderer;

        if (old == null || old.sprite == null)
            return .7f;

        Vector3 own = old.sprite.bounds.size;
        Vector3 scale = old.transform.localScale;

        return Mathf.Max(Mathf.Abs(own.x * scale.x),
                         Mathf.Abs(own.y * scale.y));
    }

    private static bool Assets(out GameObject happy, out GameObject neutral,
        out GameObject angry, out string problem)
    {
        happy = AssetDatabase.LoadAssetAtPath<GameObject>(happyPath);
        neutral = AssetDatabase.LoadAssetAtPath<GameObject>(neutralPath);
        angry = AssetDatabase.LoadAssetAtPath<GameObject>(angryPath);

        if (happy != null && neutral != null && angry != null)
        {
            problem = null;
            return true;
        }

        problem = "Paket prefablarindan biri bulunamadi:\n" +
                  (happy == null ? happyPath + "\n" : "") +
                  (neutral == null ? neutralPath + "\n" : "") +
                  (angry == null ? angryPath : "");
        return false;
    }
}
