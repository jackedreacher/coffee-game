using System.Collections;
using UnityEngine;

public class CustomerAnimator : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject plateau;

    [Header(" Settings ")]
    [Tooltip("Govde saniyede kac derece doner. 0 = varsayilan 540")]
    [SerializeField] private float turnSpeed = 540f;

    // How far the body is raised while it is lying down.
    //
    // The death clip was authored for whatever height its own character stood
    // at, and these capsules are scaled to a height this game picked -- so the
    // pose ends up part way through the floor. Lifting the VISUAL rather than
    // the customer means the floor is the only thing that changes: the queue
    // slot, the aim point and everything else still read the same position.
    [Tooltip("Yatarken govde ne kadar yukari alinir. Yere gomulyorsa arttir.")]
    [SerializeField] private float deathLift = .25f;

    // Sentinel, for the reason every number in this project has one: this field
    // lands on prefabs that were saved before it existed, and a serialised zero
    // is indistinguishable from a field nobody filled in
    private float TurnSpeed => turnSpeed > .01f ? turnSpeed : 540f;

    // Walking corners stay quick enough to follow a NavMesh route. Once the
    // feet stop, a quarter turn must last long enough for the authored waiter
    // TurnLeft/TurnRight pose to be visible instead of looking like a snap.
    private const float arrivalTurnSpeed = 180f;

    // The stop.
    //
    // A character walking at full pace and then simply standing still has no
    // moment of arriving -- the legs change clip and that is the whole event.
    // Squashing on the frame they stop and springing back past their own height
    // is the brakes going on: it costs a third of a second, it happens exactly
    // where the player is already looking, and it turns "the walk ended" into
    // "they got here"
    [Header(" Varis ")]
    [Tooltip("Durunca yapilan fren zıplamasi kac saniye surer. 0 = varsayilan 0.34")]
    [SerializeField] private float landTime = .34f;

    [Tooltip("En cok ne kadar ezilir, oran. 0 = varsayilan 0.2")]
    [SerializeField] private float landSquash = .2f;

    [Tooltip("Geri sicrarken kac birim yukselir. 0 = varsayilan 0.14")]
    [SerializeField] private float landHop = .14f;

    private float LandTime => landTime > .01f ? landTime : .34f;
    private float LandSquash => landSquash > .001f ? landSquash : .2f;
    private float LandHop => landHop > .001f ? landHop : .14f;

    private bool isSitting;
    private Vector3 lastVelocity;

    // The single positive reaction to a successful hand-over. Kept as a
    // controller state name, not a clip name: CustomerAnimator talks to its
    // controller by this interface everywhere else too.
    private const string foodReaction = "React_ChefsKiss";
    private const string noOrderReaction = "React_NoGesture";
    private const string leaveTurn = "Leave_Turn180";
    private const string deathDrop = "Death";
    private const string deathIdle = "Death_Idle";
    private const string runState = "Run";

    // Set before the departure starts, read when it picks its walk. Kept as a
    // flag rather than as a second CrossFadeToRun because the departure decides
    // its own state in one place and there should still be only one.
    private bool running;

    // Nothing writes layer zero after this. Update normally puts Idle or Walk
    // back every single frame, which is what was standing the body up again on
    // the frame after it dropped -- and it only ever LOOKED intermittent
    // because a reaction that happened to be running suppressed Update too.
    private bool dead;

    public void Run(bool on)
    {
        running = on;
    }

    // Dropped, and then left down.
    //
    // Two clips because Death ends standing at the last frame it was authored
    // for and would pop back to the queue idle the moment it finished. The
    // second one is a pose, and a pose that loops is a body on the floor.
    public void PlayDeath()
    {
        if (animator == null || dead)
            return;

        running = false;
        dead = true;

        // Cleared by hand because StopAllCoroutines does not unwind anything:
        // a reaction cut off mid-flight leaves its flag set for good, and the
        // arrival turn leaves its own. Neither has anything left to finish.
        reacting = false;
        arrivalTurning = false;

        // The landing squash writes scale every frame it is alive, which on a
        // body lying on the floor is a corpse breathing.
        landAge = -1f;

        // Added rather than assigned, so this cannot pile up on a body that is
        // somehow told to die twice -- the dead flag is set above and PlayDeath
        // is refused before it gets here.
        animator.transform.localPosition += Vector3.up * deathLift;

        StopAllCoroutines();

        if (Has(deathDrop))
            animator.CrossFadeInFixedTime(deathDrop, .05f, 0, 0f);
        else if (Has(deathIdle))
            animator.CrossFadeInFixedTime(deathIdle, .05f, 0, 0f);
        else
            return;

        if (Has(deathDrop) && Has(deathIdle))
            StartCoroutine(StayDown());
    }

    private System.Collections.IEnumerator StayDown()
    {
        // Wait for the drop to be the thing playing before asking how long it
        // is: on this frame the animator still describes the state being left.
        yield return null;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        yield return new WaitForSeconds(Mathf.Max(.1f, info.length * .92f));

        animator.CrossFadeInFixedTime(deathIdle, .12f, 0, 0f);
    }

    private Coroutine reactionRoutine;
    private Coroutine arrivalTurnRoutine;
    private bool reacting;
    private bool arrivalTurning;
    private bool plateauWasActive;
    private bool plateauHiddenForDeparture;
    private bool plateauHiddenForArrivalTurn;
    private bool plateauWasActiveBeforeArrivalTurn;
    private bool departing;

    // True at the instant Turn180 starts blending into the walk. Customer uses
    // it to release NavMesh during the blend rather than after it, so the feet
    // and ground movement accelerate together.
    public bool DepartureCanMove { get; private set; }

    private const float departureWalkBlend = .28f;

    public bool IsReacting => reacting;
    public bool IsArrivalTurning => arrivalTurning;

    public void HidePlateauForDeparture()
    {
        if (plateau == null)
            return;

        // Reaction already captured the real pre-emote state. Do not replace
        // it with false when Leave is requested midway through Chef's Kiss.
        if (!reacting)
            plateauWasActive = plateau.activeSelf;

        plateauHiddenForDeparture = true;
        plateau.SetActive(false);
    }

    // Negative means "not running", the same as every other age in this project
    private float landAge = -1f;

    // Where the body was authored, read before anything has moved it. The
    // squash multiplies THESE rather than whatever the last frame left behind,
    // so a customer who is pooled and lands twice does not end up flatter
    private Vector3 bodyScale = Vector3.one;
    private Vector3 bodyHome;

    private void Awake()
    {
        if (animator == null)
            return;

        bodyScale = animator.transform.localScale;
        bodyHome = animator.transform.localPosition;
    }

    // They have arrived. Called by whoever knows that -- the customer, on the
    // frame they stop walking -- rather than guessed at from the velocity here,
    // which would also fire every time somebody was nudged in the queue
    public void Land()
    {
        landAge = 0f;
    }

    // Where the body is turning to, kept apart from how fast it is going. They
    // used to be the same vector and they answer different questions
    private Vector3 heading;

    private void Update()
    {
        // Play/Idle is normally written every frame. During a reaction that
        // would replace Chef's Kiss on its very first frame, so the reaction
        // exclusively owns layer zero until its one-shot time has elapsed.
        if (!dead && !isSitting && !reacting && !arrivalTurning)
            HandleAnimations();

        // Outside the sitting test. Someone who sits down mid-bounce still has
        // to be put back to their own size, or they spend the rest of the scene
        // in a chair slightly squashed
        if (landAge >= 0f)
            Landing();
    }

    // Called only after Plateau accepted a real served item. Invalid taps never
    // reach this method, so a reaction always means "I received food".
    public void ReactToFood()
    {
        if (animator == null)
            return;

        if (!Has(foodReaction))
            return;

        CancelArrivalTurn();

        // Several items can be handed over before the first Chef's Kiss has
        // finished. Restarting the pose is harmless, but replaying "muah" for
        // every item turns one reaction into a machine-gun sound. Only the
        // first item in this uninterrupted reaction chain owns the sound.
        bool firstInReaction = reactionRoutine == null;

        if (reactionRoutine != null)
            StopCoroutine(reactionRoutine);

        if (firstInReaction)
            SoundManager.Play(SoundManager.Sound.Kiss);

        reactionRoutine = StartCoroutine(Reaction(foodReaction));
    }

    // Starts the complete departure performance. Navigation waits on
    // IsReacting, so neither the agent nor HandleAnimations can rotate or move
    // the body while the authored 180-degree clip owns it.
    public bool BeginDeparture(Vector3 facing, bool disappointed)
    {
        if (animator == null || reacting || departing)
            return false;

        CancelArrivalTurn();

        // Permanent for this customer. After the authored 180 turn, the first
        // NavMesh frame can still report zero velocity while its first path
        // heading differs from the direct exit vector. The ordinary idle turn
        // selector used to read that as a request for TurnLeft/TurnRight and
        // play a second, baffling 90-degree gesture while they were leaving.
        departing = true;
        DepartureCanMove = false;

        facing.y = 0f;

        if (facing.sqrMagnitude < .0001f)
            facing = animator.transform.forward;

        reactionRoutine = StartCoroutine(Departure(facing.normalized, disappointed));
        return true;
    }

    private IEnumerator Reaction(string state)
    {
        // Chef's Kiss was authored empty handed. Let the customer use
        // both hands naturally, then put the exact same loaded tray back. Only
        // visibility changes; parent, offsets and food stack stay untouched.
        //
        // A second item can arrive while the first reaction is still running.
        // Preserve the original state only once, but hide the plateau on EVERY
        // restart: CollectFood or another system may have made it active again
        // between the two coroutine starts.
        if (plateau != null)
        {
            if (!reacting)
                plateauWasActive = plateau.activeSelf;

            plateau.SetActive(false);
        }

        reacting = true;

        // Imported waiter one-shots are marked loopTime. Leave a tiny safety
        // margin so the Animator never renders their wrapped first frame.
        yield return PlayOnce(state, .98f);

        RestoreReactionPlateau();

        reacting = false;
        reactionRoutine = null;
    }

    private IEnumerator Departure(Vector3 facing, bool disappointed)
    {
        // Departure stays empty-handed all the way to the exit. The exact food
        // stack and hand mounting remain untouched on the inactive object.
        if (plateau != null)
        {
            if (!plateauHiddenForDeparture)
                plateauWasActive = plateau.activeSelf;

            plateauHiddenForDeparture = true;
            plateau.SetActive(false);
        }

        reacting = true;
        lastVelocity = Vector3.zero;

        // A customer who timed out objects first. A satisfied customer skips
        // this state and goes directly to the same authored about-face.
        if (disappointed && Has(noOrderReaction))
            yield return PlayOnce(noOrderReaction, .96f);

        // Not when patience reaches zero: the NoGesture performance may still
        // take a full clip before the customer actually leaves. This cue owns
        // the exact frame the authored 180-degree turn begins.
        if (disappointed)
            SoundManager.Play(SoundManager.Sound.CustomerDisappointed);

        if (Has(leaveTurn))
            yield return PlayTurnOnce(facing);
        else
            Face(facing);

        heading = facing;
        CrossFadeToWalk();

        // Navigation may begin as the walk pose takes over. The root has
        // already completed exactly one monotonic turn, so there is no second
        // orientation to compensate and no frame that can face backwards.
        DepartureCanMove = true;

        float age = 0f;

        while (age < departureWalkBlend)
        {
            age += Time.deltaTime;
            yield return null;
        }

        reacting = false;
        DepartureCanMove = false;
        reactionRoutine = null;
    }

    private IEnumerator PlayOnce(string state, float portion)
    {
        animator.CrossFadeInFixedTime(state, .08f, 0, 0f);

        // Let Animator enter the state before asking its length. On this frame
        // GetCurrentAnimatorStateInfo still describes the state being left.
        yield return null;

        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        float speed = Mathf.Abs(info.speed * info.speedMultiplier);
        float duration = info.length / Mathf.Max(.01f, speed);

        // Exactly one cycle even though the imported waiter clips loop.
        yield return new WaitForSeconds(Mathf.Max(.1f, duration * Mathf.Clamp(portion, .1f, .99f)));
    }

    private IEnumerator PlayTurnOnce(Vector3 facing)
    {
        animator.CrossFadeInFixedTime(leaveTurn, .08f, 0, 0f);
        yield return null;

        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        float speed = Mathf.Abs(info.speed * info.speedMultiplier);
        float duration = info.length / Mathf.Max(.01f, speed);
        float span = Mathf.Max(.1f, duration * .96f);

        Quaternion from = animator.transform.rotation;
        Quaternion to = Quaternion.LookRotation(facing, Vector3.up);
        float age = 0f;

        // Rotation has been extracted from the FBX pose by the importer. Apply
        // it once to the Animator root, in the same time window as the feet's
        // authored turn. This value only moves from FROM to TO; mathematically
        // it cannot look back towards FROM for an intermediate frame.
        while (age < span)
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / span);
            t = t * t * (3f - 2f * t);

            animator.transform.rotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }

        animator.transform.rotation = to;
    }

    private IEnumerator ArrivalTurn(string state, Vector3 facing)
    {
        arrivalTurning = true;
        lastVelocity = Vector3.zero;
        HidePlateauForArrivalTurn();

        animator.CrossFadeInFixedTime(state, .04f, 0, 0f);
        yield return null;

        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        float speed = Mathf.Abs(info.speed * info.speedMultiplier);
        Quaternion from = animator.transform.rotation;
        Quaternion to = Quaternion.LookRotation(facing, Vector3.up);
        float angle = Quaternion.Angle(from, to);

        // The source is a wide 90-degree step. Only sample its compact opening
        // gesture; the root still reaches the exact counter direction. This
        // gives a quick in-place pivot instead of feet and arms occupying half
        // the customer slot while the long authored turn completes.
        float animatedAngle = Mathf.Min(angle, maxArrivalClipAngle);
        float clipPortion = Mathf.Clamp(animatedAngle / 90f, .08f, .27f);
        float naturalSpan = info.length / Mathf.Max(.01f, speed) * clipPortion;
        float span = Mathf.Clamp(naturalSpan, .1f, maxArrivalTurnTime);
        float age = 0f;
        bool idleBlendStarted = false;
        bool trayRevealed = false;

        // Animation and root own the same selected portion of the turn.
        while (age < span)
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / span);
            t = t * t * (3f - 2f * t);
            animator.transform.rotation = Quaternion.Slerp(from, to, t);

            // Prepare the holding hands while the tray is still hidden, then
            // reveal it in the last beat of the pivot. It no longer appears
            // after the customer has already been standing still.
            if (!idleBlendStarted && t >= .72f && Has("IdleWithPlateau"))
            {
                idleBlendStarted = true;
                animator.CrossFadeInFixedTime("IdleWithPlateau", .06f, 0, 0f);
            }

            if (!trayRevealed && t >= .85f)
            {
                trayRevealed = true;
                RevealArrivalTurnPlateau();
            }

            yield return null;
        }

        animator.transform.rotation = to;
        heading = animator.transform.forward;

        // Blend the hands fully into the holding idle while the tray is still
        // hidden. Only then reveal it, so it never materialises inside a hand
        // that is visibly halfway through the turn.
        if (!idleBlendStarted && Has("IdleWithPlateau"))
            animator.CrossFadeInFixedTime("IdleWithPlateau", .08f, 0, 0f);

        age = 0f;

        while (age < .08f)
        {
            age += Time.deltaTime;
            yield return null;
        }

        arrivalTurning = false;
        arrivalTurnRoutine = null;
        RestoreArrivalTurnPlateau(true);
    }

    private void CrossFadeToWalk()
    {
        // Running beats carrying. Somebody leaving because a gun went off is
        // not still minding their plate.
        if (running && Has(runState))
        {
            animator.CrossFadeInFixedTime(runState, .06f, 0, 0f);
            return;
        }

        string state = plateau != null && plateau.gameObject.activeInHierarchy
            ? "WalkWithPlateau"
            : "Walk";

        if (Has(state))
            // The capsule's arms and torso differ a lot between the pitcher
            // turn and the old-character tray walk. A short blend preserves
            // the turn but still looks like a cut in that silhouette; give the
            // walk almost a third of a second to take the weight naturally.
            animator.CrossFadeInFixedTime(state, departureWalkBlend, 0, 0f);
    }

    private void RestoreReactionPlateau()
    {
        if (plateau != null)
            plateau.SetActive(plateauHiddenForDeparture
                ? false
                : plateauWasActive);
    }

    private void OnDisable()
    {
        // A pool/despawn can interrupt the arrival pivot before the next idle
        // frame restores the tray. Put that temporary visibility change back
        // before the ordinary reaction/departure cleanup decides to return.
        RestoreArrivalTurnPlateau();

        // Coroutines stop without executing their remaining lines when the
        // component's GameObject is disabled. Restore here so pooling/reusing a
        // customer cannot bring it back with a permanently invisible tray.
        if (!reacting && !plateauHiddenForDeparture)
            return;

        // This object is already being disabled, so restoring its authored
        // state cannot flash on screen. It only makes a pooled/reused customer
        // start clean next time.
        if (plateau != null)
            plateau.SetActive(plateauWasActive);

        plateauHiddenForDeparture = false;
        departing = false;
        reacting = false;
        DepartureCanMove = false;
        reactionRoutine = null;
    }

    // Squash, spring, settle.
    //
    // One damped cosine does all three: it starts at full squash on the frame
    // the feet stop, crosses through neutral, overshoots into a stretch, and
    // dies out at exactly the size it began. Two tweens chained would meet in
    // the middle at a seam, and a seam in a bounce is a hitch.
    //
    // Width moves against height by half, which is what keeps it reading as a
    // body compressing rather than a model being resized
    private void Landing()
    {
        landAge += Time.deltaTime;

        float t = Mathf.Clamp01(landAge / LandTime);

        if (t >= 1f)
        {
            landAge = -1f;

            if (animator != null)
            {
                animator.transform.localScale = bodyScale;
                animator.transform.localPosition = bodyHome;
            }

            return;
        }

        if (animator == null)
        {
            landAge = -1f;
            return;
        }

        // -1 at the start, +1 at the halfway point, fading to nothing
        float wave = -Mathf.Cos(t * Mathf.PI * 2f) * (1f - t);

        float squash = wave * LandSquash;

        animator.transform.localScale = new Vector3(
            bodyScale.x * (1f - squash * .5f),
            bodyScale.y * (1f + squash),
            bodyScale.z * (1f - squash * .5f));

        // Only on the way up. A hop downwards is a character sinking into the
        // floor, which is a different animation entirely
        animator.transform.localPosition =
            bodyHome + Vector3.up * (Mathf.Max(0f, wave) * LandHop);
    }

    public void ManageAnimations(Vector3 velocity)
    {
        ManageAnimations(velocity, velocity);
    }

    // Two directions, because they are two questions. How fast the legs go
    // comes from what the agent is ACTUALLY doing; which way the body points
    // comes from where the path goes. Feeding one vector to both is what tied
    // the facing to every sidestep the avoidance made
    public void ManageAnimations(Vector3 velocity, Vector3 facing)
    {
        lastVelocity = velocity;

        facing.y = 0f;

        // Kept from the last good frame rather than zeroed. A heading of
        // nothing would spin the character to face world forward for the one
        // frame the agent is between paths
        if (facing.sqrMagnitude > .01f)
            heading = facing.normalized;
    }

    // Where the body is actually pointing this frame, which is not the same as
    // where it has been asked to point -- the turn takes time. Read by the
    // navigation, which slows down for whatever is left of it
    public Vector3 Facing => animator == null ? transform.forward : animator.transform.forward;

    public void StartWalking()
    {
        CancelArrivalTurn();
        isSitting = false;
    }

    public void Stop()
    {
        lastVelocity = Vector3.zero;
    }

    private void HandleAnimations()
    {
        if (lastVelocity.magnitude > 0)
        {
            animator.SetFloat("moveSpeed", lastVelocity.magnitude);
            PlayWalkAnimation();
        }
        else if (departing)
        {
            // An exit walk may have one stationary NavMesh frame before its
            // first velocity sample. Staying on the walk prevents that frame
            // from selecting IdleWithPlateau after the authored 180 turn.
            PlayWalkAnimation();
        }
        else
        {
            PlayIdleAnimation();
        }

        // Outside the walking branch on purpose.
        //
        // Arriving at a spot in a queue ends with turning to face the counter,
        // and for a person that turn is part of arriving -- it happens as the
        // last steps are taken and finishes just after them. Inside the branch
        // the legs stopping cut the turn off, so the facing was set by a snap
        // instead: the customer landed and then spun on the spot
        Turn();
    }

    // Turned at a fixed rate, towards where the path goes.
    //
    // Two separate things were making this sway, and both are in this one line
    // of what it replaced.
    //
    // It followed agent.velocity, which is the steering AFTER local avoidance
    // has pushed the agent aside for everything else on the floor -- and with
    // every customer on the same avoidance priority, that push changed
    // direction several times a second all the way to the counter.
    //
    // And it lerped BETWEEN two direction vectors, which is a straight line
    // through the space between them and not a turn at all. The step it takes
    // grows with the distance between the two, so a jittery target is chased
    // hardest at exactly the moments it jitters most.
    //
    // A rate limit is the opposite: two degrees of shove is corrected at the
    // same speed as ninety, so the small ones never build into a visible swing
    private void Turn()
    {
        if (heading.sqrMagnitude < .0001f)
            return;

        Vector3 forward = animator.transform.forward;

        forward.y = 0f;

        // Only reachable if something left the body looking straight up or
        // down, which nothing here does -- but normalising a zero vector is a
        // silent NaN and it would stay one for the rest of the customer's life
        if (forward.sqrMagnitude < .0001f)
        {
            animator.transform.forward = heading;
            return;
        }

        float degrees = lastVelocity.sqrMagnitude > .0225f
            ? TurnSpeed
            : arrivalTurnSpeed;

        animator.transform.forward = Vector3.RotateTowards(
            forward.normalized, heading, degrees * Mathf.Deg2Rad * Time.deltaTime, 0f);
    }

    private void PlayWalkAnimation()
    {
        string state = plateau != null && plateau.gameObject.activeInHierarchy
            ? "WalkWithPlateau"
            : "Walk";

        PlayLoop(state);
    }

    // How far the body still has to swing, signed, in degrees.
    //
    // Read off the same two vectors Turn() steers by, so the animation and the
    // rotation can never disagree about which way this is going
    private float TurnError()
    {
        if (heading.sqrMagnitude < .0001f)
            return 0f;

        Vector3 forward = animator.transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < .0001f)
            return 0f;

        return Vector3.SignedAngle(forward.normalized, heading, Vector3.up);
    }

    // Wide enough that the constant small corrections a walk produces never
    // trip it, narrow enough that an actual about-face does
    private const float turnAnimationAngle = 12f;
    private const float maxArrivalClipAngle = 24f;
    private const float maxArrivalTurnTime = .2f;

    private bool Has(string state)
    {
        return animator.HasState(0, Animator.StringToHash(state));
    }

    private void PlayIdleAnimation()
    {
        // Arrival uses the Pitcher_Turn_90 family, like the empty-handed 180
        // departure. The plateau is hidden for the pivot and restored only
        // after the body has squared up to the counter.
        float error = !departing ? TurnError() : 0f;

        if (Mathf.Abs(error) >= turnAnimationAngle)
        {
            string turning = error < 0f ? "TurnLeft" : "TurnRight";

            if (Has(turning))
            {
                HidePlateauForArrivalTurn();
                PlayLoop(turning);
                return;
            }
        }

        RestoreArrivalTurnPlateau();

        string idle = plateau != null && plateau.gameObject.activeInHierarchy
            ? "IdleWithPlateau"
            : "Idle";

        PlayLoop(idle);
    }

    private void HidePlateauForArrivalTurn()
    {
        if (plateau == null || plateauHiddenForArrivalTurn)
            return;

        plateauWasActiveBeforeArrivalTurn = plateau.activeSelf;
        plateauHiddenForArrivalTurn = true;
        plateau.SetActive(false);
    }

    private void RestoreArrivalTurnPlateau(bool arrivedAndWaiting = false)
    {
        if (!plateauHiddenForArrivalTurn)
            return;

        if (plateau != null && !plateauHiddenForDeparture)
            // A pooled customer can reach this turn with the tray inactive
            // from its previous departure. Once this specific sequence
            // completes it is no longer walking or turning: it is waiting at
            // the counter, where the empty tray must be visible. Interrupted
            // turns still restore the captured state through the default path.
            plateau.SetActive(arrivedAndWaiting ||
                              plateauWasActiveBeforeArrivalTurn);

        plateauHiddenForArrivalTurn = false;
    }

    private void RevealArrivalTurnPlateau()
    {
        if (plateauHiddenForArrivalTurn && plateau != null &&
            !plateauHiddenForDeparture)
            plateau.SetActive(true);
    }

    private void CancelArrivalTurn()
    {
        if (arrivalTurnRoutine != null)
            StopCoroutine(arrivalTurnRoutine);

        arrivalTurnRoutine = null;
        arrivalTurning = false;
        RestoreArrivalTurnPlateau();
    }

    private void PlayLoop(string state)
    {
        if (!Has(state))
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

        if (current.IsName(state))
            return;

        // Most important at Turn180 -> Walk: Animator's current state remains
        // Turn180 until the crossfade is complete. Calling Play every frame in
        // that window cut the blend and produced the visible hitch. The next
        // state is already exactly what we want, so leave the transition alone.
        if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state))
            return;

        animator.CrossFadeInFixedTime(state, .12f, 0);
    }

    public void PlaySitDownAnimation(Vector3 facing)
    {
        isSitting = true;
        animator.Play("Sit");
        Face(facing);
    }

    // Turned into, not snapped to. What this is for is the moment a customer
    // reaches their spot and squares up to the counter, and that is a movement
    // -- the version that set the rotation outright read as the character
    // teleporting into a new direction the instant their feet stopped
    public void TurnTo(Vector3 facing)
    {
        facing.y = 0f;

        if (facing.sqrMagnitude < .0001f)
            return;

        heading = facing.normalized;

        // StartIdleState can repeat the same request when NavMesh settles on
        // its final frame. Do not cancel and restart a turn already in its last
        // beat; restarting was also resetting the tray timing.
        if (arrivalTurning)
            return;

        // Customer.StartIdleState makes the waiting tray visible before it
        // requests this turn. Update order between the two components is not
        // guaranteed, so waiting until our next Update left one rendered frame
        // with the tray already in hand. Hide it synchronously: no tray for any
        // frame of the turn; PlayIdleAnimation restores it after TurnError has
        // fallen below the threshold and the customer is genuinely waiting.
        float error = TurnError();
        string turning = error < 0f ? "TurnLeft" : "TurnRight";

        if (departing || Mathf.Abs(error) < turnAnimationAngle || !Has(turning))
            return;

        CancelArrivalTurn();
        HidePlateauForArrivalTurn();
        arrivalTurnRoutine = StartCoroutine(ArrivalTurn(turning, heading));
    }

    // The hard version, kept for sitting down: that one is a cut, not a turn.
    // It sets the heading as well, or the smooth turn would spend the next
    // quarter second dragging the character back off the chair
    public void Face(Vector3 facing)
    {
        facing.y = 0f;

        if (facing.sqrMagnitude < .0001f)
            return;

        heading = facing.normalized;

        animator.transform.forward = heading;
    }
}
