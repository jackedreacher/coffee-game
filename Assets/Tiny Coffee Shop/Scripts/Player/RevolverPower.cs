using System.Collections;
using UnityEngine;

// The cowboy hat's power: reach a station without walking to it.
//
// Attaches itself. There is no field for it on the player and no way to add one
// without editing a scene that is already saved, so TapToServe asks for the
// component and makes one if it is missing -- the same arrangement
// PlayerHatRuntimeFollower already uses for the hat itself.
//
// THE GUN DOES NOT MOVE. That is the whole design, and the first version had it
// exactly backwards.
//
// It used to grow into the hand, spin on the trigger finger, swing round to aim
// and kick on firing -- all driven from here, on this script's own clock, with
// the gun's world rotation rewritten every frame off the forearm. Then Quickdraw
// arrived, which draws, raises and fires the arm. Two choreographies then
// described the same event and neither knew the other existed, so every frame
// one overwrote the other. What that looks like on screen is a revolver that
// never quite belongs to the hand holding it.
//
// There is one now: the clip. The gun is parented to the hand with a fixed local
// transform and never touched again -- the hand moves and the gun goes with it,
// because that is what parenting means. Everything left in here happens at a
// single instant, the bang, and none of it is animation.
[DisallowMultipleComponent]
public class RevolverPower : MonoBehaviour
{

    // How fast the body swings round to face what is being shot.
    //
    // PlayerAnimator multiplies this by Rad2Deg and by deltaTime, so 45 is a
    // little over 2500 degrees a second: half a turn in about seven hundredths
    // of a second. It has to beat the draw, and the draw is a quarter of a
    // second long -- at the old 14 a full about-face took very nearly all of
    // it, which is one of the two reasons shots at something behind the player
    // went off over their shoulder. The other one is in TapToServe.FaceTarget.
    private const float aimTurn = 45f;
    private const string shootState = "Revovler_Shooting";

    private PlayerAnimator player;
    private CharacterSkinPreview wardrobe;

    // The gun that is simply there, and the rig it was hung on.
    //
    // It used to be built for a shot and destroyed a second later. That was
    // right while the hat was a trick and wrong now that it is a costume: the
    // character stands in Idle_w_Revolver and walks in Run_w_Gun, and both of
    // those are poses built around a hand that is already holding something.
    private Transform held;
    private Animator heldOn;

    // Held because the tuner asked, rather than because the hat is on. Lets the
    // placement be dialled in without going and changing hats first.
    private bool forced;

    private bool firing;

    private Coroutine shot;
    private Coroutine toss;

    // A tap that landed while the gun was busy.
    //
    // Queued rather than refused, and queued rather than acted on at once. Both
    // of those were tried and both are wrong: refusing it loses the second
    // target, and acting on it immediately cuts the first shot off before the
    // bang, which loses the FIRST target. Held until the shot in progress has
    // gone off, and then taken up straight away -- so two quick taps are two
    // hits, and the only thing that gets cut is the pose afterwards.
    //
    // Kept whole rather than as three loose fields, because a half-overwritten
    // queue is a shot fired at one thing's aim point through another thing's
    // tap feedback.
    private sealed class Waiting
    {
        public IShootable shootable;
        public Interactable pop;
    }

    private Waiting queued;

    // Where the thrown tray has to go back to. Held in fields as well as on the
    // coroutine's stack, because disabling a MonoBehaviour kills its coroutines
    // without unwinding them -- and the tray is UNPARENTED while it is in the
    // air, so a shot interrupted by the round ending would leave a plate
    // floating in an empty kitchen.
    private Transform flying;
    private Transform flyingHome;
    private Vector3 flyingSeat;
    private Quaternion flyingSit;
    private Vector3 flyingSize;

    // The wrist correction, switched off for the trip. Held here as well as on
    // the coroutine's stack for the same reason everything else is: a component
    // disabled by a coroutine that never gets to finish stays disabled.
    private Behaviour flyingLevel;

    private Transform liveMount;

    // What the barrel is kept pointed at, and the roll it keeps while pointing.
    private bool aiming;
    private Vector3 aimPoint;
    private Vector3 aimTrim;

    // Kept from the last Draw so the scene-view placement can be read back out
    // as the numbers the asset stores. Without them the scale on the transform
    // is just a number -- gunSize is that scale expressed against the model's
    // own length and the animal's height, which is what makes it portable
    // between animals.
    // The animator we sped up, and what it was running at before.
    private Animator spedUp;
    private float plainSpeed = 1f;

    private float drawnLength;
    private float drawnRigHeight;

    // The stance swap, and what it has to put back.
    private Animator stanced;
    private RuntimeAnimatorController plainController;
    private float nextStanceCheck;

    public bool Busy => firing;

    // Whether the hat currently worn is the one that carries this power. Asked
    // on every tap, so it reads the wardrobe rather than caching -- the player
    // can change hats between two taps without the kitchen reloading.
    public bool Armed
    {
        get
        {
            if (wardrobe == null)
                wardrobe = FindFirstObjectByType<CharacterSkinPreview>(
                    FindObjectsInactive.Include);

            return wardrobe != null &&
                   HatPowerBook.For(wardrobe.SelectedHatKey) == HatPower.Revolver;
        }
    }

    // The whole decision, in one call, so TapToServe does not have to know what
    // makes a shot possible. False means "not my business" -- the tap carries on
    // to the ordinary walk-up-and-use path, which is what has to happen when the
    // station is out of range or has nothing a bullet could do for it.
    //
    // Every refusal past the "wrong hat" check says why. A power that declines
    // in silence is indistinguishable from a power that is broken.
    public bool TryShoot(Interactable target)
    {
        if (target == null)
            return false;

        return Begin(target.GetComponentInParent<IShootable>(), target,
                     target.transform, target.Label);
    }

    // Customers come down a different road.
    //
    // They are not Interactables -- the tap finds them by their own search,
    // after the station one has already failed -- so they need their own way in
    // rather than a cast. What happens after this line is identical: the same
    // draw, the same bang, the same IShootable answering for itself.
    public bool TryShoot(Customer customer)
    {
        if (customer == null)
            return false;

        return Begin(customer, null, customer.transform, "musteri");
    }

    private bool Begin(IShootable shootable, Interactable pop, Transform what,
        string label)
    {
        if (what == null || !Armed)
            return false;

        if (shootable == null)
            return false;

        if (!shootable.CanTakeShot)
            return Decline(label, "burada merminin yapacagi bir is yok");

        HatPowerCatalogue book = HatPowerCatalogue.Get();

        if (book == null)
            return Decline(label, "Hat Powers.asset yok -- " +
                                  "Cooked Fast > Sapka > Sapka Yeteneklerini Kur");

        if (book.revolverPrefab == null)
            return Decline(label, "Hat Powers.asset icindeki Revolver Prefab bos");

        // No distance test, on purpose.
        //
        // There used to be one, and past it the tap fell through to the ordinary
        // walk-up-and-use path -- so the answer to "you are too far away to
        // shoot the fryer" was the character walking all the way over to it,
        // which is the opposite of what the gun is for.
        //
        // There is no reload wait either, and no cooldown. What paces the gun
        // is the draw itself: a tap arriving mid-shot waits for the bang and
        // not one frame longer.
        if (firing)
        {
            queued = new Waiting { shootable = shootable, pop = pop };

            return true;
        }

        shot = StartCoroutine(Fire(book, shootable, pop,
                                   shootable.ShotAimPoint, false));

        return true;
    }

    // ---- the stance ---------------------------------------------------
    //
    // The cowboy hat does not only carry a gun, it changes how the character
    // stands, walks and says hello. Done by overriding the CLIPS rather than by
    // adding a second set of states: the state machine, ManageAnimations and
    // every string it plays by name all stay exactly as they were, and taking
    // the hat off is putting the old controller back.
    //
    // The gun is handled on the same tick, because it is the same question --
    // the hat is on, so the character is a cowboy, so there is a revolver in
    // the hand and a walk built around holding one.
    //
    // Checked on a timer rather than every frame. Nothing here changes except
    // when somebody picks a different hat or the body is swapped for another
    // animal, and both are things that happen at human speed.
    private void Update()
    {
        if (Time.unscaledTime < nextStanceCheck)
            return;

        nextStanceCheck = Time.unscaledTime + .25f;

        if (player == null)
            player = GetComponentInChildren<PlayerAnimator>(true);

        Animator rig = player == null ? null : player.CurrentAnimator;
        HatPowerCatalogue book = HatPowerCatalogue.Get();

        Stance(book, rig);
        Carry(book, rig);
    }

    private void Stance(HatPowerCatalogue book, Animator rig)
    {
        bool wanted = rig != null && Armed && Swaps(book) > 0;

        // Already right, including the case where the body was replaced under
        // us and the override went with the animator that is now gone.
        if (wanted && stanced == rig)
            return;

        Restore();

        if (!wanted)
            return;

        // Wrapping the controller rebinds the animator, which restarts it at
        // its default state. That is a visible hitch, and it is why this is
        // only ever done on a hat change rather than continuously.
        AnimatorOverrideController swap =
            new AnimatorOverrideController(rig.runtimeAnimatorController)
            {
                name = "Cowboy Stance",
            };

        for (int i = 0; i < book.stance.Length; i++)
        {
            StanceSwap row = book.stance[i];

            if (row == null || row.clip == null || row.replaces == null)
                continue;

            swap[row.replaces] = row.clip;
        }

        plainController = rig.runtimeAnimatorController;
        rig.runtimeAnimatorController = swap;
        stanced = rig;
    }

    // Rows that would actually change something. A row whose clip is empty is
    // "leave this one alone", and a row whose replaces is empty is a state the
    // controller does not have -- neither is worth wrapping a controller for,
    // and wrapping one costs a rebind.
    private static int Swaps(HatPowerCatalogue book)
    {
        if (book == null || book.stance == null)
            return 0;

        int live = 0;

        for (int i = 0; i < book.stance.Length; i++)
        {
            StanceSwap row = book.stance[i];

            if (row != null && row.clip != null && row.replaces != null)
                live++;
        }

        return live;
    }

    // ---- the gun that is always there ---------------------------------------
    //
    // Rebuilt only when the rig underneath it changes, which is what a body
    // swap is. Nothing else touches it ever again: the gun is a child of the
    // hand and the hand is animated, so being carried is simply what parenting
    // already means.
    private void Carry(HatPowerCatalogue book, Animator rig)
    {
        bool wanted = rig != null && book != null &&
                      book.revolverPrefab != null && (forced || Armed);

        if (!wanted)
        {
            Holster();
            return;
        }

        if (held != null && heldOn == rig)
            return;

        Holster();

        Transform hand = Hand(rig);

        if (hand == null)
            return;

        held = Draw(book, hand, rig);
        heldOn = rig;
    }

    private void Holster()
    {
        // Never out from under a shot that is using it. Finish owns whatever it
        // is firing from, and destroys it there once held has let go.
        if (held != null && held != liveMount)
            Destroy(held.gameObject);

        held = null;
        heldOn = null;
    }

    private void OnDisable()
    {
        Restore();
        Interrupt();
        Holster();

        // Catch it on the way out, wherever it happens to be.
        Caught(flying, flyingHome, flyingSeat, flyingSit, flyingSize,
               flyingLevel);

        flying = null;
        flyingHome = null;
        flyingLevel = null;
        toss = null;
    }

    private void Restore()
    {
        if (stanced != null && plainController != null)
            stanced.runtimeAnimatorController = plainController;

        stanced = null;
        plainController = null;
    }

    private bool Decline(string label, string why)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Kovboy] " + label + " icin ates yok -- " + why, this);
#endif
        return false;
    }

    // Turn, put the gun in the hand, start the clip, and get out of the way.
    // The Interactable is optional: stations have one and use it for the tap
    // feedback, customers do not have one at all.
    private IEnumerator Fire(HatPowerCatalogue book, IShootable shootable,
        Interactable pop, Vector3 aim, bool follow)
    {
        firing = true;

        // A follow-up is the same clip at double speed, and everything timed
        // off it is divided by the same number -- so the bang still lands on
        // the same FRAME of the draw and the shot simply takes half as long
        // from end to end. Half the animation, not half of it played.
        float speed = book.ClipSpeed * (follow ? book.FollowSpeed : 1f);
        float hold = book.HoldAfter / (follow ? book.FollowSpeed : 1f);

        if (player == null)
            player = GetComponentInChildren<PlayerAnimator>(true);

        Animator rig = player == null ? null : player.CurrentAnimator;
        Transform hand = Hand(rig);

        if (hand == null)
            Debug.LogWarning("[Kovboy] el kemigi bulunamadi -- silah gorunmeyecek, " +
                             "atis yine de isler.", this);

        // The BODY turns to face the target, and that is not the same thing as
        // aiming the gun. The clip raises the arm relative to the character;
        // which way the character is standing is nobody's job but this one's.
        if (player != null)
            player.FaceOverride(aim - transform.position, aimTurn);

        // The plate goes up in the air.
        //
        // A character cannot draw a revolver and carry a tray with the same
        // hand, and the tray was winning: it stayed welded to the hand through
        // the whole draw and swung about with it. Thrown, it is out of the shot
        // for exactly as long as the shot lasts, and it comes back to the same
        // place in the same hand -- so nothing that owns the tray ever finds out
        // it left.
        if (player != null && toss == null)
        {
            Transform tray = player.Carried;

            if (tray != null)
                toss = StartCoroutine(Toss(tray,
                                           book.FireAt / Mathf.Max(speed, .01f) + hold,
                                           book.TossLift, book.TossSpins));
        }

        // Already in the hand -- see Carry. Built here only if something
        // upstream failed, because a shot that fires from nothing is worse
        // than a shot that builds its own gun a frame late.
        Transform mount = held;

        if (mount == null && hand != null)
        {
            mount = Draw(book, hand, rig);
            held = mount;
            heldOn = rig;
        }

        // Held in a field as well, so an interruption knows what to clean up.
        liveMount = mount;

        aiming = book.aimAtTarget;
        aimPoint = aim;
        aimTrim = book.aimTurn;

        if (rig != null && speed > .01f)
        {
            plainSpeed = rig.speed;
            rig.speed = plainSpeed * speed;
            spedUp = rig;
        }

        // From here to the bang this script does nothing whatsoever. Quickdraw
        // has the hand, and the gun is parented to it.
        if (player != null)
            player.PlayAction(PlayerAnimator.Action.Shoot, true);

        yield return new WaitForSeconds(book.FireAt / Mathf.Max(speed, .01f));

        Bang(book, shootable, pop, aim, mount);

        // The pose afterwards, unless somebody has already tapped somewhere
        // else. This is the part a second tap is allowed to cut: the shot it
        // interrupts has already gone off, so nothing is lost but standing
        // still.
        for (float t = 0f; t < hold && queued == null; t += Time.deltaTime)
            yield return null;

        Waiting next = queued;

        queued = null;

        if (next != null && next.shootable != null && next.shootable.CanTakeShot)
        {
            // Straight into the next one, with no Finish in between. The gun
            // does not go back to the hip and come out again -- it never left
            // the hand -- and the facing is handed over rather than dropped and
            // retaken, which is what keeps the body turning towards the new
            // target instead of snapping back to its walking heading first.
            //
            // The animator speed IS put back, because the next shot multiplies
            // whatever it finds there: left alone, the third shot in a row
            // would play at six times speed.
            Slow();

            shot = StartCoroutine(Fire(book, next.shootable, next.pop,
                                       next.shootable.ShotAimPoint, true));

            yield break;
        }

        Finish();

        // Cleared HERE rather than in Finish, because Finish also runs when a
        // second tap takes over -- and letting go of the facing between two
        // taps would snap the body back to its walking heading for a frame
        // before the next shot turned it again.
        if (player != null)
            player.ClearFaceOverride();
    }

    // Everything one shot leaves behind, in one place, so it can be run either
    // at the natural end or the moment a second tap takes over.
    private void Finish()
    {
        Slow();
        Park(liveMount);

        // Only a gun this shot built for itself. The carried one belongs to the
        // hat rather than to the shot, and outlives it.
        if (liveMount != null && liveMount != held)
            Destroy(liveMount.gameObject);

        liveMount = null;
        aiming = false;
        shot = null;
        firing = false;

        // Dropped rather than carried over. Finish means this burst is over --
        // a queued tap that outlived it belongs to nothing, and firing it now
        // would be a shot the player asked for a second and a half ago.
        queued = null;
    }

    private void Interrupt()
    {
        if (shot != null)
            StopCoroutine(shot);

        Finish();

        // The facing is NOT cleared here. The next shot sets its own the moment
        // it starts, and clearing it in between would let the body snap back to
        // its walking heading for a frame between two taps.
    }

    // Up, over, and back into the hand.
    //
    // Unparented for the trip and re-parented at the end with its original
    // local position put back exactly, which is what makes this invisible to
    // everything else: the tray is a child of the hand at the start and a child
    // of the hand at the finish, and only the frames in between differ.
    //
    // The landing point is read every frame rather than remembered, because the
    // hand it has to land in is being animated -- aiming at where the hand USED
    // to be would drop the plate on the floor beside a character who has since
    // turned round.
    //
    // THREE things went wrong in the first version and all three had the same
    // symptom: nothing appeared to happen, the plate simply swung about with
    // the arm as it always had.
    //
    // * The arc was drawn from the hand to the hand. A curve whose both ends
    //   are on the arm, blended every frame with where the arm has got to, is
    //   a curve that reads as being carried the whole way. It hopped; it never
    //   left. It goes out to a point fixed in the WORLD now, and only the way
    //   down is allowed to know where the hand is.
    //
    // * It was thrown along world up, which on this camera is worth a few
    //   pixels. Same trap the dead emoji fell into: the view looks steeply
    //   down, so height barely moves anything on screen and the camera's own
    //   up axis is what "up" means to whoever is watching.
    //
    // * It spun about its own vertical axis, which for a round plate is the
    //   one rotation that cannot be seen at all. It turns end over end now,
    //   about an axis across the view.
    private IEnumerator Toss(Transform tray, float seconds, float lift,
        float spins)
    {
        Transform home = tray.parent;

        Vector3 seat = tray.localPosition;
        Quaternion sit = tray.localRotation;

        // Scale has to be remembered too, and it is the one that bites.
        //
        // SetParent(null, true) keeps the tray the size it LOOKS, which it does
        // by rewriting localScale to the old world scale. Handing that number
        // back to a parent that has a scale of its own multiplies the two, and
        // the plate comes back bigger every single time it is thrown.
        Vector3 size = tray.localScale;

        // The wrist correction is switched off for the trip rather than left
        // to discover the tray has gone.
        //
        // PlateauLevel rotates the BONE the tray hangs from, and re-learns the
        // moment it finds the tray parented somewhere else. Mid-throw that
        // means learning "which way is up on this tray" from a tray in the air
        // above a hand halfway through a draw -- and then keeping that answer
        // for the rest of the round. Switched off and on again it never sees
        // the tray leave, so there is nothing for it to mis-learn.
        Behaviour leveller = tray.GetComponent<PlateauLevel>();

        if (leveller != null && leveller.enabled)
            leveller.enabled = false;
        else
            leveller = null;

        // Up and across the SCREEN. On a camera this steep the world's up axis
        // and the camera's are nearly at right angles, and only one of them is
        // the direction a thrown plate appears to go.
        Camera eye = Camera.main;

        Vector3 up = eye != null ? eye.transform.up : Vector3.up;
        Vector3 across = eye != null ? eye.transform.right : Vector3.right;

        tray.SetParent(null, true);

        flying = tray;
        flyingHome = home;
        flyingSeat = seat;
        flyingSit = sit;
        flyingSize = size;
        flyingLevel = leveller;

        Vector3 from = tray.position;
        Quaternion spin = tray.rotation;

        // Chosen once and then left alone. This one line is the whole
        // difference between a throw and a hop: nothing the arm does afterwards
        // can move the top of the arc.
        Vector3 apex = from + up * lift;

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            // The tray can be handed over, emptied or destroyed mid-flight --
            // this is a game about giving plates away.
            if (tray == null)
            {
                if (leveller != null)
                    leveller.enabled = true;

                flying = null;
                flyingHome = null;
                flyingLevel = null;
                toss = null;

                yield break;
            }

            if (home == null)
                break;

            float a = Mathf.Clamp01(t / seconds);

            if (a < .5f)
            {
                // Slowing into the top, the way a thrown thing does.
                float rise = a * 2f;

                tray.position =
                    Vector3.Lerp(from, apex, 1f - (1f - rise) * (1f - rise));
            }
            else
            {
                // And falling away from it, into wherever the hand has got to
                // by now -- which is read fresh every frame, because the arm is
                // being animated and will not be where it was.
                float fall = (a - .5f) * 2f;

                tray.position =
                    Vector3.Lerp(apex, home.TransformPoint(seat), fall * fall);
            }

            // Pre-multiplied, so the turn happens in WORLD space about an axis
            // lying across the view. Post-multiplying would turn it about its
            // own axes instead, and a plate turning about its own axis is a
            // plate that looks perfectly still.
            tray.rotation = Quaternion.AngleAxis(360f * spins * a, across) * spin;

            yield return null;
        }

        Caught(tray, home, seat, sit, size, leveller);

        flying = null;
        flyingHome = null;
        flyingLevel = null;
        toss = null;
    }

    // Put back exactly, not approximately. Anything that reads the tray's local
    // position later has to find what it wrote there.
    private void Caught(Transform tray, Transform home, Vector3 seat,
        Quaternion sit, Vector3 size, Behaviour leveller)
    {
        if (tray == null || home == null)
        {
            // The wrist still goes back on even when there is no tray left to
            // put anywhere. A component switched off by a throw that never
            // landed is a tray that never levels again.
            if (leveller != null)
                leveller.enabled = true;

            return;
        }

        tray.SetParent(home, false);
        tray.localPosition = seat;
        tray.localRotation = sit;
        tray.localScale = size;

        // Switched back on AFTER the re-parent, so the first frame it runs
        // finds the tray exactly where it was before the throw and has nothing
        // to re-learn.
        if (leveller != null)
            leveller.enabled = true;
    }

    // ---- aiming -------------------------------------------------------------

    // The revolver points at what is being shot. OFF by default -- see
    // HatPowerCatalogue.aimAtTarget for why it is not worth the trade.
    //
    // Written every frame the gun is out, because the hand it hangs off is
    // being animated: a rotation set once would drift away with the arm.
    //
    // The gun turns about its GRIP, which is the holder origin, so the fist
    // keeps hold of it either way. What it does not keep is gunTurn: an aimed
    // rotation is a whole rotation, and it lands wherever aiming needs rather
    // than where the grip was dialled to sit.
    //
    // aimTurn is applied after the look, not before, so it reads as a
    // correction to the aimed pose rather than as part of the aim itself.
    private void LateUpdate()
    {
        if (!aiming || liveMount == null)
            return;

        Vector3 line = aimPoint - liveMount.position;

        if (line.sqrMagnitude < .0001f)
            return;

        liveMount.rotation =
            Quaternion.LookRotation(line.normalized, Vector3.up) *
            Quaternion.Euler(aimTrim);
    }

    // ---- the bang -----------------------------------------------------------

    // One shot: noise, flash, tracer, and the station being told.
    //
    // Called once per shot rather than once per tap, and the station answers
    // separately every time. The second barrel is not decoration -- if there is
    // still something up there worth shooting it goes too, and if there is not
    // the station declines, which is a miss rather than a bug.
    private void Bang(HatPowerCatalogue book, IShootable shootable,
        Interactable pop, Vector3 aim, Transform mount)
    {
        Vector3 muzzle = Muzzle(book, aim);

        if (book.gunshot != null)
            AudioSource.PlayClipAtPoint(book.gunshot, muzzle);

        if (book.muzzleFlash != null)
        {
            GameObject flash = Instantiate(book.muzzleFlash, muzzle,
                Quaternion.LookRotation(aim - muzzle));

            // Carried by the PLAYER, not by the gun, because that is now what
            // the flash is standing at. Parented after being placed so it keeps
            // the pose it was given; left loose in the world it would hang in
            // the air if the character turned during the hold.
            flash.transform.SetParent(transform, true);

            Spend(flash, book.HoldAfter);
        }

        PlayShot(mount);
        StartCoroutine(Tracer(book, muzzle, aim));

        string what = shootable.TakeShot();

        if (string.IsNullOrEmpty(what))
            return;

        // Reusing the tap's own feedback. The station already knows how to say
        // "that registered", and a bullet is no less a tap for having been
        // fired from across the room. A customer has no such squeeze -- being
        // shot is its own announcement.
        if (pop != null)
            pop.Pop();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Kovboy] " + what, this);
#endif
    }

    // Putting the animator back to the speed it was found at. Called on the way
    // out of the shot and again if the component goes away mid-shot, because a
    // character left running at 1.7x is a bug nobody would connect to a gun.
    private void Slow()
    {
        if (spedUp != null)
            spedUp.speed = plainSpeed;

        spedUp = null;
        plainSpeed = 1f;
    }

    // ---- putting it in the hand ---------------------------------------------

    // One fixed local transform, set once. Nothing reads or writes it again.
    //
    // The mount exists so the pack's prefab keeps its own transform to itself:
    // Revovler.prefab carries an Animator that writes the model every
    // LateUpdate, and anything set on that same object would be overwritten a
    // frame later. The mount is ours, the model inside it is theirs.
    private Transform Draw(HatPowerCatalogue book, Transform hand, Animator rig)
    {
        GameObject holder = new GameObject("COWBOY REVOLVER");

        holder.transform.SetParent(hand, false);

        GameObject gun = Instantiate(book.revolverPrefab, holder.transform);

        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.identity;
        gun.transform.localScale = Vector3.one;

        Ready(gun);
        PlayerHatFitter.UseUrpMaterials(gun);
        SetLayer(holder.transform, gameObject.layer);

        if (!Measure(gun.transform, holder.transform, out Bounds raw))
        {
            Debug.LogWarning("[Kovboy] " + book.revolverPrefab.name +
                             " olculemedi -- prefab icinde renderer yok mu?", this);

            return holder.transform;
        }

        // Barrel down the mount's own forward, so every gun in the pack starts
        // from the same place and gunTurn means the same thing for all of them.
        // The longest axis of a gun IS the barrel; which END of it is the muzzle
        // a bounding box cannot say, and that is what gunTurn is for.
        gun.transform.localRotation = Align(raw.size);

        if (!Measure(gun.transform, holder.transform, out Bounds box))
            return holder.transform;

        float length = Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z));
        float rigHeight = RigHeight(rig);

        drawnLength = length;
        drawnRigHeight = rigHeight;

        if (rigHeight > .0001f && length > .0001f)
            holder.transform.localScale =
                Vector3.one * (rigHeight * book.GunSize / length);

        // The GRIP onto the palm, not the middle of the mesh. Centring puts the
        // revolver's balance point in the fist, which hangs half a barrel out of
        // the back of the hand.
        gun.transform.localPosition = -new Vector3(
            box.center.x,
            Mathf.Lerp(box.min.y, box.max.y, .33f),
            Mathf.Lerp(box.min.z, box.max.z, .20f));

        // And the one adjustment there is, in the HAND's own frame, applied to
        // the mount so the measurements above stay untouched.
        holder.transform.localRotation = Quaternion.Euler(book.gunTurn);
        holder.transform.localPosition =
            new Vector3(book.gunSide, book.gunLift, book.gunReach);

        if (Visible(holder.transform) <= 0)
            Debug.LogWarning("[Kovboy] silah kuruldu ama gorunur renderer yok.",
                this);

        return holder.transform;
    }

    private Transform Hand(Animator rig)
    {
        if (rig == null)
            return null;

        Transform hand = rig.GetBoneTransform(HumanBodyBones.RightHand);

        return hand != null ? hand : rig.GetBoneTransform(HumanBodyBones.LeftHand);
    }

    // Switched on, and made immune to the culling test.
    //
    // A SkinnedMeshRenderer is culled against a box stored relative to its ROOT
    // BONE, baked when the prefab was authored. Bolt it to a hand bone that is
    // animated and scaled and that box describes a place the gun is not, so
    // Unity decides the mesh is off screen and never draws it -- correctly
    // placed, correctly sized, and invisible. updateWhenOffscreen recomputes the
    // bounds from the vertices instead.
    private void Ready(GameObject gun)
    {
        Animator gunRig = gun.GetComponentInChildren<Animator>(true);

        // Parked. Its controller's default state starts the moment the object
        // exists, and whatever that was -- a reload, an idle sway -- it is not
        // what a gun being held does. It comes back for the shot.
        if (gunRig != null)
        {
            gunRig.applyRootMotion = false;
            gunRig.enabled = false;
        }

        Transform[] all = gun.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
            if (!all[i].gameObject.activeSelf)
                all[i].gameObject.SetActive(true);

        Renderer[] renderers = gun.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].enabled = true;

            if (renderers[i] is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
        }
    }

    // The pack's own hammer-and-cylinder clip, on the model inside the mount.
    // Nothing waits for it: it is decoration on top of the character's own
    // recoil, and a shot that stalled because an imported clip was a different
    // length would be a worse bug than a shot with no clip at all.
    private void PlayShot(Transform mount)
    {
        if (mount == null)
            return;

        Animator gunRig = mount.GetComponentInChildren<Animator>(true);

        if (gunRig == null || gunRig.runtimeAnimatorController == null)
            return;

        gunRig.enabled = true;
        gunRig.speed = 1f;

        int hash = Animator.StringToHash(shootState);

        if (gunRig.HasState(0, hash))
            gunRig.Play(hash, 0, 0f);
    }

    // And back to sleep afterwards.
    //
    // It is switched on for the hammer-and-cylinder clip and nothing else. That
    // did not matter while the gun was destroyed a second later; on one that
    // now stays in the hand, an animator left running drops into whatever its
    // controller calls a default state -- a reload, an idle sway -- and does it
    // forever, on a gun that is only being held.
    private void Park(Transform mount)
    {
        if (mount == null)
            return;

        Animator gunRig = mount.GetComponentInChildren<Animator>(true);

        if (gunRig != null)
            gunRig.enabled = false;
    }

    // ---- live tuning --------------------------------------------------------
    //
    // The gun is in the hand for as long as the hat is on, so there is nothing
    // left to preview -- what the tuner adjusts is the real one. All this adds
    // is a way to get it out WITHOUT the hat, so the placement can be dialled
    // in without going and changing hats first.
    public bool PreviewOn => held != null;

    // Out because the tuner asked, rather than because the hat is on. The
    // hat's own gun is not the tuner's to take away.
    public bool Forced => forced;

    public string ShowPreview()
    {
        forced = true;

        if (player == null)
            player = GetComponentInChildren<PlayerAnimator>(true);

        Animator rig = player == null ? null : player.CurrentAnimator;

        if (rig == null)
            return "Player Animator bulunamadi";

        if (Hand(rig) == null)
            return "El kemigi yok";

        HatPowerCatalogue book = HatPowerCatalogue.Get();

        if (book == null)
            return "Hat Powers.asset yok";

        if (book.revolverPrefab == null)
            return "Asset icindeki Revolver Prefab bos";

        // Now rather than on the next quarter-second tick: whoever pressed the
        // button is expecting a gun to look at.
        Carry(book, rig);

        return held == null ? "Silah kurulamadi" : null;
    }

    public void HidePreview()
    {
        forced = false;

        if (player == null)
            player = GetComponentInChildren<PlayerAnimator>(true);

        // Handed straight back to the hat, which may well want it out anyway.
        Carry(HatPowerCatalogue.Get(),
              player == null ? null : player.CurrentAnimator);
    }

    // The held gun, so the editor can hand it to Unity's own move and rotate
    // handles. Placing it by dragging it is the same act as placing anything
    // else in a scene, which is a great deal more direct than three sliders
    // describing a rotation nobody can picture.
    public GameObject PreviewObject => held == null ? null : held.gameObject;

    // Asset -> the held gun. Used while a slider is being dragged, so the thing
    // on screen keeps up without being torn down and rebuilt -- rebuilding it
    // would drop the scene-view selection on every frame of the drag.
    public void Apply(HatPowerCatalogue book)
    {
        if (held == null || book == null)
            return;

        held.localRotation = Quaternion.Euler(book.gunTurn);
        held.localPosition =
            new Vector3(book.gunSide, book.gunLift, book.gunReach);

        if (drawnLength > .0001f && drawnRigHeight > .0001f)
            held.localScale =
                Vector3.one * (drawnRigHeight * book.GunSize / drawnLength);
    }

    // The held gun -> asset. The other direction, and the point of the whole
    // arrangement: drag it into the fist by hand, then keep where it landed.
    //
    // Scale is stored back as a SHARE rather than as the transform's own
    // number. The transform's scale only means anything next to this model's
    // length and this animal's height; written as a share it survives both.
    public bool Capture(HatPowerCatalogue book)
    {
        if (held == null || book == null)
            return false;

        book.gunTurn = held.localEulerAngles;
        book.gunSide = held.localPosition.x;
        book.gunLift = held.localPosition.y;
        book.gunReach = held.localPosition.z;

        if (drawnLength > .0001f && drawnRigHeight > .0001f)
            book.gunSize = held.localScale.x * drawnLength / drawnRigHeight;

        return true;
    }

    // ---- the shot's own effects ---------------------------------------------

    // Front of the barrel, in world space. The mount's forward IS the barrel,
    // because Align put it there.
    // Where the shot comes from.
    //
    // The PLAYER, not the revolver. It used to be the gun -- a barrel length in
    // front of the grip -- and that tied the bullet to a transform whose whole
    // job is being nudged about by hand until the gun looks held. Every
    // correction to the grip moved the shot with it, so the two could never be
    // dialled in at the same time.
    //
    // Taken off the character, the two are unrelated. The gun can be put
    // anywhere at all and the bullet still leaves chest high, in front, on its
    // own line to whatever was tapped.
    private Vector3 Muzzle(HatPowerCatalogue book, Vector3 aim)
    {
        Vector3 from = transform.position + Vector3.up * book.MuzzleHeight;

        Vector3 line = aim - from;

        if (line.sqrMagnitude < .0001f)
            return from;

        // Never more than halfway there, or a shot at something close by would
        // start past the thing it is aimed at.
        float ahead = Mathf.Min(book.MuzzleAhead, line.magnitude * .5f);

        return from + line.normalized * ahead;
    }

    private IEnumerator Tracer(HatPowerCatalogue book, Vector3 from, Vector3 to)
    {
        GameObject holder = new GameObject("SHOT TRACER");
        LineRenderer line = holder.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null)
        {
            Material material = new Material(shader);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(1f, .92f, .55f));

            line.material = material;
        }

        line.widthMultiplier = book.TracerWidth;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startColor = new Color(1f, .95f, .6f);
        line.endColor = new Color(1f, .7f, .25f, 0f);

        yield return new WaitForSeconds(book.TracerSeconds);

        Destroy(holder);
    }

    // ---- measuring ----------------------------------------------------------

    // A rotation that brings the longest of the three local axes round to point
    // along +Z, which is the mount's forward.
    private static Quaternion Align(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z)
            return Quaternion.FromToRotation(Vector3.right, Vector3.forward);

        if (size.y >= size.x && size.y >= size.z)
            return Quaternion.FromToRotation(Vector3.up, Vector3.forward);

        return Quaternion.identity;
    }

    // Feet to head bone, walked along the bone chain. Pose independent: humanoid
    // animation rotates bones and never stretches them, so every localPosition
    // below the hips is a fixed length. Same measurement PlayerHatFitter uses,
    // and for the same reason -- it reads the same on every animal whatever the
    // artwork is scaled to.
    public static float RigHeight(Animator rig)
    {
        if (rig == null)
            return 0f;

        Transform head = rig.GetBoneTransform(HumanBodyBones.Head);
        Transform root = rig.transform;

        if (head == null)
            return 0f;

        float total = 0f;
        Transform node = head;

        while (node != null && node != root)
        {
            total += node.localPosition.magnitude;
            node = node.parent;
        }

        return total;
    }

    // Everything drawn under `item`, expressed in `space`.
    //
    // Off the mesh, never off Renderer.bounds. That one is a box aligned to the
    // WORLD axes, so it measures the object AND the angle it is being held at --
    // which is exactly how the same hat ended up two different sizes in the menu
    // and in the kitchen.
    public static bool Measure(Transform item, Transform space, out Bounds box)
    {
        box = default;

        if (item == null || space == null)
            return false;

        Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Bounds local = renderer.localBounds;
            Transform frame = renderer.transform;

            if (renderer is SkinnedMeshRenderer skinned && skinned.rootBone != null)
                frame = skinned.rootBone;

            Vector3 min = local.min;
            Vector3 max = local.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = space.InverseTransformPoint(frame.TransformPoint(
                    new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z)));

                if (!found)
                {
                    box = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                    box.Encapsulate(point);
            }
        }

        return found;
    }

    // Renderers that would actually draw: on, and on a live object.
    public static int Visible(Transform root)
    {
        if (root == null)
            return 0;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        int live = 0;

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].enabled &&
                renderers[i].gameObject.activeInHierarchy)
                live++;

        return live;
    }

    private void Spend(GameObject thing, float seconds)
    {
        if (thing != null)
            Destroy(thing, seconds);
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        foreach (Transform child in root)
            SetLayer(child, layer);
    }
}
