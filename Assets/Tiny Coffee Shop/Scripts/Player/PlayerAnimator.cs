using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject plateau;

    public Animator CurrentAnimator => animator;

    public void ReplaceAnimator(Animator replacement)
    {
        if (replacement == null)
            return;

        animator = replacement;
        hasActionSpeed = null;
    }

    [Header(" Yurume Temposu ")]
    // Was a bare / 1.5f, chosen when the joystick moved the player at about 5.
    // At 40 that divisor asks the walk clip for twenty-six cycles a second, so
    // the number had to become a number anyone can see.
    //
    // Expressed as a speed rather than a divisor because a speed can be checked
    // against something: it is the pace the clip was drawn at, so if the legs
    // and the floor disagree, this is the one value that is wrong
    [Tooltip("Bacaklar bu hizda normal tempoda doner. DUSURURSEN ANIMASYON HIZLANIR. " +
             "Klibin kendi hizinin altina hicbir zaman inmez. 0 = varsayilan 10")]
    [SerializeField] private float walkSpeedReference = 10f;

    // Zero means nobody set it. This field arrived on a component already saved
    // in the scene, and one of those does not reliably come back carrying its
    // initializer -- a zero here would divide the playback speed by nothing
    private float WalkSpeedReference => walkSpeedReference > .01f ? walkSpeedReference : 10f;

    // Never below the clip's own tempo.
    //
    // The floor started at .5 to cover the acceleration out of idle, and that
    // was the slow motion: a kitchen is small enough that the player spends
    // most of every trip below the reference speed, so .5 was not the exception
    // it was meant to be, it was the normal case. At 1 the legs run at the pace
    // they were drawn at and only ever speed UP, which is the pace that looked
    // right before any of this was tied to velocity
    private const float minimumPlayback = 1f;

    // Work animations: a three part Start / Loop / End the character plays while
    // standing at a station. The names match the clips in the character fbx, so
    // adding another kind of work is adding a value here and running the setup
    // command -- Chop is sitting in the same file unused
    public enum Action
    {
        Assembly,
        Pan,

        // Handing a plate to a customer, fetching something, putting something
        // down. Each is one clip rather than a three part Start/Loop/End --
        // PlayOnce skips a state the controller does not have, so an action
        // with only a _Start plays its one motion and blends back
        Serve,
        PickUp,
        PickUpCooked,
        Drop,

        // Empty-handed tap on a customer: a short polite bow, with no item
        // transfer and no plateau pop.
        Greet,

        // The cowboy hat's revolver coming up. Wired but not required: the
        // controller has a Shoot_Start only if a clip for it was found, and
        // PlayAction already declines an action whose states are missing --
        // loudly, in the console, rather than by freezing mid stride. So the
        // shot works with or without one, and buying an animation for it is
        // dropping a file in and re-running command 2.
        Shoot,
    }

    [Tooltip("Is animasyonunun oynatma hizi. 1 = normal, 2 = iki kat hizli. 0 = varsayilan 1.8")]
    [SerializeField] private float actionSpeed = 1.8f;

    private float ActionSpeed => actionSpeed > .01f ? actionSpeed : 1.8f;

    private const string actionSpeedParameter = "actionSpeed";

    // Looked up once. SetFloat on a parameter the controller does not have logs
    // a warning every time it is called, and this is called on every tap
    private bool? hasActionSpeed;

    private bool HasActionSpeed()
    {
        if (hasActionSpeed.HasValue)
            return hasActionSpeed.Value;

        bool found = false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name != actionSpeedParameter ||
                parameter.type != AnimatorControllerParameterType.Float)
                continue;

            found = true;
            break;
        }

        hasActionSpeed = found;

        return found;
    }

    // Which of the three clips actually play.
    //
    // Was a negative number on the hold time meaning "skip the middle", which
    // is the kind of rule that has to be looked up. It also could not express
    // the third case: Pan_Loop is a pan-SHAKING cycle, so Start-Loop-End reads
    // as two separate swings in the air, and sometimes only the reach is wanted
    public enum ActionShape
    {
        UzanCalisGeriCek,
        UzanGeriCek,
        SadeceUzan,
    }

    [Tooltip("Is animasyonunun kac parcasi oynasin. " +
             "Pan_Loop bir sallama dongusu, havada fazladan savurma o")]
    [SerializeField] private ActionShape actionShape = ActionShape.UzanGeriCek;

    [Tooltip("Sadece Uzan-Calis-GeriCek secildiyse: ortadaki Loop kac saniye tutulsun")]
    [SerializeField] private float actionHoldTime = .35f;

    [Tooltip("Drop_Start klibinin ne kadari oynasin. 0 = varsayilan 0.50, 1 = tamami")]
    [Range(.1f, 1f)]
    [SerializeField] private float dropClipFraction = .5f;

    // Same reason as walkSpeedReference: added to a component already saved in
    // the scene, so zero means nobody set it rather than "no hold at all"
    private float ActionHoldTime => actionHoldTime > .001f ? actionHoldTime : .35f;
    private float DropClipFraction => dropClipFraction > .001f
        ? Mathf.Clamp(dropClipFraction, .1f, 1f)
        : .5f;

    // Above the arrival threshold TapToServe uses, so the residue of stopping
    // never counts as walking away
    private const float actionCancelSpeed = .3f;

    private Coroutine actionRoutine;

    [Header(" Is Zipllamasi ")]
    [Tooltip("Is animasyonunda karakter kendi onune dogru ne kadar atilsin. 0 = kapali")]
    [SerializeField] private float actionHopForward = .25f;

    [Tooltip("Ayni anda ne kadar yukari kalksin. 0 = duz ileri atilma")]
    [SerializeField] private float actionHopUp = .12f;

    [Tooltip("Atilip geri gelmesi kac saniye sursun. 0 = varsayilan 0.28")]
    [SerializeField] private float actionHopTime = .28f;

    private float ActionHopTime => actionHopTime > .001f ? actionHopTime : .28f;

    [Header(" Verme Pop Efekti ")]
    [Tooltip("Yemek verildiginde gorsel govde ne kadar buyusun. 0 = varsayilan 0.08 (%8)")]
    [SerializeField] private float transferPopScale = .08f;

    [Tooltip("Buyuyup eski boyuna donmesi kac saniye sursun. 0 = varsayilan 0.20")]
    [SerializeField] private float transferPopTime = .2f;

    [Tooltip("Pop sirasinda govde one kac derece egilsin. 0 = varsayilan 6")]
    [SerializeField] private float transferPopLean = 6f;

    private float TransferPopScale => transferPopScale > .001f ? transferPopScale : .08f;
    private float TransferPopTime => transferPopTime > .001f ? transferPopTime : .2f;
    private float TransferPopLean => transferPopLean > .001f ? transferPopLean : 6f;

    // The VISUAL is moved, never the player root. The root is the NavMeshAgent's
    // transform: nudging it forward would either be dragged straight back the
    // same frame or, worse, stick -- every station tap walking the player a few
    // centimetres further into the counter
    private Vector3 hopBase;
    private Vector3 hopBaseScale;
    private Coroutine hopRoutine;
    private float transferPopBump;
    private bool transferLeanApplied;

    private void Awake()
    {
        if (animator != null)
        {
            hopBase = animator.transform.localPosition;
            hopBaseScale = animator.transform.localScale;
        }
    }

    [Tooltip("Animasyonlar arasi gecis suresi. 0 = varsayilan 0.12, EKSI = gecis yok")]
    [SerializeField] private float blendTime = .12f;

    private float BlendTime => Mathf.Abs(blendTime) < .001f ? .12f : blendTime;

    // Which state was last asked for, so the same one is not re-requested.
    //
    // Everything here used to call animator.Play from Update, once per frame.
    // Play is a cut: the character arrives in the new pose on the next frame
    // with nothing in between, which between a run and a reach at a counter is
    // a visible snap. CrossFade blends instead -- but a crossfade restarted
    // every frame never gets anywhere, so it can only be issued on a change
    private string requestedState;

    private void PlayState(string state, bool restart = false)
    {
        if (!restart && state == requestedState)
            return;

        requestedState = state;

        float blend = BlendTime;

        if (blend <= 0f)
            animator.Play(state, 0, 0f);
        else
            animator.CrossFadeInFixedTime(state, blend, 0, 0f);
    }

    // Plays over the walk and idle. Nothing else changes: the station has
    // already done its work by the time this is called, so an interrupted
    // animation costs nothing but the animation
    // Still running, so whoever asked for it can wait for the end before doing
    // the thing the animation is about
    public bool ActionPlaying => actionRoutine != null;

    // Survives movement when the caller says so. Starting the reach a step
    // early is the point of that flag: the character has to keep the pose while
    // the last of the walk plays out, and the ordinary rule -- any movement
    // cancels -- would kill it on the frame it started
    private bool actionSurvivesMovement;

    public void PlayAction(Action action, bool keepWhileMoving = false)
    {
        if (!isActiveAndEnabled || animator == null)
            return;

        // Picking food up is represented by the plateau + food popping into
        // the hand. Never let an old caller or a misclassified station bring
        // the long reach / chef-kiss clips back: those clips move the hand after
        // the tray has appeared and make the food sweep through the character.
        if (action == Action.PickUp || action == Action.PickUpCooked)
        {
            CancelAction();
            return;
        }

        actionSurvivesMovement = keepWhileMoving;

        string prefix = action.ToString();

        // A controller that was never given these states would otherwise leave
        // ManageAnimations suppressed while the coroutine waited on a state the
        // animator never entered -- the character would freeze mid stride
        if (!Has(prefix + "_Start") && !Has(prefix + "_Loop"))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Said out loud rather than returned quietly. A missing state and a
            // station that was never tapped produce the same nothing on screen
            Debug.Log("[Is] " + prefix + ": controller'da " + prefix +
                      "_Start / _Loop state'i yok.\n" +
                      "  Cooked Fast > Animasyon: 4 - Is Animasyonlarini Ekle", this);
#endif
            return;
        }

        if (actionRoutine != null)
            StopCoroutine(actionRoutine);

        // Written before the routine starts, so the length PlayOnce measures is
        // already the shortened one -- it divides the clip by whatever the
        // multiplier turns out to be
        if (HasActionSpeed())
            animator.SetFloat(actionSpeedParameter, ActionSpeed);

        actionRoutine = StartCoroutine(ActionRoutine(prefix));

        // Station work may keep the old forward hop. Food transfers get their
        // own scale-only pop at the exact frame the item changes hands;
        // starting one here would happen on approach and then play twice.
        if (action == Action.Assembly || action == Action.Pan)
            StartHop();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Is] " + prefix + " basladi", this);
#endif
    }

    public void CancelAction()
    {
        StopHop();

        actionSurvivesMovement = false;

        if (actionRoutine == null)
            return;

        StopCoroutine(actionRoutine);
        actionRoutine = null;
    }

    // Holds the working pose after the reach is over and the food has actually
    // arrived. Without it the plate lands in the hand on the same frame the
    // character straightens up and starts walking, so the one moment the pickup
    // is readable is the moment it is thrown away
    public void HoldActionPose(Action action, float seconds)
    {
        if (!isActiveAndEnabled || animator == null || seconds <= 0f)
            return;

        string state = action.ToString() + "_Loop";

        if (!Has(state))
            return;

        if (actionRoutine != null)
            StopCoroutine(actionRoutine);

        // Deliberately NOT surviving movement: this one is a beat to look at,
        // and a player who taps somewhere else should leave immediately rather
        // than stand admiring a plate
        actionSurvivesMovement = false;

        actionRoutine = StartCoroutine(HoldRoutine(state, seconds));
    }

    private IEnumerator HoldRoutine(string state, float seconds)
    {
        PlayState(state);

        yield return new WaitForSeconds(seconds);

        actionRoutine = null;
    }

    private void StartHop()
    {
        if (animator == null)
            return;

        if (Mathf.Abs(actionHopForward) < .001f && Mathf.Abs(actionHopUp) < .001f)
            return;

        StopHop();

        hopRoutine = StartCoroutine(HopRoutine());
    }

    // A small visual acknowledgement at the exact give/drop moment. No slide,
    // no direction and no root movement: the visual body only grows once and
    // settles back, while the NavMeshAgent/collider remain completely still.
    public void PlayTransferPop()
    {
        if (!isActiveAndEnabled || animator == null)
            return;

        StopHop();
        hopRoutine = StartCoroutine(TransferPopRoutine());
    }

    private IEnumerator TransferPopRoutine()
    {
        Transform visual = animator.transform;
        float duration = TransferPopTime;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float bump = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
            transferPopBump = bump;
            visual.localScale = hopBaseScale * (1f + TransferPopScale * bump);

            yield return null;
        }

        visual.localScale = hopBaseScale;
        transferPopBump = 0f;
        hopRoutine = null;
    }

    // Put back on the way out, not just at the end of a completed hop. A hop
    // interrupted by walking off would otherwise leave the body parked at its
    // furthest point, permanently offset from the feet
    private void StopHop()
    {
        if (hopRoutine != null)
        {
            StopCoroutine(hopRoutine);
            hopRoutine = null;
        }

        if (animator != null)
        {
            animator.transform.localPosition = hopBase;
            animator.transform.localScale = hopBaseScale;
        }

        transferPopBump = 0f;
    }

    private IEnumerator HopRoutine()
    {
        Transform visual = animator.transform;

        float duration = ActionHopTime;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            // Out and back on one curve. A sine hump is 0 at both ends and 1 in
            // the middle, so there is no seam where two tweens would hand over
            float bump = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);

            Vector3 forward = visual.parent == null
                ? visual.forward
                : visual.parent.InverseTransformDirection(visual.forward);

            visual.localPosition = hopBase +
                                   forward.normalized * (actionHopForward * bump) +
                                   Vector3.up * (actionHopUp * bump);

            yield return null;
        }

        visual.localPosition = hopBase;
        hopRoutine = null;
    }

    private IEnumerator ActionRoutine(string prefix)
    {
        // DropOff's second half is the waiter lingering over the counter. For
        // a fast kitchen interaction the readable part is the first half: lean
        // in, put the item down, then blend back. Serve uses the same source
        // clip but deliberately remains full length; this rule belongs to the
        // Drop action, not to the shared animation asset.
        float startFraction = prefix == Action.Drop.ToString()
            ? DropClipFraction
            : 1f;

        yield return PlayOnce(prefix + "_Start", startFraction);

        if (actionShape == ActionShape.UzanCalisGeriCek && Has(prefix + "_Loop"))
        {
            PlayState(prefix + "_Loop");

            yield return new WaitForSeconds(ActionHoldTime);
        }

        // Nothing to play on the way out in the shortest shape. The blend back
        // to idle or walk is the return, and one clip is one motion -- which is
        // the whole reason this setting exists
        if (actionShape != ActionShape.SadeceUzan)
            yield return PlayOnce(prefix + "_End");

        actionSurvivesMovement = false;
        actionRoutine = null;
    }

    // Waits out one state without needing its length written down anywhere. The
    // length is only readable once the animator has actually entered the state,
    // which is the frame after the request -- hence the yield before the read
    private IEnumerator PlayOnce(string state, float fraction = 1f)
    {
        if (!Has(state))
            yield break;

        // One-shot actions must restart even when the previous tap requested
        // this exact state. This happens with adjacent fryers: tapping the
        // second one cancels the first coroutine before Idle/Walk gets a frame
        // to replace requestedState, so the ordinary duplicate guard would
        // suppress the second Drop_Start completely.
        PlayState(state, true);

        yield return null;

        // Mid-blend the state being entered is the NEXT one; asking for the
        // current one returns the state being faded out and times the wait
        // against the wrong clip
        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        float speed = Mathf.Abs(info.speed * info.speedMultiplier);

        float part = Mathf.Clamp01(fraction);

        yield return new WaitForSeconds(
            info.length * part / Mathf.Max(.01f, speed));
    }

    private bool Has(string state)
    {
        return animator.HasState(0, Animator.StringToHash(state));
    }

    public void ManageAnimations(Vector3 moveVector, float moveSpeed)
    {
        // A station animation outranks standing still, but not walking away.
        // Holding it through a move order would leave the character sliding to
        // the next counter in a chopping pose.
        //
        // Measured against moveSpeed and not moveVector: the vector arrives
        // normalised, so its magnitude is 1 for any movement at all, including
        // the last of the deceleration still bleeding off at the moment the
        // station is tapped. Every work animation would have been cancelled on
        // the frame it started
        if (actionRoutine != null)
        {
            if (actionSurvivesMovement || moveSpeed <= actionCancelSpeed)
                return;

            CancelAction();
        }

        if (moveVector.magnitude > 0)
        {
            animator.SetFloat("moveSpeed",
                Mathf.Max(minimumPlayback, moveSpeed / WalkSpeedReference));

            Vector3 wanted = moveVector.normalized;

            // A turn clip is one planted 90-degree step, not a locomotion
            // cycle. Playing it while the NavMeshAgent is translating made a
            // sharp direction change look like one enormous sideways stride:
            // the agent kept moving while the legs were busy doing a single
            // pivot. Translation always gets the looping walk. Face() already
            // turns the visual smoothly, so no steering behaviour is lost.
            PlayWalkAnimation();

            Face(wanted);
        }
        else
        {
            PlayIdleAnimation();
        }
    }

    [Tooltip("Govdenin donme hizi, derece/saniye. En az 720 uygulanir. Sadece GORSELI dondurur -- " +
             "hareket yonu aninda degisir, kontrol hissi degismez. " +
             "Cok yuksek = donus animasyonu goze carpmaz. 0 = varsayilan 720")]
    [SerializeField] private float bodyTurnSpeed = 720f;

    // The scene still serializes the old 360 value. A changed initializer does
    // not overwrite saved data, so enforce the new responsive floor at runtime.
    private float BodyTurnSpeed => Mathf.Max(720f, bodyTurnSpeed);

    // Wide enough that steering along a corridor never trips it, narrow enough
    // that a real change of direction does
    private const float turnAnimationAngle = 45f;

    // How far the body still has to come round, signed, in degrees
    private float Swing(Vector3 wanted)
    {
        Vector3 forward = animator.transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < .0001f)
            return 0f;

        return Vector3.SignedAngle(forward.normalized, wanted, Vector3.up);
    }

    // The stable movement direction. Stored and applied in LateUpdate so the
    // Animator cannot write a root pose after Update and erase the turn.
    private Vector3 movementFacing;
    private bool hasMovementFacing;

    // At exactly 180 degrees left and right are equally short. Remembering the
    // last real choice gives that tie one deterministic answer instead of
    // allowing floating point noise to choose a different side every frame.
    private float lastTurnSide = 1f;

    // Rate limited rather than assigned.
    //
    // This used to be a straight write of forward, which is why there was never
    // a turn to animate: the body was already facing the new way on the frame
    // the input changed. What it costs is nothing that matters -- the thing
    // being turned is animator.transform, the VISUAL, and the character's
    // actual movement direction is set by the caller and is untouched. The
    // player still goes where they said immediately; only the body catches up
    private void Face(Vector3 wanted)
    {
        wanted.y = 0f;

        if (wanted.sqrMagnitude < .0001f)
            return;

        movementFacing = wanted.normalized;
        hasMovementFacing = true;
    }

    private void TurnVisualToward(Vector3 wanted, float maxDegrees)
    {
        if (animator == null)
            return;

        wanted.y = 0f;

        Vector3 forward = animator.transform.forward;
        forward.y = 0f;

        if (wanted.sqrMagnitude < .0001f)
            return;

        if (forward.sqrMagnitude < .0001f)
        {
            animator.transform.rotation = Quaternion.LookRotation(wanted.normalized, Vector3.up);
            return;
        }

        float currentYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float wantedYaw = Mathf.Atan2(wanted.x, wanted.z) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(currentYaw, wantedYaw);

        if (Mathf.Abs(delta) >= 179.5f)
            delta = 180f * lastTurnSide;
        else if (Mathf.Abs(delta) > .25f)
            lastTurnSide = Mathf.Sign(delta);

        float step = Mathf.Clamp(delta, -Mathf.Abs(maxDegrees), Mathf.Abs(maxDegrees));

        animator.transform.rotation = Quaternion.Euler(0f, currentYaw + step, 0f);
    }

    private bool PlayTurnAnimation(float swing)
    {
        // Only with something in the hands.
        //
        // The two turn clips are Waiter_Tray_Turn_*, which is a person pivoting
        // while carrying a tray -- arms up, holding a shape. On an empty handed
        // character that is the same miming problem the empty handed walk had,
        // and the walk clip already turns perfectly well on its own
        if (!Carrying())
            return false;

        if (Mathf.Abs(swing) < turnAnimationAngle)
            return false;

        string state = swing < 0f ? "TurnLeft" : "TurnRight";

        // Guarded, so a controller without the two states walks exactly as it
        // did before they existed
        if (!Has(state))
            return false;

        PlayState(state);

        return true;
    }
    
    // A facing asked for by something other than the walk.
    //
    // Applied in LateUpdate rather than where it is set, and that is the point:
    // ManageAnimations writes the move direction into the same transform from
    // Update, and which of the two lands would otherwise be script execution
    // order. Running afterwards means the override always wins, so the character
    // can turn towards a customer while the last stride is still playing instead
    // of swivelling once already stopped
    private bool hasOverride;
    private Vector3 overrideDirection;
    private float overrideTurnSpeed;

    public void FaceOverride(Vector3 direction, float turnSpeed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < .0001f)
            return;

        hasOverride = true;
        overrideDirection = direction.normalized;
        overrideTurnSpeed = turnSpeed;
    }

    public void ClearFaceOverride()
    {
        hasOverride = false;
    }

    // The root/NavMeshAgent can have stopped while the visual body is still
    // finishing its turn. Work gestures lean along the VISUAL's local forward,
    // so beginning one before this is true makes a side approach lean sideways
    // and look as if no forward gesture played at all.
    public bool IsFacing(Vector3 direction, float toleranceDegrees)
    {
        if (animator == null)
            return true;

        direction.y = 0f;

        Vector3 forward = animator.transform.forward;
        forward.y = 0f;

        if (direction.sqrMagnitude < .0001f || forward.sqrMagnitude < .0001f)
            return true;

        return Vector3.Angle(forward, direction) <= Mathf.Max(0f, toleranceDegrees);
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ReportPlayingClip();
#endif

        if (hasOverride)
            TurnVisualToward(overrideDirection,
                overrideTurnSpeed * Mathf.Rad2Deg * Time.deltaTime);
        else if (hasMovementFacing)
            TurnVisualToward(movementFacing, BodyTurnSpeed * Time.deltaTime);

        ApplyTransferLean();
    }

    // Applied after facing so the turn logic cannot erase the pitch. The
    // upright yaw is rebuilt every frame before leaning; multiplying the
    // current tilted rotation would add six more degrees every LateUpdate and
    // eventually flip the character, exactly the kind of cumulative transform
    // bug this effect must never introduce.
    private void ApplyTransferLean()
    {
        if (animator == null)
            return;

        // One zero-strength pass is required to stand upright after the final
        // tilted frame. After that, leave the visual transform entirely alone.
        if (transferPopBump <= .0001f && !transferLeanApplied)
            return;

        Vector3 forward = animator.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < .0001f)
            return;

        Quaternion upright = Quaternion.LookRotation(forward.normalized, Vector3.up);
        Vector3 right = upright * Vector3.right;
        float angle = TransferPopLean * transferPopBump;

        animator.transform.rotation = Quaternion.AngleAxis(angle, right) * upright;
        transferLeanApplied = transferPopBump > .0001f;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // What is on screen, as opposed to what the controller asset says.
    //
    // Those are two different claims and there was no way to compare them: the
    // asset can be read in the editor and shows the new clip, the character can
    // be watched in play mode and looks unchanged, and nothing connects the two.
    // Keyed on the clip name alone -- the speed rides along in the message but
    // is not part of the comparison, or a walk that varies with velocity would
    // log every frame
    private string lastClipReport;

    private void ReportPlayingClip()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        AnimatorClipInfo[] playing = animator.GetCurrentAnimatorClipInfo(0);
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        string clip = playing.Length <= 0 ? "KLIP YOK" : playing[0].clip.name;

        if (clip == lastClipReport)
            return;

        lastClipReport = clip;

        Debug.Log("[Animator] oynayan klip: " + clip +
                  "\n  uzunluk : " + (playing.Length <= 0 ? 0f : playing[0].clip.length)
                      .ToString("0.00") + " sn" +
                  "\n  hiz     : x" + (state.speed * state.speedMultiplier).ToString("0.00") +
                  "  (state " + state.speed.ToString("0.00") +
                  " x moveSpeed " + state.speedMultiplier.ToString("0.00") + ")" +
                  "\n  avatar  : " + (animator.avatar == null
                      ? "YOK"
                      : animator.avatar.name + (animator.avatar.isHuman ? " humanoid" : " HUMANOID DEGIL")) +
                  "\n  katman  : " + animator.layerCount +
                  "\n  agirlik : " + animator.GetLayerWeight(0).ToString("0.00"),
                  this);
    }
#endif

    private void PlayWalkAnimation()
    {
        PlayState(Carrying() ? "WalkWithPlateau" : "Walk");
    }

    private void PlayIdleAnimation()
    {
        PlayState(Carrying() ? "IdleWithPlateau" : "Idle");
    }

    // The tray, for anything that needs it out of the way for a moment.
    //
    // Null when there is nothing being carried, which is the same question
    // Carrying() answers -- asked by something that then needs the object
    // rather than the answer.
    public Transform Carried => Carrying() ? plateau.transform : null;

    private bool Carrying()
    {
        return plateau != null && plateau.activeInHierarchy;
    }
}
