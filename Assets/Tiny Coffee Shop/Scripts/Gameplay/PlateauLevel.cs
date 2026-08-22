using UnityEngine;

// Keeps the tray level by turning the WRIST, not by moving the tray.
//
// This started life the other way round and the other way round is wrong. The
// tray was read off the bone, corrected in character space and written back
// over the top -- which is, precisely and unavoidably, "ignore the hand this
// much". Every knob it had (levelling, height hold, leash, lead) was a dial for
// how much of the animation to throw away, so the more it worked the less the
// tray looked attached to anything. In the Hurry clip, which pitches the torso
// further than anything else in the pack, it read as a tray floating near a
// squirrel rather than a tray being carried by one.
//
// A real waiter's tray stays level and never leaves the palm, because the arm
// does the compensating. So that is what this does: the tray is left welded to
// the bone, untouched, and the BONE is rotated after the animator has run until
// the tray sits flat. The tray follows because it is a child. It cannot detach,
// drift or lag, because nothing writes to it at all.
//
// Two properties fall out of that and both were problems before:
//
// * No feedback loop is possible. The animator rewrites the bone from muscle
//   curves every frame, so this correction is transient by construction --
//   applied fresh, never accumulated. The old version wrote to a transform
//   nothing reset, so a constant nudge added to itself every frame and the tray
//   crawled away.
//
// * Placement stops being this script's business. Where the tray sits on the
//   arm is the hand adjuster's job and it stays exactly where it was put.
public class PlateauLevel : MonoBehaviour
{
    [Header(" Elements ")]
    [Tooltip("Karakterin DONEN transformu -- animator'un durdugu gorsel, KOK DEGIL. " +
             "Bos birak, tepsinin bagli oldugu rig otomatik bulunur. Sadece kosma " +
             "hizini olcmek icin kullaniliyor")]
    [SerializeField] private Transform character;

    [Header(" Settings ")]
    [Tooltip("Bilek egimin ne kadarini geri alsin. 0 = hic, tepsi kolla beraber " +
             "yatar. 1 = tepsi her zaman tam yatay. Tepsi HER DURUMDA ele bagli " +
             "kalir, degisen sadece bilegin ne kadar dondugu")]
    [Range(0f, 1f)]
    [SerializeField] private float level = .8f;

    [Tooltip("Bilek en fazla kac derece bukulebilsin. Bunu kaldirmak tepsiyi " +
             "her pozda yatay yapar ama el kirik gorunur")]
    [SerializeField] private float maxBend = 70f;

    [Tooltip("BIRAKMA. Gereken duzeltme maxBend'i bu kadar derece asarsa bilek " +
             "ugrasmayi tamamen birakir. Chef's Kiss gibi elin agza gittigi " +
             "pozlar icin: sinira dayanip titremek yerine sakince pes eder. " +
             "0 = hic birakmaz (eski hali, sinirda titrer)")]
    [SerializeField] private float release = 45f;

    [Tooltip("Duzeltme ne kadar yumusak gelsin, saniye. 0 = aninda")]
    [SerializeField] private float smoothing = .06f;

    [Header(" Kosarken ")]
    [Tooltip("Tam hizda tepsi kac derece ARKAYA yatsin -- hizlanirken uzerindekinin " +
             "kaymamasi icin yapilan sey. Eksi deger one yatirir")]
    [SerializeField] private float runTilt = 8f;

    [Tooltip("Hangi hizda tam yatma olsun. Bunun altinda orantili")]
    [SerializeField] private float runSpeed = 6f;

    [Tooltip("Yatmanin ATALETI. 0 = yumusakca oturur. Yukseldikce kalkista " +
             "fazladan yatip geri salinir")]
    [Range(0f, 1f)]
    [SerializeField] private float bounce = .4f;

    // The bone being corrected -- whatever the tray hangs off
    private Transform mount;

    // Which of the tray's OWN axes counts as up.
    //
    // Not a guess at the model's top: the plateau prefab is not authored axis
    // aligned, its tuned local rotation is something like (318, 272, 19), so
    // there is no "up" to read off it. The only honest definition of level is
    // the placement somebody dialled in and approved, so that pose is what gets
    // measured, once
    private Vector3 trayUp;

    private Quaternion smoothedFix = Quaternion.identity;

    // Measured off the character's own movement rather than asked of a
    // NavMeshAgent or a controller. Both exist here, but they are different
    // components on the player and on a customer, and this only needs one
    // number that both of them already express by moving
    private Vector3 lastPlace;
    private float speed;

    // The run lean and its velocity. A value that eases towards a target can
    // only ever arrive; one with velocity overshoots and comes back, which is
    // the difference between a tray being angled and a tray being CARRIED
    private float tilt;
    private float tiltRate;

    private bool learned;

    private void Start()
    {
        StabiliseMount();

        Transform rig = Visual();

        if (character == null)
            character = rig;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Said out loud rather than silently corrected, because a field somebody
        // filled in by hand is a decision -- and the root is the tempting wrong
        // answer here, since it never rotates in this project
        if (character != rig)
        {
            Debug.LogWarning(
                "[PlateauLevel] character = " + character.name + ", ama tepsinin " +
                "bagli oldugu rig " + rig.name + ". Buraya karakterin DONEN " +
                "transformu girilmeli (animator), kok degil.", this);
        }
#endif

        Learn();
    }

    // A tray may be positioned near a fingertip or directly on an arm while
    // tuning, but neither is a carrying mount. Fingers curl independently and
    // an arm-mounted tray is a sibling of the hand, so in both cases the hand
    // can visibly travel through it. The mapped Hand is the stable end of the
    // humanoid arm chain. A deliberate socket BELOW Hand is left alone.
    //
    // Public so HoldFoodAbility can do this while the inactive plateau is still
    // at its authored scale, BEFORE PopIn turns that scale to zero.
    public void StabiliseMount()
    {
        if (StabiliseFingerMount(transform) && learned)
            Learn();
    }

    // Static because correct parenting is required even when tray levelling is
    // deliberately disabled in the Hand Adjuster.
    public static bool StabiliseFingerMount(Transform tray)
    {
        if (tray == null)
            return false;

        Transform oldMount = tray.parent;

        Animator rig = tray.GetComponentInParent<Animator>(true);

        if (rig == null || !rig.isHuman)
            return false;

        Transform right = rig.GetBoneTransform(HumanBodyBones.RightHand);
        Transform left = rig.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform hand = null;

        Transform rightArm = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform leftArm = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);

        if (right != null && rightArm != null && tray.IsChildOf(rightArm))
            hand = right;
        else if (left != null && leftArm != null && tray.IsChildOf(leftArm))
            hand = left;

        // Direct Hand and a deliberate socket below Hand are already stable.
        // Only an arm ancestor or a finger descendant needs correction.
        bool armAncestor = hand != null && oldMount != hand && hand.IsChildOf(oldMount);
        bool fingerDescendant = hand != null && IsFinger(oldMount) && oldMount.IsChildOf(hand);

        if (hand == null || (!armAncestor && !fingerDescendant))
            return false;

        // World-preserving is intentional here. The hand-adjusted placement is
        // visually approved already; only the unstable parent is wrong.
        tray.SetParent(hand, true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning(
            "[PlateauLevel] Plateau parmak kemigindeydi: " + oldMount.name +
            ". Sabit tasima icin " + hand.name + " altina alindi.", tray);
#endif

        return true;
    }

    private static bool IsFinger(Transform bone)
    {
        if (bone == null)
            return false;

        string key = bone.name.ToLowerInvariant();

        return key.Contains("finger") || key.Contains("thumb") ||
               key.Contains("index") || key.Contains("middle") ||
               key.Contains("ring") || key.Contains("pinky") ||
               key.Contains("little");
    }

    // The transform that actually turns.
    //
    // Walked UP from the tray rather than searched down from the root, and that
    // is the point: the tray is parented into a bone, so the animator above it
    // is by construction the rig carrying it. Searching down would have to
    // choose between a live body and a retired one left switched off in the
    // hierarchy, and this project has both
    private Transform Visual()
    {
        Animator rig = GetComponentInParent<Animator>();

        return rig == null ? transform.root : rig.transform;
    }

    // Split out so it can be re-learned after the tray is re-placed at runtime,
    // which is exactly what the hand adjuster does while play is running
    public void Learn()
    {
        mount = transform.parent;

        // Whatever was pointing up in the approved placement is what will be
        // kept pointing up from now on
        trayUp = Quaternion.Inverse(transform.rotation) * Vector3.up;

        smoothedFix = Quaternion.identity;

        tilt = 0f;
        tiltRate = 0f;

        if (character != null)
            lastPlace = character.position;

        speed = 0f;

        learned = true;
    }

    // Runtime skin selection replaces only the visual rig. The tray object is
    // preserved, so its serialized character reference would otherwise keep
    // pointing at the body that has just been removed and levelling would stop.
    public void RebindCharacter(Transform newCharacter)
    {
        character = newCharacter;
        Learn();
    }

    // After the animation has written the bones, or this would be correcting
    // last frame's pose
    private void LateUpdate()
    {
        if (!learned || mount == null || character == null)
            return;

        // The hand adjuster can move the tray to a different bone mid-play, and
        // correcting a bone the tray no longer hangs from would twist an arm
        // for no reason
        if (transform.parent != mount)
        {
            Learn();

            // Learn takes whatever parent it finds, and a tray taken off the
            // arm entirely has none -- there is no bone to correct, and asking
            // for one is a null reference once a frame for as long as it is
            // off. The revolver's throw switches this component off rather than
            // relying on the check; this is for everything that does not.
            if (mount == null)
                return;
        }

        // Where the tray's level axis is pointing now that the animator has had
        // its say. Read from the tray, but only READ -- nothing is written back
        Vector3 pointing = transform.rotation * trayUp;

        Quaternion fix = Quaternion.FromToRotation(pointing, Vector3.up);

        fix.ToAngleAxis(out float angle, out Vector3 axis);

        // ToAngleAxis can hand back the long way round, and a wrist asked to
        // turn three hundred degrees rather than sixty is a snapped wrist
        if (angle > 180f)
        {
            angle = 360f - angle;
            axis = -axis;
        }

        // Clamping alone was not enough, and the chef's kiss showed why.
        //
        // That clip puts the palm at the mouth, so levelling the tray asks for
        // far more rotation than a wrist has. Clamped, the ANGLE pins at the
        // limit -- but the AXIS keeps swinging as the arm moves, and a
        // full-strength correction about an axis that is rotating is the tray
        // rocking left and right. Straining at the limit, frame after frame.
        //
        // So past what a wrist can do it lets go instead. The correction fades
        // out over the release band and the tray simply rides the hand, which
        // is what an arm carrying something actually does when the fingers go
        // somewhere the tray cannot follow
        float needed = angle;

        float give = release <= .001f
            ? 1f
            : 1f - Mathf.Clamp01((needed - maxBend) / release);

        angle = Mathf.Min(needed * level, Mathf.Max(0f, maxBend)) * give;

        float ease = smoothing <= .001f
            ? 1f
            : 1f - Mathf.Exp(-Time.deltaTime / smoothing);

        smoothedFix = Quaternion.Slerp(
            smoothedFix, Quaternion.AngleAxis(angle, axis), ease);

        Spring(runTilt * Rush());

        // Tipped about the character's own right, so it reads as leaning back
        // into the run whichever way the kitchen the character is facing
        Quaternion lean = Quaternion.AngleAxis(-tilt, character.right);

        // Written to the BONE. Pre-multiplying rotates it in world space about
        // its own pivot, which is what a wrist does -- and it carries the tray
        // with it because the tray is still, and stays, its child
        mount.rotation = lean * smoothedFix * mount.rotation;
    }

    // A spring rather than an ease, so the lean can overshoot.
    //
    // Easing towards a target only ever arrives at it, which reads as the tray
    // being angled by somebody. A spring has velocity, so breaking into a run
    // tips the tray further than it settles at and it swings back -- which is
    // what a carried thing actually does.
    //
    // Damping ratio does the work. At 1 it is critically damped, no overshoot
    // at all, which is why bounce 0 is a real setting and not a compromise
    private void Spring(float target)
    {
        float remaining = Mathf.Min(Time.deltaTime, .05f);

        if (remaining <= .0001f)
            return;

        // Read out of the same field that tunes the easing, so there is one
        // "how quickly does this settle" number rather than two that disagree
        float omega = 2f * Mathf.PI / Mathf.Max(.02f, smoothing * 4f);

        float zeta = Mathf.Lerp(1f, .3f, bounce);

        // Integrating a spring in one step of whatever length the frame
        // happened to be goes unstable as omega * dt approaches 2, and unstable
        // here means the tray flipping over. A dropped frame on a phone must
        // not be able to do that, so the step is cut up small enough for the
        // stiffness actually in use
        int steps = Mathf.Clamp(Mathf.CeilToInt(remaining * omega / .5f), 1, 8);

        float dt = remaining / steps;

        for (int step = 0; step < steps; step++)
        {
            float offset = tilt - target;

            tiltRate += (-2f * zeta * omega * tiltRate - omega * omega * offset) * dt;

            tilt += tiltRate * dt;
        }
    }

    // How hard the character is going, from 0 to 1.
    //
    // Read off the transform, flattened, because a step down a kerb is not a
    // rush. The speed itself is eased as well as the pose it drives: a single
    // frame where the agent stalls against a counter should not drop the tray
    // out of its lean and back again
    private float Rush()
    {
        Vector3 place = character.position;
        Vector3 step = place - lastPlace;

        step.y = 0f;
        lastPlace = place;

        float now = Time.deltaTime > .0001f ? step.magnitude / Time.deltaTime : 0f;

        speed = Mathf.Lerp(speed, now, 1f - Mathf.Exp(-Time.deltaTime / .12f));

        return runSpeed > .01f ? Mathf.Clamp01(speed / runSpeed) : 0f;
    }
}
