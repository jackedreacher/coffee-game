using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The row of hearts at the top of the screen, plus the life-loss flight.
//
// A lost heart stays full while its copy pops over the customer, breaks, then
// flies into the row. Only on arrival does the real slot become broken. This is
// deliberately independent of the money celebration: losing a life must never
// manufacture cash or borrow the cash animation's timing.
public class LivesHud : MonoBehaviour
{
    [Header(" Elements ")]
    [Tooltip("Soldan saga can slotlari")]
    [SerializeField] private Image[] hearts;

    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;
    [SerializeField] private Sprite brokenHeart;
    [SerializeField] private Sprite sunburst;

    [Header(" Renkler ")]
    [SerializeField] private Color fullColour = new Color(.91f, .26f, .30f);

    [Tooltip("Eski sahnelerde alfa 0 gelirse kod guvenli kirmiziyi kullanir")]
    [SerializeField] private Color brokenColour = new Color(.38f, .38f, .42f, 1f);

    [Tooltip("Kirik kalp resmi bagli degilse kullanilan eski bos kalp rengi")]
    [SerializeField] private Color emptyColour = new Color(.32f, .27f, .29f, .5f);

    [Header(" Can Kaybi Efekti ")]
    [Tooltip("Musterinin ustunde buyume ve kirilma suresi")]
    [SerializeField] private float popTime = .34f;

    [Tooltip("Kirik kalbin HUD'a ucma suresi")]
    [SerializeField] private float flightTime = .62f;

    [Tooltip("Ucus yayinin ekran yuksekligine orani")]
    [SerializeField] private float flightLift = .08f;

    [Tooltip("PNG icindeki seffaf pay farkini dengeler; kirik ve dolu kalbin gorunen boyu esit olur")]
    [SerializeField] private float brokenScale = .78f;

    private readonly HashSet<int> pending = new HashSet<int>();
    private Lives lives;
    private GameObject flightCanvas;
    private Vector3[] heartBaseScales;

    private Color BrokenColour =>
        brokenColour.a > .01f ? brokenColour : new Color(.38f, .38f, .42f, 1f);

    private float PopTime => popTime > .01f ? popTime : .34f;
    private float FlightTime => flightTime > .01f ? flightTime : .62f;
    private float FlightLift => flightLift > .001f ? flightLift : .08f;
    private float BrokenScale => brokenScale > .01f ? brokenScale : .78f;

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
        StopAllCoroutines();
        pending.Clear();

        if (flightCanvas != null)
            Destroy(flightCanvas);

        flightCanvas = null;
    }

    private void Update()
    {
        if (lives == null)
            Bind();
    }

    private void Bind()
    {
        if (lives != null || Lives.Instance == null)
            return;

        lives = Lives.Instance;
        lives.Changed += Draw;
        lives.Lost += LoseVisual;

        RememberHeartScales();
        Draw();
    }

    private void RememberHeartScales()
    {
        if (hearts == null || heartBaseScales != null && heartBaseScales.Length == hearts.Length)
            return;

        heartBaseScales = new Vector3[hearts.Length];

        for (int i = 0; i < hearts.Length; i++)
            heartBaseScales[i] = hearts[i] == null
                ? Vector3.one
                : hearts[i].rectTransform.localScale;
    }

    private void Unbind()
    {
        if (lives != null)
        {
            lives.Changed -= Draw;
            lives.Lost -= LoseVisual;
        }

        lives = null;
    }

    private void Draw()
    {
        if (hearts == null || lives == null)
            return;

        for (int i = 0; i < hearts.Length; i++)
        {
            Image heart = hearts[i];

            if (heart == null)
                continue;

            bool exists = i < lives.Max;
            heart.gameObject.SetActive(exists);

            if (!exists)
                continue;

            // Pending keeps the destination intact until the flying heart has
            // actually reached it. Otherwise the result appears before cause.
            bool held = i < lives.Left || pending.Contains(i);

            heart.sprite = held
                ? fullHeart
                : brokenHeart != null ? brokenHeart : emptyHeart;
            heart.color = held ? fullColour : BrokenColour;

            // Both Images keep the exact same RectTransform dimensions. Only
            // compensate for transparent pixels baked into the generated PNG;
            // multiply the hand-authored slot scale instead of replacing it.
            Vector3 authored = heartBaseScales != null && i < heartBaseScales.Length
                ? heartBaseScales[i]
                : Vector3.one;
            bool usesBrokenArtwork = !held && brokenHeart != null;
            heart.rectTransform.localScale = authored * (usesBrokenArtwork ? BrokenScale : 1f);
        }
    }

    private void LoseVisual(int remaining, Vector3 source)
    {
        if (hearts == null || hearts.Length == 0)
            return;

        int slot = Mathf.Clamp(remaining, 0, hearts.Length - 1);
        Image target = hearts[slot];

        if (target == null)
            return;

        pending.Add(slot);
        Draw();

        StartCoroutine(FlyBrokenHeart(slot, source, target));
    }

    private IEnumerator FlyBrokenHeart(int slot, Vector3 source, Image target)
    {
        Canvas canvas = FlightCanvas();

        if (canvas == null)
        {
            Settle(slot);
            yield break;
        }

        float size = Mathf.Clamp(Screen.width * .23f, 120f, 210f);
        GameObject effect = new GameObject("Lost Heart", typeof(RectTransform));
        effect.transform.SetParent(canvas.transform, false);

        RectTransform holder = effect.GetComponent<RectTransform>();
        holder.anchorMin = holder.anchorMax = Vector2.zero;
        holder.pivot = new Vector2(.5f, .5f);
        holder.sizeDelta = new Vector2(size, size);

        Vector2 start = SourcePoint(source);
        holder.anchoredPosition = start;

        Image burst = ChildImage(holder, "Sunburst", sunburst, Color.white, size * 1.65f);
        Image flying = ChildImage(holder, "Heart", fullHeart, fullColour, size);

        if (burst != null)
            burst.transform.SetAsFirstSibling();

        holder.localScale = Vector3.zero;

        float elapsed = 0f;
        bool broke = false;

        while (elapsed < PopTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float through = Mathf.Clamp01(elapsed / PopTime);
            float scale = EaseOutBack(through);

            holder.localScale = Vector3.one * scale;

            if (burst != null)
            {
                burst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, through * 80f);
                burst.color = new Color(1f, .88f, .22f, 1f - through * .35f);
            }

            if (!broke && through >= .62f)
            {
                broke = true;
                flying.sprite = brokenHeart != null ? brokenHeart : emptyHeart;
                flying.color = BrokenColour;

                if (brokenHeart != null)
                    flying.rectTransform.localScale = Vector3.one * BrokenScale;
            }

            yield return null;
        }

        if (!broke)
        {
            flying.sprite = brokenHeart != null ? brokenHeart : emptyHeart;
            flying.color = BrokenColour;

            if (brokenHeart != null)
                flying.rectTransform.localScale = Vector3.one * BrokenScale;
        }

        Vector2 from = holder.anchoredPosition;
        elapsed = 0f;

        while (elapsed < FlightTime && target != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float through = Mathf.Clamp01(elapsed / FlightTime);
            float eased = through * through * (3f - 2f * through);
            Vector2 destination = TargetPoint(target);
            Vector2 place = Vector2.LerpUnclamped(from, destination, eased);

            place.y += Mathf.Sin(through * Mathf.PI) * Screen.height * FlightLift;
            holder.anchoredPosition = place;
            holder.localScale = Vector3.one * Mathf.Lerp(1f, .28f, eased);

            if (burst != null)
                burst.color = new Color(1f, .88f, .22f, 1f - through);

            yield return null;
        }

        if (effect != null)
            Destroy(effect);

        Settle(slot);

        if (target != null)
            yield return Punch(target.rectTransform);
    }

    private void Settle(int slot)
    {
        pending.Remove(slot);
        Draw();
    }

    private IEnumerator Punch(RectTransform target)
    {
        Vector3 original = target.localScale;
        float duration = .22f;
        float elapsed = 0f;

        while (elapsed < duration && target != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float through = Mathf.Clamp01(elapsed / duration);
            float amount = Mathf.Sin(through * Mathf.PI) * .28f;
            target.localScale = original * (1f + amount);
            yield return null;
        }

        if (target != null)
            target.localScale = original;
    }

    private Canvas FlightCanvas()
    {
        if (flightCanvas != null)
            return flightCanvas.GetComponent<Canvas>();

        flightCanvas = new GameObject("Life Loss Effects", typeof(RectTransform), typeof(Canvas));

        Canvas canvas = flightCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32001;

        return canvas;
    }

    private static Image ChildImage(
        RectTransform parent, string childName, Sprite sprite, Color colour, float size)
    {
        if (sprite == null)
            return null;

        GameObject child = new GameObject(childName, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);

        Image image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.color = colour;
        image.preserveAspect = true;
        image.raycastTarget = false;

        return image;
    }

    private static Vector2 SourcePoint(Vector3 source)
    {
        Camera camera = Camera.main;

        if (camera == null || source == Vector3.zero)
            return new Vector2(Screen.width * .5f, Screen.height * .55f);

        Vector3 point = camera.WorldToScreenPoint(source);

        if (point.z <= 0f)
            return new Vector2(Screen.width * .5f, Screen.height * .55f);

        return new Vector2(
            Mathf.Clamp(point.x, 40f, Screen.width - 40f),
            Mathf.Clamp(point.y, 40f, Screen.height - 40f));
    }

    private static Vector2 TargetPoint(Image target)
    {
        Canvas canvas = target.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.WorldToScreenPoint(camera, target.rectTransform.position);
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float shifted = value - 1f;

        return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
    }
}
