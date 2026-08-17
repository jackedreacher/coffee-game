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

    [MenuItem("Cooked Fast/Musteri: Siparis Balonunu Kur", priority = 600)]
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
        bool reimported = FoodIconBaker.MakeSprite(sunburstPath);

        reimported |= FoodIconBaker.MakeSprite(cardPath);

        if (reimported)
            AssetDatabase.Refresh();

        Sprite happy = LoadEmoji(happyEmoji);
        Sprite neutral = LoadEmoji(neutralEmoji);
        Sprite angry = LoadEmoji(angryEmoji);

        StringBuilder report = new StringBuilder();

        report.AppendLine("Isin: " +
            (AssetDatabase.LoadAssetAtPath<Sprite>(sunburstPath) == null
                ? "BULUNAMADI " + sunburstPath
                : "Sunburst.png"));
        report.AppendLine();

        report.AppendLine("Emojiler");
        report.AppendLine("  mutlu : " + (happy == null ? "BULUNAMADI " + happyEmoji : happy.name));
        report.AppendLine("  idare : " + (neutral == null ? "BULUNAMADI " + neutralEmoji : neutral.name));
        report.AppendLine("  kizgin: " + (angry == null ? "BULUNAMADI " + angryEmoji : angry.name));
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
        report.AppendLine();
        // Both figures, because they are different questions. The file is what
        // the neighbours have to clear; the panel is what the player reads
        report.AppendLine("Balon dosyasi: " + CardWidth.ToString("0.00") + " en x " +
                          bubbleSize.ToString("0.00") + " boy (kuyruk dahil).");
        report.AppendLine("  Okunan kutu: " + (CardWidth * .94f).ToString("0.00") + " x " +
                          (bubbleSize * .69f).ToString("0.00"));
        report.AppendLine("  Elle degistirmek icin prefabi ac, " + bubbleName + "'in");
        report.AppendLine("  Scale'ini buyut -- ic olculer kendiliginden uyar.");
        report.AppendLine("  Kalici olsun istersen OrderBubbleSetup > bubbleSize.");
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
            // panel can hold anything. Measured off the file: the drawn box runs
            // from 4% to 73% of the image height and the spike carries on to
            // 95%. Centring content on the IMAGE would drop the food into the
            // spike, so every offset below is taken from the box instead
            const float boxTop = size * .46f;
            const float boxBottom = -size * .23f;
            const float boxMiddle = (boxTop + boxBottom) * .5f;

            Sprite cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(cardPath);

            GameObject card = cardSprite != null
                ? Card(bubble, cardSprite, width, size)
                : Quad(bubble, "Card", Color.white,
                    new Vector3(width, size, 1f), new Vector3(0f, 0f, .004f));

            GameObject anchor = new GameObject("Icon Anchor");

            anchor.transform.SetParent(bubble.transform, false);

            // Dead centre of the drawn box. The food is what the bubble is for
            anchor.transform.localPosition = new Vector3(0f, boxMiddle, -size * .3f);

            // The anchor's scale IS the icon size. CustomerOrder fits the food
            // model into it, so resizing the picture is dragging one number
            const float iconSize = size * .34f;

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

            // Clear of the box rather than half over it. The badge is nearly as
            // tall as the food is now, so overlapping the top edge would put the
            // emoji on the pizza
            Vector3 badge = new Vector3(0f, boxTop + size * .30f, -.03f);

            // The hole in the middle of the ring, and the ONE number everything
            // that sits in it is measured from. The face, the disc and the
            // digits all fill it exactly, so nothing can end up rattling around
            // inside the collar or spilling out past it
            const float hole = size * .40f;

            GameObject timer = Radial(bubble, badge + new Vector3(0f, 0f, .012f),
                hole * .5f, hole * .66f);

            // In the emoji's place, not above it. For the last few seconds the
            // number IS the face -- it takes the spot the eye is already on
            // rather than asking it to look somewhere else at the one moment
            // there is no time to
            Vector3 clock = badge + new Vector3(0f, 0f, -.01f);

            // A hair over the hole. The drawn circle stops two pixels short of
            // its own texture edge for the anti-aliasing, and at this size that
            // reads as a gap between the disc and the collar
            GameObject disc = Disc(bubble, clock, hole * 1.05f);

            GameObject number = Text(bubble, "Countdown",
                clock + new Vector3(0f, 0f, -.01f), size, 130);

            TextMeshPro digits = number.GetComponent<TextMeshPro>();

            digits.fontSize = size * 4.2f;
            digits.color = Color.white;

            RectTransform digitRect = number.GetComponent<RectTransform>();

            if (digitRect != null)
                digitRect.sizeDelta = new Vector2(hole, hole);

            // Behind the emoji as well, and that is settled by SORTING ORDER,
            // not by where these sit in the hierarchy. Sprites are sorted by
            // their order first and their place in the list never comes into it
            GameObject burst = Sunburst(bubble, badge + new Vector3(0f, 0f, .005f), hole * 2.4f);

            GameObject face = Emoji(bubble, happy, badge, hole);

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
            so.FindProperty("emoji").objectReferenceValue = face.GetComponent<SpriteRenderer>();
            so.FindProperty("sunburst").objectReferenceValue = burst.transform;
            so.FindProperty("earningsText").objectReferenceValue = money;

            // Written rather than left alone, because the field's MEANING
            // changed: it used to read zero as "use 40", and now zero is a still
            // burst that only pops. The 40 already saved on these prefabs would
            // otherwise keep spinning under the new rule
            so.FindProperty("sunburstSpin").floatValue = 140f;

            so.FindProperty("happySprite").objectReferenceValue = happy;
            so.FindProperty("neutralSprite").objectReferenceValue = neutral;
            so.FindProperty("angrySprite").objectReferenceValue = angry;

            so.ApplyModifiedProperties();

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

        // White on the asset, tinted per customer at runtime off an instanced
        // copy -- so one customer running out of time does not turn every other
        // ring in the queue red
        piece.GetComponent<MeshRenderer>().sharedMaterial = UnlitMaterial("Timer", Color.white);

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

    private static GameObject Emoji(GameObject parent, Sprite sprite, Vector3 offset, float height)
    {
        GameObject piece = new GameObject("Emoji");

        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = offset;
        piece.transform.localRotation = Quaternion.identity;

        SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.sortingOrder = 120;

        // Pixel art at a hundred pixels per unit comes out over a unit across,
        // which is bigger than the whole bubble
        float own = renderer.bounds.size.y;

        piece.transform.localScale = own > .0001f ? Vector3.one * (height / own) : Vector3.one;

        return piece;
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
        return AssetDatabase.LoadAssetAtPath<Sprite>(emojiFolder + "/" + name + ".png");
    }
}
