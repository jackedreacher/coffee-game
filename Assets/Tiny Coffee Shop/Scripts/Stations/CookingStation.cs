using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The oven. Unlike every other station here it takes food as well as giving it:
// raw meat goes into the pan, cooks on a timer, and comes back out as a
// different type. Nothing anywhere has to remember to refuse raw meat at the
// counter -- CookedMeat simply is not Meat, and the type check already in
// FoodDropZone does the rest
public class CookingStation : MonoBehaviour
{
    [Header(" Elements ")]
    [Tooltip("Etin pisirilirken duracagi yer -- tencerenin ici")]
    [SerializeField] private Transform cookPoint;

    [SerializeField] private SpawnableFood rawFoodPrefab;
    [SerializeField] private SpawnableFood cookedFoodPrefab;
    [SerializeField] private Transform workerTargetPoint;

    [Header(" Sayac ")]
    [Tooltip("Bos ve her sey pismisken gizlenir")]
    [SerializeField] private GameObject timerRoot;

    [Tooltip("Image Type = Filled, Fill Method = Radial 360")]
    [SerializeField] private Image timerFill;

    // In world units, not canvas units. The canvas is 100 wide at a hundredth
    // scale, and working out which of those two to change to make the ring
    // smaller is not a thing anyone should have to do twice
    [Tooltip("Halkanin capi (dunya birimi). Kucultmek icin burayi kullan")]
    [SerializeField] private float timerSize = .35f;

    [Header(" Ates ")]
    [Tooltip("Pisirme sirasinda yanacak efekt. Ocagin altina konur")]
    [SerializeField] private GameObject fireEffect;

    [Header(" Settings ")]
    [Tooltip("Bir etin pismesi kac saniye surecek")]
    [SerializeField] private float cookDuration = 6f;
    [SerializeField][Range(1, 6)] private int capacity = 1;

    [Tooltip("Birden fazla parca pisiyorsa aralarindaki mesafe")]
    [SerializeField] private float spacing = .12f;

    // Only while it is in the pan. Changing the food prefab instead would change
    // it everywhere -- on the tray, in the customer's hands, at every station
    [Tooltip("Tavadaki etin buyuklugu. Tepsideki boyutuna dokunmaz")]
    [SerializeField] private float panScale = 1f;

    [Header(" Yanma ")]
    // The grace period is the cook time again, so one number balances both: an
    // oven that cooks slowly also forgives slowly
    [Tooltip("Pistikten sonra ne kadar beklenirse uyari baslar. 0 = pisirme suresi kadar")]
    [SerializeField] private float burnGrace;

    [Tooltip("Uyari basladiktan sonra yanmasina kac saniye kalir")]
    [SerializeField] private float burnWarningTime = 2f;

    [Tooltip("Uyari isareti saniyede kac kez yanip sonsun")]
    [SerializeField] private float warningBlinksPerSecond = 4f;

    [Tooltip("Yanmak uzereyken yanip sonecek unlem isareti")]
    [SerializeField] private GameObject warningRoot;

    [Tooltip("Yanan etin uzerinde cikacak ates. Et alininca yok olur")]
    [SerializeField] private GameObject burnEffect;

    private float BurnGrace => burnGrace > .001f ? burnGrace : cookDuration;

    private class Slot
    {
        public SpawnableFood item;
        public float timer;
        public bool cooked;

        // Runs only once cooked. Kept separate from timer rather than letting
        // that one carry on, because the ring reads timer against cookDuration
        // and a value past the end would leave it showing a full circle
        public float burnTimer;
        public bool burnt;
        public GameObject fire;

        // What the item was before the pan stretched it, so handing it out can
        // put it back. Without this the pan size rides along onto the tray:
        // FoodPosition.Push zeroes position and rotation but never scale
        public Vector3 scale;
    }

    private readonly List<Slot> slots = new List<Slot>();

    public SpawnableFood CookedFoodPrefab => cookedFoodPrefab;
    public Vector3 WorkerTargetPosition =>
        workerTargetPoint == null ? transform.position : workerTargetPoint.position;

    public bool HasRoom => slots.Count < capacity;

    public bool HasCooked
    {
        get
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].cooked)
                    return true;
            }

            return false;
        }
    }

    // Raw only, and only of the one kind this oven cooks. A salad handed to an
    // oven would otherwise sit there forever and never turn into anything
    public bool CanAccept(SpawnableFood food)
    {
        if (food == null || rawFoodPrefab == null || !HasRoom)
            return false;

        return food.GetType() == rawFoodPrefab.GetType();
    }

    private void Start()
    {
        ApplyTimerLook();
        ShowTimer();
    }

    // In edit mode too, so the number in the inspector and the ring in the scene
    // agree while it is being dragged rather than only once the game runs
    private void OnValidate()
    {
        ApplyTimerLook();

        // Dragging the pan size while the game runs resizes what is already
        // frying, rather than only showing up on the next piece
        if (Application.isPlaying)
            Arrange();
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

        // Whatever the oven prop is scaled to divides out, so the number in the
        // inspector is the diameter actually seen on screen
        float parent = transform.lossyScale.x;

        parent = Mathf.Abs(parent) < .0001f ? 1f : Mathf.Abs(parent);

        rect.localScale = Vector3.one * (timerSize / (side * parent));
    }

    private void Update()
    {
        Cook();
        ShowTimer();
        ShowWarning();
    }

    private void Cook()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];

            if (!slot.cooked)
            {
                slot.timer += Time.deltaTime;

                if (slot.timer >= cookDuration)
                    Swap(slot, i);

                continue;
            }

            if (slot.burnt)
                continue;

            slot.burnTimer += Time.deltaTime;

            if (slot.burnTimer >= BurnGrace + Mathf.Max(0f, burnWarningTime))
                Burn(slot);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool loggedWarning;
#endif

    // Past the grace period and not yet on fire. The window the exclamation mark
    // is asking the player to beat
    private bool AnyAboutToBurn()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].cooked && !slots[i].burnt && slots[i].burnTimer >= BurnGrace)
                return true;
        }

        return false;
    }

    private void Burn(Slot slot)
    {
        slot.burnt = true;

        if (slot.item == null)
            return;

        slot.item.MarkAsBurnt();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Ocak] " + name + ": et YANDI (" + slot.item.name + ")" +
                  "\n  IsBurnt: " + slot.item.IsBurnt +
                  "\n  Burn Effect: " + (burnEffect == null ? "BOS -- ates cikmaz" : burnEffect.name),
                  this);
#endif

        if (burnEffect == null)
            return;

        // Parented to the food, not to the oven. The flames have to travel with
        // it: the player can still pick a burnt piece up, and it has to look
        // like the thing on fire is the thing in their hand
        slot.fire = Instantiate(burnEffect, slot.item.transform);

        slot.fire.transform.localPosition = Vector3.zero;
        slot.fire.transform.localRotation = Quaternion.identity;
    }

    // Blinks only while something is in the warning window, and is switched off
    // the rest of the time. Driven from unscaled-independent game time so the
    // blink rate does not depend on the frame rate
    private void ShowWarning()
    {
        bool warn = AnyAboutToBurn();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The window opening is the moment the whole thing either works or is
        // silently missing a reference, and those look identical on screen
        if (warn != loggedWarning)
        {
            loggedWarning = warn;

            if (warn)
                Debug.Log("[Ocak] " + name + ": UYARI penceresi acildi, " +
                          Mathf.Max(0f, burnWarningTime).ToString("0.0") + " sn sonra yanacak" +
                          "\n  Warning Root: " + (warningRoot == null
                              ? "BOS -- unlem gorunmez. Cooked Fast > Ocak: Yanma Uyarisini Kur"
                              : warningRoot.name),
                          this);
        }
#endif

        if (warningRoot == null)
            return;

        if (!warn)
        {
            if (warningRoot.activeSelf)
                warningRoot.SetActive(false);

            return;
        }

        float rate = Mathf.Max(.1f, warningBlinksPerSecond);

        warningRoot.SetActive(Mathf.Repeat(Time.time * rate, 1f) < .5f);
    }

    // The raw instance is replaced rather than relabelled: the two are different
    // meshes and different types, and a component swapped at runtime would leave
    // the old one's serialized fields behind
    private void Swap(Slot slot, int index)
    {
        if (cookedFoodPrefab == null)
        {
            // Nothing to turn into. Better to stop the timer than to loop the
            // ring forever on a station that was never finished being wired
            slot.cooked = true;
            return;
        }

        if (slot.item != null)
            Destroy(slot.item.gameObject);

        slot.item = Instantiate(cookedFoodPrefab, cookPoint, false);
        slot.cooked = true;

        // Read before the pan size is applied: the cooked prefab has its own
        // scale and it is that one, not the raw one, that goes out on the tray
        slot.scale = slot.item.transform.localScale;

        Place(slot, index);
    }

    // Filled in only while something is actually cooking. A ring left on screen
    // at zero reads as a broken oven rather than an idle one
    private void ShowTimer()
    {
        Slot next = null;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].cooked)
                continue;

            if (next == null || slots[i].timer > next.timer)
                next = slots[i];
        }

        if (timerRoot != null)
            timerRoot.SetActive(next != null);

        if (timerFill != null && next != null)
            timerFill.fillAmount = cookDuration <= 0f ? 1f : Mathf.Clamp01(next.timer / cookDuration);

        // Not the ring's condition. The ring answers "is anything still
        // cooking"; the hob answers "is there anything in the pan", and food
        // sitting there going from done to burnt is very much still in the pan
        ShowFire(slots.Count > 0);
    }

    // Nullable so the first call always applies. A plain false would match the
    // opening state and return early, leaving a prefab that ships playing to
    // burn under an empty oven for the whole game
    private bool? burning;

    private void ShowFire(bool shouldBurn)
    {
        if (fireEffect == null || burning == shouldBurn)
            return;

        // The very first call, which is Start deciding the opening state rather
        // than the oven going out
        bool opening = !burning.HasValue;

        burning = shouldBurn;

        ParticleSystem[] systems = fireEffect.GetComponentsInChildren<ParticleSystem>(true);

        if (systems.Length <= 0)
        {
            fireEffect.SetActive(shouldBurn);
            return;
        }

        if (shouldBurn)
        {
            if (!fireEffect.activeSelf)
                fireEffect.SetActive(true);

            foreach (ParticleSystem system in systems)
                system.Play(false);

            return;
        }

        // Nothing has been lit yet, so there is nothing to let burn out. Cutting
        // it dead is right here and only here -- the earlier version activated
        // the object on its way to stopping it, which lit a Play On Awake effect
        // under an empty oven and left it going
        if (opening)
        {
            fireEffect.SetActive(false);
            return;
        }

        // SetActive on a running effect is a cut: the flames vanish mid-frame.
        // Stopping the emission lets what is already alight burn out on its own
        foreach (ParticleSystem system in systems)
            system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    public void PutIn(SpawnableFood raw)
    {
        if (raw == null || cookPoint == null)
            return;

        // Not world-preserving, for the same reason FoodPosition.Push is not:
        // the food is coming off a tray on a moving arm, and preserving its world
        // transform would bake that arm's pose and scale into the pan
        raw.transform.SetParent(cookPoint, false);

        slots.Add(new Slot
        {
            item = raw,
            timer = 0f,
            cooked = false,
            scale = raw.transform.localScale,
        });

        Arrange();
    }

    // What TakeCooked would hand over, without taking it.
    //
    // Needed because the decision about where a piece may go has to be made
    // against the piece. Asking the PREFAB instead is the same object for a
    // good piece and a burnt one -- same type, same everything -- and only the
    // instance carries the burn
    public SpawnableFood PeekCooked()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].cooked)
                return slots[i].item;
        }

        return null;
    }

    public SpawnableFood TakeCooked()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].cooked)
                continue;

            SpawnableFood item = slots[i].item;

            // The flames stay at the oven. The meat leaves black and useless,
            // which is the part the player has to deal with; a piece still
            // alight in the hand would read as something still happening
            if (slots[i].fire != null)
                Destroy(slots[i].fire);

            // Handed back the size it would have been anywhere else. The pan
            // size is the pan's business and must not leave with the food
            if (item != null)
                item.transform.localScale = slots[i].scale;

            slots.RemoveAt(i);

            Arrange();

            return item;
        }

        return null;
    }

    private void Arrange()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item != null)
                Place(slots[i], i);
        }
    }

    // Laid out along the pan rather than stacked: two patties on top of each
    // other look like one, and the point of the second one is seeing it
    private void Place(Slot slot, int index)
    {
        float middle = (capacity - 1) * .5f;

        Transform item = slot.item.transform;

        item.localPosition = Vector3.right * ((index - middle) * spacing);
        item.localRotation = Quaternion.identity;
        item.localScale = slot.scale * Mathf.Max(.01f, panScale);
    }
}
