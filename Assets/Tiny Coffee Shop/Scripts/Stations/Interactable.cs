using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Marks something the player can walk up to and use, and says where to stand.
//
// This replaces the trigger volumes for the PLAYER. A trigger answers "am I
// inside it", which needs a box big enough to stand in, placed so the player can
// reach it without walking through the counter -- two things that are invisible
// while playing and wrong more often than they are right. A stand point answers
// "where do I go", which is one dot anyone can drag and see.
//
// The station triggers stay in the scene on purpose: the workers still walk into
// them, and PlayerDetector still runs for them
public class Interactable : MonoBehaviour
{
    [Header(" Elements ")]
    [Tooltip("Oyuncunun gelip duracagi nokta. Bos ise objenin kendi konumu")]
    [SerializeField] private Transform standPoint;

    [Header(" Settings ")]
    [Tooltip("Noktaya bu kadar yaklasinca etkilesim olur")]
    [SerializeField] private float reach = 1.4f;

    [Tooltip("Console'da bu isimle gorunur. Bos ise objenin adi")]
    [SerializeField] private string label;

    // The player walks for a second or two before anything happens, and until
    // this the tap produced nothing at all on screen -- so there was no way to
    // tell a registered tap from a missed one until the walk was over
    [Header(" Tiklama Geri Bildirimi ")]
    [Tooltip("Tiklaninca zipllayacak obje. Bos ise tabak bulunur, yoksa obje kendisi")]
    [SerializeField] private Transform popTarget;

    [Tooltip("Ne kadar buyusun. 1.08 = yuzde 8. 0 birakilirsa 1.08 kullanilir")]
    [SerializeField] private float popScale = 1.08f;

    [Tooltip("Kac saniyede acilip kapansin. 0 = varsayilan, EKSI = zipllama kapali")]
    [SerializeField] private float popDuration = .18f;

    // Read through these, never straight off the fields.
    //
    // Both arrived after Interactable was already sitting on seventeen objects
    // in a saved scene, and a serialized field added to a component that is
    // already on disk does not reliably come back carrying its initializer. A
    // zero duration is a station that accepts the tap, calls Pop, returns
    // immediately and looks broken -- with nothing logged, because as far as the
    // code is concerned the author switched it off on purpose.
    //
    // So zero means "nobody set this" and takes the default. Switching it off
    // is a negative number, which nothing can arrive at by accident
    private float PopScale => popScale > 1.001f ? popScale : 1.08f;
    private float PopDuration => Mathf.Abs(popDuration) < .001f ? .18f : popDuration;

    public Transform StandPoint => standPoint;
    public Vector3 StandPosition => standPoint == null ? transform.position : standPoint.position;
    public float Reach => reach;
    public string Label => string.IsNullOrEmpty(label) ? name : label;

    private Vector3 popBaseScale = Vector3.one;
    private Coroutine popRoutine;

    private void Awake()
    {
        // Not "is it empty" but "is it any good". A target saved into the scene
        // by an older run of the setup command outlives the rule that picked it,
        // and the two ways it can be wrong -- invisible, or carving the navmesh
        // -- are exactly the two that produce no pop and no error. Checking here
        // means the scene repairs itself on Play instead of needing a menu item
        // The wheel gets its own clause because it is the one case the check
        // above cannot catch: a cheese counter whose stored target is some
        // visible but wrong lump of cabinet passes "would it ever show" and
        // keeps bouncing the wrong thing. On that station the wheel is the
        // answer by definition, so it does not wait for the menu command
        Transform wheel = WheelOf(gameObject);

        if (WouldNeverShow(gameObject, popTarget) ||
            (wheel != null && popTarget != wheel && !Carves(wheel)))
        {
            Transform found = ResolvePopTarget(gameObject);

            if (popTarget != null && found != popTarget)
                Debug.Log("[Pop] " + name + ": kayitli hedef " + popTarget.name +
                          " ise yaramaz, " + (found == null ? "YOK" : found.name) +
                          " kullaniliyor", this);

            popTarget = found;
        }

        if (popTarget == null)
        {
            Debug.LogWarning(name + ": zipllayacak gorunur bir sey bulunamadi -- " +
                             "tiklayinca tepki vermez.\n" +
                             "Interactable > Pop Target'a elle bir mesh ata.", this);
            return;
        }

        // Read once, before anything has stretched it. Reading it at pop time
        // instead would compound: tap twice quickly and the plate grows
        popBaseScale = popTarget.localScale;
    }

    // What the food sits on, most specific first.
    //
    // A pan belongs to the oven and a basket to the fryer, so a station carrying
    // one has told us exactly which piece the player is aiming at. Plateau is
    // late because nearly everything has one and some are bare transforms
    private static readonly string[] popNames =
    {
        "pan", "basket", "Plateau", "tray", "plate"
    };

    // Whole words, not substrings.
    //
    // The oven's pan is called "pan" and this list once said "Environment_Pan",
    // which matches nothing -- so the oven fell through to its Plateau and the
    // pan never moved. Loosening it to a plain Contains fixes that and breaks
    // something worse: every station's timer lives on a UI object, and "Panel"
    // contains "pan". Requiring a boundary on both sides matches "pan",
    // "Environment_Pan" and "Pan 2" while leaving "Panel" alone
    private static bool NameMatches(string name, string wanted)
    {
        name = name.ToLower();
        wanted = wanted.ToLower();

        for (int at = name.IndexOf(wanted); at >= 0; at = name.IndexOf(wanted, at + 1))
        {
            int after = at + wanted.Length;

            bool leftClear = at == 0 || !char.IsLetterOrDigit(name[at - 1]);
            bool rightClear = after >= name.Length || !char.IsLetterOrDigit(name[after]);

            if (leftClear && rightClear)
                return true;
        }

        return false;
    }

    public static Transform ResolvePopTarget(GameObject station)
    {
        return ResolvePopTarget(station, out _);
    }

    // The one place that answers "which piece of this station should bounce".
    // Both Awake and the editor setup command call it: there were two copies of
    // this and they disagreed, so the editor wrote one target into the scene and
    // the runtime would have chosen another.
    //
    // The reason comes back with the answer because every rejection here is
    // silent on screen -- a station that picked nothing and a station that
    // picked the wrong thing look identical, which cost two rounds of guessing
    public static Transform ResolvePopTarget(GameObject station, out string why)
    {
        Transform root = station.transform;

        // Asked of the component instead of matched by name, because every test
        // below is a test for a renderer and the wheel has none until Play
        // builds it. This is the whole reason the cheese counter never bounced:
        // the one piece worth bouncing was thrown out for being empty
        Transform wheel = WheelOf(station);

        if (wheel != null && !Carves(wheel))
        {
            why = "peynir tekerlegi (oyun sirasinda dolar)";
            return wheel;
        }

        List<string> skipped = new List<string>();

        for (int i = 0; i < popNames.Length; i++)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == root || !NameMatches(candidate.name, popNames[i]))
                    continue;

                // A timer ring is UI, not a prop. Scaling one fights the layout
                // and moves nothing the player reads as the station
                if (candidate is RectTransform)
                {
                    skipped.Add(candidate.name + " (UI)");
                    continue;
                }

                if (Carves(candidate))
                {
                    skipped.Add(candidate.name + " (navmesh oyuyor)");
                    continue;
                }

                if (!HasVisual(candidate))
                {
                    skipped.Add(candidate.name + " (mesh yok)");
                    continue;
                }

                why = "isim eslesti: " + popNames[i] + Note(skipped);
                return candidate;
            }
        }

        // Nothing named, so the whole station -- but only when moving it will
        // not drag a carving NavMeshObstacle along and rebuild the navmesh
        if (!Carves(root) && HasVisual(root))
        {
            why = "isim eslesmedi, butun istasyon" + Note(skipped);
            return root;
        }

        // Something here carves, so the biggest visible piece that does not.
        // Scaling a child leaves an obstacle on the parent exactly where it was
        Transform best = null;
        float bestSize = 0f;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || Carves(renderer.transform))
                continue;

            float size = renderer.bounds.size.sqrMagnitude;

            if (size <= bestSize)
                continue;

            best = renderer.transform;
            bestSize = size;
        }

        why = (best == null
                  ? "gorunur ve navmesh oymayan hicbir parca yok"
                  : "isim eslesmedi, en buyuk gorunur parca") + Note(skipped);

        return best;
    }

    private static string Note(List<string> skipped)
    {
        return skipped.Count <= 0 ? "" : "  [atlandi: " + string.Join(", ", skipped) + "]";
    }

    // Two ways a target is worthless, and neither of them says anything on
    // screen. Nothing to draw is a fifth of a second spent scaling an empty
    // transform. Carving is worse: moving a NavMeshObstacle rebuilds the mesh
    // around it once per frame for the length of the pop, while every customer
    // in the queue is pathfinding through it -- that is what froze the table
    public static bool WouldNeverShow(GameObject station, Transform target)
    {
        if (target == null)
            return true;

        if (Carves(target))
            return true;

        if (HasVisual(target))
            return false;

        // Empty now, full a frame later. The exception, and the only one
        return WheelOf(station) != target;
    }

    private static Transform WheelOf(GameObject station)
    {
        CheeseWheelStation wheel = station.GetComponentInChildren<CheeseWheelStation>(true);

        return wheel == null ? null : wheel.WheelCentre;
    }

    private static bool HasVisual(Transform target)
    {
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is ParticleSystemRenderer) && renderer.enabled)
                return true;
        }

        return false;
    }

    // Only what would move WITH it. An obstacle on a parent stays exactly where
    // it is when a child is scaled, so it does not disqualify the child
    private static bool Carves(Transform target)
    {
        return target.GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>(true) != null;
    }

    public void Pop()
    {
        if (PopDuration <= 0f || popTarget == null || !isActiveAndEnabled)
            return;

        if (popRoutine != null)
            StopCoroutine(popRoutine);

        popRoutine = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        Transform target = popTarget;

        float duration = PopDuration;
        float amount = PopScale - 1f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            // Out and back on one curve. A sine hump is 0 at both ends and 1 in
            // the middle, so there is no seam where a two-stage tween would
            // hand over -- and no tuning to hide one
            float bump = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);

            target.localScale = popBaseScale * (1f + amount * bump);

            yield return null;
        }

        target.localScale = popBaseScale;
        popRoutine = null;
    }

    // Right click the Interactable header in the inspector. Says what this
    // station would actually do when tapped, which is otherwise only knowable
    // by tapping it and watching very carefully
    [ContextMenu("Pop: ne yapacagini soyle")]
    private void ExplainPop()
    {
        Transform picked = ResolvePopTarget(gameObject, out string why);

        Transform found = WouldNeverShow(gameObject, popTarget) ? picked : popTarget;

        Debug.Log("[Pop] " + name +
                  "\n  kayitli: " + (popTarget == null ? "bos" : popTarget.name) +
                  (popTarget != null && found != popTarget ? "  (ise yaramaz, degistirilecek)" : "") +
                  "\n  hedef : " + (found == null ? "YOK -- tiklayinca tepki vermez" : found.name) +
                  "\n  secim : " + (picked == null ? "YOK" : picked.name) + " -- " + why +
                  "\n  sure  : " + PopDuration.ToString("0.000") + " sn" +
                  (PopDuration < 0f ? "  (kapali)" : "") +
                  "\n  olcek : " + PopScale.ToString("0.000") +
                  "\n  alandaki ham degerler: popDuration " + popDuration.ToString("0.000") +
                  ", popScale " + popScale.ToString("0.000"),
                  this);
    }

    // The stand point and how close is close enough are the two numbers that
    // decide whether this thing works, and neither shows up on screen
    private void OnDrawGizmosSelected()
    {
        Vector3 where = StandPosition;

        Gizmos.color = new Color(.2f, 1f, .4f, .9f);
        Gizmos.DrawSphere(where, .12f);

        Gizmos.color = new Color(.2f, 1f, .4f, .35f);
        Gizmos.DrawWireSphere(where, reach);

        Gizmos.DrawLine(transform.position, where);
    }
}
