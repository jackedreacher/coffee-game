using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject plateau;

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

    // Same reason as walkSpeedReference: added to a component already saved in
    // the scene, so zero means nobody set it rather than "no hold at all"
    private float ActionHoldTime => actionHoldTime > .001f ? actionHoldTime : .35f;

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

    // The VISUAL is moved, never the player root. The root is the NavMeshAgent's
    // transform: nudging it forward would either be dragged straight back the
    // same frame or, worse, stick -- every station tap walking the player a few
    // centimetres further into the counter
    private Vector3 hopBase;
    private Coroutine hopRoutine;

    private void Awake()
    {
        if (animator != null)
            hopBase = animator.transform.localPosition;
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

    private void PlayState(string state)
    {
        if (state == requestedState)
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
            animator.transform.localPosition = hopBase;
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

            // Read every frame: the character is still turning towards the
            // station while this runs, and a direction captured at the start
            // would send the lunge off at the angle it was standing at
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
        yield return PlayOnce(prefix + "_Start");

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
    private IEnumerator PlayOnce(string state)
    {
        if (!Has(state))
            yield break;

        PlayState(state);

        yield return null;

        // Mid-blend the state being entered is the NEXT one; asking for the
        // current one returns the state being faded out and times the wait
        // against the wrong clip
        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        float speed = Mathf.Abs(info.speed * info.speedMultiplier);

        yield return new WaitForSeconds(info.length / Mathf.Max(.01f, speed));
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

            PlayWalkAnimation();

            animator.transform.forward = moveVector.normalized;
        }
        else
        {
            PlayIdleAnimation();
        }
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

    private void LateUpdate()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ReportPlayingClip();
#endif

        if (!hasOverride)
            return;

        animator.transform.forward = Vector3.RotateTowards(
            animator.transform.forward,
            overrideDirection,
            overrideTurnSpeed * Time.deltaTime,
            0f);
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

    private bool Carrying()
    {
        return plateau != null && plateau.activeInHierarchy;
    }
}
