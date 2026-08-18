using UnityEngine;
using UnityEngine.UI;

// A station with no input and nothing on show.
//
// The oven is fed: raw meat goes into the pan and comes back out cooked, and
// half its code is about refusing the wrong food. This one is asked instead. Tap
// it to start, tap it again when the oil has filled to take a portion. Nothing
// is carried in, so nothing can be carried in wrong.
//
// The fries are never built until they are taken. A portion sitting in the
// basket waiting to be collected is a thing the player has to look past, and the
// oil rising is already the whole story: empty means idle, filling means wait,
// full means come and get it.
//
// Driven by tapping rather than by standing in a trigger, for the same reason
// the holding shelf is: a trigger would start it, hand the fries over and start
// it again on the very next frame, forever
public class FryerStation : MonoBehaviour
{
    [Header(" Elements ")]
    [Tooltip("Ne uretiyor")]
    [SerializeField] private SpawnableFood foodPrefab;

    [Tooltip("Dolan yag -- sure boyunca sifirdan kendi boyuna buyur")]
    [SerializeField] private Transform oilVolume;

    // The one that is actually on screen. The volume sits inside an opaque tub,
    // so from a camera looking down at the kitchen scaling it alone changes
    // nothing visible -- which is exactly what a frozen oil level looks like
    [Tooltip("Yagin ust yuzeyi -- doluluga gore inip cikar. Yukaridan gorunen bu")]
    [SerializeField] private Transform oilSurface;

    [Tooltip("Isaretliyse yuzeyin bos yuksekligi asagidaki sayidan alinir")]
    [SerializeField] private bool overrideSurfaceEmptyY;

    [Tooltip("Yuzeyin bos haldeki yerel Y'si. Isaretli degilse yag hacminin dibi kullanilir")]
    [SerializeField] private float surfaceEmptyY;

    [Tooltip("Oyuncunun gelip duracagi nokta. Bos ise istasyonun kendisi")]
    [SerializeField] private Transform standPoint;

    [Header(" Sayac ")]
    [Tooltip("Sadece kizarirken gorunur. Sadece yag dolsun istiyorsan burayi bosalt")]
    [SerializeField] private GameObject timerRoot;

    [Tooltip("Image Type = Filled, Fill Method = Radial 360")]
    [SerializeField] private Image timerFill;

    // In world units, not canvas units. The canvas is 100 wide at a hundredth
    // scale, and working out which of those two to change to make the ring
    // smaller is not a thing anyone should have to do twice
    [Tooltip("Halkanin capi (dunya birimi). Kucultmek icin burayi kullan")]
    [SerializeField] private float timerSize = .35f;

    [Header(" Settings ")]
    [Tooltip("Patatesin kizarmasi kac saniye surecek")]
    [SerializeField] private float fryDuration = 5f;

    // Deliberately its own copy of the oven's settings rather than a shared
    // one. The oven burns per piece, because it can hold several and they went
    // in at different times; the fryer has one portion or none, and forcing the
    // two onto one abstraction would either break the oven's per-piece timers
    // or leave the fryer carrying machinery for a case it does not have
    [Header(" Yanma ")]
    [Tooltip("Kizardiktan sonra ne kadar beklenirse uyari baslar. 0 = kizartma suresi kadar")]
    [SerializeField] private float burnGrace;

    [Tooltip("Uyari basladiktan sonra yanmasina kac saniye kalir")]
    [SerializeField] private float burnWarningTime = 2f;

    [Tooltip("Uyari isareti saniyede kac kez yanip sonsun")]
    [SerializeField] private float warningBlinksPerSecond = 4f;

    [Tooltip("Yanmak uzereyken yanip sonecek uyari isareti")]
    [SerializeField] private GameObject warningRoot;

    // The oven's pair, on the fryer.
    //
    // The two stations say the same two things -- come and get it, and come and
    // get it NOW -- and the fryer only had the second one. A machine that warns
    // but never says it is done teaches the player to watch it instead of
    // trusting it
    [Tooltip("Kizardigini soyleyen tik. Yanma uyarisi cikinca gizlenir")]
    [SerializeField] private GameObject readyRoot;

    [Tooltip("Yanik patatesin uzerinde cikacak ates. Patates alininca yok olur")]
    [SerializeField] private GameObject burnEffect;

    private float BurnGrace => burnGrace > .001f ? burnGrace : fryDuration;

    public enum Result
    {
        Started,
        Frying,
        Taken,
        HandFull,
        Broken
    }

    private enum State
    {
        Idle,
        Frying,
        Ready,
        Burnt
    }

    private State state;
    private float timer;

    // Runs while the portion sits in the oil waiting to be collected. Separate
    // from timer, which the ring reads against fryDuration
    private float burnTimer;
    private GameObject fire;

    // Where the oil was authored. Read once, before anything has moved it, so
    // "full" means what the scene was built with rather than whatever the last
    // frame happened to leave behind
    private Vector3 oilFullScale = Vector3.one;
    private Vector3 oilFullPosition;

    // How far the oil mesh hangs below its own origin. A mesh pivoted through
    // its middle scaled down towards zero shrinks away from both ends at once,
    // and oil that pulls away from the bottom of the fryer as it empties reads
    // as a bug. With this the underside is pinned and only the top moves
    private float oilBottom;

    private Vector3 surfaceFullPosition;

    public Vector3 StandPosition => standPoint == null ? transform.position : standPoint.position;

    public bool IsIdle => state == State.Idle;
    public bool IsFrying => state == State.Frying;
    public bool IsReady => state == State.Ready;

    private void Awake()
    {
        if (oilVolume != null)
        {
            oilFullScale = oilVolume.localScale;
            oilFullPosition = oilVolume.localPosition;

            MeshFilter filter = oilVolume.GetComponent<MeshFilter>();

            if (filter != null && filter.sharedMesh != null)
                oilBottom = filter.sharedMesh.bounds.min.y;
        }

        if (oilSurface != null)
            surfaceFullPosition = oilSurface.localPosition;
    }

    private void Start()
    {
        ApplyTimerLook();
        ApplyOil();
        ShowTimer();

        // Silence here is the one failure that looks like nothing happening at
        // all, so it says so instead
        if (oilVolume == null && oilSurface == null)
            Debug.LogWarning(name + ": Oil Volume ve Oil Surface bos -- yag seviyesi " +
                             "hic degismez.\nCooked Fast > Patates: 1 - Fritozlari Kur calistir.",
                             this);
    }

    // Only the ring, and only in edit mode. Emptying the oil here as well would
    // drain it in the scene view and save it that way, leaving the fryer looking
    // broken in the editor forever
    private void OnValidate()
    {
        ApplyTimerLook();
    }

    // Only the size. Where the ring sits is left exactly where it was dragged --
    // driving that from here would undo the placement every time the inspector
    // was touched
    private void ApplyTimerLook()
    {
        if (timerRoot == null || timerSize <= 0f)
            return;

        RectTransform rect = timerRoot.transform as RectTransform;

        if (rect == null)
            return;

        float side = Mathf.Max(rect.sizeDelta.x, 1f);

        // Whatever the prop is scaled to divides out, so the number in the
        // inspector is the diameter actually seen on screen
        float parent = transform.lossyScale.x;

        parent = Mathf.Abs(parent) < .0001f ? 1f : Mathf.Abs(parent);

        rect.localScale = Vector3.one * (timerSize / (side * parent));
    }

    private void Update()
    {
        if (state == State.Frying)
        {
            timer += Time.deltaTime;

            if (timer >= fryDuration)
            {
                state = State.Ready;
                timer = fryDuration;
                burnTimer = 0f;
            }

            ApplyOil();
        }
        else if (state == State.Ready)
        {
            burnTimer += Time.deltaTime;

            if (burnTimer >= BurnGrace + Mathf.Max(0f, burnWarningTime))
                Burn();
        }

        ShowTimer();
        ShowWarning();
        ShowReady();
    }

    // The tick, and it steps aside for the warning.
    //
    // The two marks are a pair and read as one: the tick says come and get it,
    // the flashing one says come and get it NOW. Both on the same fryer at once
    // would be the machine arguing with itself
    private void ShowReady()
    {
        if (readyRoot == null)
            return;

        bool ready = state == State.Ready && !AboutToBurn;

        if (readyRoot.activeSelf != ready)
            readyRoot.SetActive(ready);
    }

    // Past the grace period and not yet alight -- the window the mark is asking
    // the player to beat
    private bool AboutToBurn => state == State.Ready && burnTimer >= BurnGrace;

    private void Burn()
    {
        state = State.Burnt;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Fritoz] " + name + ": patates YANDI" +
                  "\n  Burn Effect: " + (burnEffect == null ? "BOS -- ates cikmaz" : burnEffect.name),
                  this);
#endif

        if (burnEffect == null || fire != null)
            return;

        // Parented to the station, not to the food: there is no food yet. The
        // portion is not built until it is taken, which is the whole design of
        // this station -- nothing sits in the basket looking collectable
        Transform where = oilSurface != null ? oilSurface : transform;

        fire = Instantiate(burnEffect, where);

        fire.transform.localPosition = Vector3.zero;
        fire.transform.localRotation = Quaternion.identity;
    }

    private void ShowWarning()
    {
        if (warningRoot == null)
            return;

        if (!AboutToBurn)
        {
            if (warningRoot.activeSelf)
                warningRoot.SetActive(false);

            return;
        }

        float rate = Mathf.Max(.1f, warningBlinksPerSecond);

        warningRoot.SetActive(Mathf.Repeat(Time.time * rate, 1f) < .5f);
    }

    // One tap, and what it does depends on what the fryer is doing. The result
    // is handed back rather than logged here, so the caller can say what
    // happened in the same voice as everything else it reports
    public Result Tap(HoldFoodAbility hand)
    {
        switch (state)
        {
            case State.Idle:
                return Begin();

            case State.Frying:
                return Result.Frying;

            case State.Ready:
            case State.Burnt:
                return Take(hand);
        }

        return Result.Broken;
    }

    private Result Begin()
    {
        if (foodPrefab == null)
            return Result.Broken;

        state = State.Frying;
        timer = 0f;

        ApplyOil();

        return Result.Started;
    }

    private Result Take(HoldFoodAbility hand)
    {
        if (hand == null || foodPrefab == null)
            return Result.Broken;

        // Asked with the prefab rather than with a portion: every check in there
        // is on the type, so a full hand costs nothing built and thrown away
        if (!hand.CanTake(foodPrefab))
            return Result.HandFull;

        SpawnableFood portion = Instantiate(foodPrefab);

        // Marked BEFORE it is handed over. Everything downstream -- the burger,
        // the plateau, the serving check -- decides from the instance, and the
        // oven proved what happens when that decision is made a moment too
        // early: the burn is not there yet and the piece goes into a burger
        if (state == State.Burnt)
            portion.MarkAsBurnt();

        if (!hand.TryPush(portion))
        {
            Destroy(portion.gameObject);

            return Result.HandFull;
        }

        if (fire != null)
        {
            Destroy(fire);
            fire = null;
        }

        state = State.Idle;
        timer = 0f;
        burnTimer = 0f;

        if (warningRoot != null && warningRoot.activeSelf)
            warningRoot.SetActive(false);

        // On the same frame the portion leaves, not on the next one. A tick
        // still up over an empty fryer is a tick that means nothing, and it is
        // up for exactly as long as it takes the player to look back
        if (readyRoot != null && readyRoot.activeSelf)
            readyRoot.SetActive(false);

        ApplyOil();

        return Result.Taken;
    }

    private void ApplyOil()
    {
        float fill = 0f;

        // Burnt counts as full: the portion is still in there, it is just no
        // longer worth eating
        if (state == State.Ready || state == State.Burnt)
            fill = 1f;
        else if (state == State.Frying)
            fill = fryDuration <= 0f ? 1f : Mathf.Clamp01(timer / fryDuration);

        if (oilVolume != null)
        {
            Vector3 scale = oilFullScale;

            scale.y = oilFullScale.y * fill;

            oilVolume.localScale = scale;

            // Pushed down by exactly the height the shrinking took off the
            // bottom, which is zero when the mesh is already pivoted on its
            // underside
            Vector3 position = oilFullPosition;

            position.y = oilFullPosition.y + oilBottom * oilFullScale.y * (1f - fill);

            oilVolume.localPosition = position;
        }

        if (oilSurface == null)
            return;

        Vector3 surface = surfaceFullPosition;

        surface.y = Mathf.Lerp(SurfaceEmptyY, surfaceFullPosition.y, fill);

        oilSurface.localPosition = surface;
    }

    // The underside of the oil column, so an empty fryer puts its surface where
    // the oil would start rather than at a number picked out of the air. Both
    // objects hang off the same parent, which is what makes the two local Y
    // values comparable
    private float SurfaceEmptyY
    {
        get
        {
            if (overrideSurfaceEmptyY)
                return surfaceEmptyY;

            if (oilVolume != null)
                return oilFullPosition.y + oilBottom * oilFullScale.y;

            return 0f;
        }
    }

    // Filled in only while something is actually frying. A ring left on screen at
    // zero reads as a broken fryer rather than an idle one
    private void ShowTimer()
    {
        if (timerRoot != null)
            timerRoot.SetActive(state == State.Frying);

        if (timerFill != null)
            timerFill.fillAmount = fryDuration <= 0f ? 1f : Mathf.Clamp01(timer / fryDuration);
    }
}
