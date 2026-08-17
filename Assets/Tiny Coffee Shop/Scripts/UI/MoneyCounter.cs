using System.Collections;
using TMPro;
using UnityEngine;

// The money counter at the top of the screen, as a destination.
//
// Two different things fly money into it -- the number off a customer's bubble
// and, for customers without one, banknotes from the till -- and neither should
// have to know how to find it. There are two CurrencyTexts in this scene and
// one of them lives in the HR panel under a CanvasGroup at alpha 0. Money
// flying into that one looks exactly like money disappearing.
//
// Makes itself on first use: nothing to wire, so nothing to forget to wire
public class MoneyCounter : MonoBehaviour
{
    private static MoneyCounter instance;

    private RectTransform counter;
    private Canvas canvas;
    private Canvas flights;
    private Vector3 rest = Vector3.one;
    private Coroutine punch;

    public static MoneyCounter Instance
    {
        get
        {
            if (instance != null)
                return instance;

            GameObject host = new GameObject("Money Counter");
            instance = host.AddComponent<MoneyCounter>();
            DontDestroyOnLoad(host);

            return instance;
        }
    }

    // Where it is on screen, in pixels.
    //
    // Falls back to the top middle rather than refusing. Something arriving in
    // roughly the right place beats a number that changes with nothing moving
    public Vector2 ScreenPoint
    {
        get
        {
            RectTransform target = Locate();

            if (target == null)
                return new Vector2(Screen.width * .5f, Screen.height * .93f);

            // An overlay canvas has no camera, and asking with one anyway
            // answers about a projection that is not the one being drawn
            Camera source = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.WorldToScreenPoint(source, target.position);
        }
    }

    // Paid at the moment something visibly lands in it, never before
    public void Deposit(int amount)
    {
        if (amount <= 0 || CurrencyManager.instance == null)
            return;

        CurrencyManager.instance.AddCurrency(amount);
        Punch();
    }

    // Flies a copy of a world label into the counter and deposits when it lands.
    //
    // The copy is UI, not a world object, and that is the whole trick. A screen
    // space overlay canvas draws on top of every 3D object there is, whatever
    // its sorting order -- a world label flying at the counter goes UNDER the
    // card it is flying into, and the last thing the player sees is the number
    // disappearing behind the hearts. So what flies is UI on a canvas of its
    // own, ordered above every other canvas in the game.
    //
    // Run here rather than on whoever launched it. The thing that launches this
    // is a customer's bubble, and the customer walks out and is destroyed a
    // moment later -- a coroutine on a destroyed object stops exactly where it
    // is, which would leave the label hanging in the air and the sale unpaid
    public void FlyText(TextMeshPro source, int amount, float time, float lift)
    {
        Camera eye = Camera.main;

        if (source == null || eye == null)
        {
            Deposit(amount);
            return;
        }

        Vector3 origin = source.transform.position;
        RectTransform flyer = BuildFlyer(source, eye);

        if (flyer == null)
        {
            Deposit(amount);
            return;
        }

        StartCoroutine(Flying(flyer, origin, amount, time, lift));
    }

    private IEnumerator Flying(RectTransform flyer, Vector3 origin, int amount, float time, float lift)
    {
        Camera eye = Camera.main;
        Vector3 restScale = flyer.localScale;

        float flight = time > .01f ? time : .55f;
        float age = 0f;

        while (age < flight && flyer != null)
        {
            age += Time.deltaTime;
            float p = Mathf.Clamp01(age / flight);

            // Both ends read every frame: the camera follows the player, so
            // neither where it left from nor where it is going stays put
            Vector2 from = eye != null ? (Vector2)eye.WorldToScreenPoint(origin) : ScreenPoint;
            Vector2 to = ScreenPoint;

            // p * p leaves slowly and arrives fast, which is what reads as
            // being pulled in rather than merely travelling
            Vector2 flat = Vector2.Lerp(from, to, p * p);
            flat.y += Mathf.Sin(p * Mathf.PI) * Screen.height * lift;

            // An overlay canvas measures its world in screen pixels, so this is
            // the screen point directly -- no projection to undo
            flyer.position = flat;
            flyer.localScale = restScale * Mathf.Lerp(1f, .45f, p * p);

            yield return null;
        }

        if (flyer != null)
            Destroy(flyer.gameObject);

        Deposit(amount);
    }

    private RectTransform BuildFlyer(TextMeshPro source, Camera eye)
    {
        Canvas above = Flights();

        if (above == null)
            return null;

        GameObject host = new GameObject("Earnings");
        host.transform.SetParent(above.transform, false);

        TextMeshProUGUI label = host.AddComponent<TextMeshProUGUI>();

        label.text = source.text;
        label.color = source.color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        if (source.font != null)
            label.font = source.font;

        // A reference size, not the final one.
        //
        // TMP's font size is a POINT size, not a height. The same number is a
        // different height in every font, and a world label's number means
        // nothing on a canvas at all -- converting one to the other by
        // arithmetic is what filled the screen with a single "$3". So it is set
        // to something arbitrary, measured, and then scaled to what is wanted
        const float reference = 100f;

        label.fontSize = reference;

        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(reference * 8f, reference * 2f);

        label.ForceMeshUpdate();

        float drawn = label.textBounds.size.y;
        float wanted = ScreenHeight(source, eye);

        rect.localScale = Vector3.one * (drawn > .01f ? wanted / drawn : 1f);

        // Only x and y. An overlay canvas lives at z 0 and the projected depth
        // would put the label somewhere the canvas is not
        rect.position = (Vector2)eye.WorldToScreenPoint(source.transform.position);

        return rect;
    }

    // The height the world label actually occupies on screen, in pixels.
    //
    // Measured off what is drawn, not worked out from a font size. Projecting
    // eight corners is arithmetic; the relationship between a TMP point size
    // and a height on screen is not
    private static float ScreenHeight(TextMeshPro source, Camera eye)
    {
        Renderer drawn = source.GetComponent<Renderer>();

        if (drawn == null)
            return Screen.height * .05f;

        Bounds box = drawn.bounds;

        float top = float.MinValue;
        float bottom = float.MaxValue;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? box.min.x : box.max.x,
                (i & 2) == 0 ? box.min.y : box.max.y,
                (i & 4) == 0 ? box.min.z : box.max.z);

            float y = eye.WorldToScreenPoint(corner).y;

            top = Mathf.Max(top, y);
            bottom = Mathf.Min(bottom, y);
        }

        // Capped whatever the measurement says. Getting this wrong once already
        // covered the whole display with one number, and a ceiling costs
        // nothing next to being sure
        return Mathf.Clamp(top - bottom, 14f, Screen.height * .12f);
    }

    // No CanvasScaler on purpose: the scale factor stays 1, so a font size in
    // pixels is a font size in pixels and the position is the screen point
    private Canvas Flights()
    {
        if (flights != null)
            return flights;

        GameObject host = new GameObject("Money Flights");
        host.transform.SetParent(transform, false);

        flights = host.AddComponent<Canvas>();
        flights.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above every other canvas in the game. The flying number is the one
        // thing that must not end up behind what it is flying into
        flights.sortingOrder = 32000;

        return flights;
    }

    // Jumps once per arrival. Seeing the number change comes before reading
    // what it changed to
    public void Punch()
    {
        if (Locate() == null)
            return;

        if (punch != null)
            StopCoroutine(punch);

        punch = StartCoroutine(Punching());
    }

    private IEnumerator Punching()
    {
        const float time = .18f;
        float age = 0f;

        while (age < time && counter != null)
        {
            age += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(age / time);
            counter.localScale = rest * (1f + Mathf.Sin(p * Mathf.PI) * .22f);
            yield return null;
        }

        if (counter != null)
            counter.localScale = rest;

        punch = null;
    }

    // Re-found whenever it is gone: this object survives a scene change and the
    // counter it was pointing at does not
    private RectTransform Locate()
    {
        if (counter != null)
            return counter;

        CurrencyText[] all = FindObjectsByType<CurrencyText>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (CurrencyText text in all)
        {
            if (text == null || !Visible(text.transform))
                continue;

            RectTransform rect = text.transform as RectTransform;

            if (rect == null)
                continue;

            counter = rect;
            rest = rect.localScale;

            Canvas found = rect.GetComponentInParent<Canvas>(true);
            canvas = found != null ? found.rootCanvas : null;

            return counter;
        }

        return null;
    }

    // Three ways a counter is on screen in name only, and all three of them
    // look healthy in the hierarchy
    private static bool Visible(Transform target)
    {
        if (!target.gameObject.activeInHierarchy)
            return false;

        for (Transform walk = target; walk != null; walk = walk.parent)
        {
            if (walk.TryGetComponent(out CanvasGroup group) && group.alpha < .01f)
                return false;

            if (walk.TryGetComponent(out Canvas found) && !found.enabled)
                return false;
        }

        return true;
    }
}
