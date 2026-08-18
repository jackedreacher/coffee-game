using UnityEngine;

public class CustomerAnimator : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject plateau;

    [Header(" Settings ")]
    [Tooltip("Govde saniyede kac derece doner. 0 = varsayilan 540")]
    [SerializeField] private float turnSpeed = 540f;

    // Sentinel, for the reason every number in this project has one: this field
    // lands on prefabs that were saved before it existed, and a serialised zero
    // is indistinguishable from a field nobody filled in
    private float TurnSpeed => turnSpeed > .01f ? turnSpeed : 540f;

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
        if (!isSitting)
            HandleAnimations();

        // Outside the sitting test. Someone who sits down mid-bounce still has
        // to be put back to their own size, or they spend the rest of the scene
        // in a chair slightly squashed
        if (landAge >= 0f)
            Landing();
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

        animator.transform.forward = Vector3.RotateTowards(
            forward.normalized, heading, TurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
    }

    private void PlayWalkAnimation()
    {
        if (plateau == null)
            animator.Play("Walk");
        else
        {
            if (plateau.gameObject.activeInHierarchy)
                animator.Play("WalkWithPlateau");
            else
                animator.Play("Walk");
        }
    }

    private void PlayIdleAnimation()
    {
        if (plateau == null)
            animator.Play("Idle");
        else
        {
            if (plateau.gameObject.activeInHierarchy)
                animator.Play("IdleWithPlateau");
            else
                animator.Play("Idle");
        }
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

        if (facing.sqrMagnitude > .0001f)
            heading = facing.normalized;
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
