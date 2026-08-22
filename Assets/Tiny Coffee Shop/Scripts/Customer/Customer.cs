using System;
using System.Collections;
using UnityEngine;

public class Customer : MonoBehaviour, IShootable
{
    private enum State
    {
        Idle = 0,
        Walking = 1,
        Drinking = 2
    }

    [Header(" Components ")]
    [SerializeField] private CustomerAnimator animator;
    [SerializeField] private NavigationAbility navigationAbility;
    [SerializeField] private Plateau plateau;

    [Header(" State ")]
    private State state;

    [Header(" Settings ")]
    // Waiting empty handed reads as standing about; waiting with a plate reads
    // as waiting to be served. Only while stood still though -- walking in
    // holding an empty plate reads as already served. CustomerAnimator picks the
    // holding clips off whether the tray is switched on, so the tray being on is
    // the whole of the change
    [SerializeField] private bool carriesEmptyPlateau = true;

    private Vector3 finalFacing;

    // True while the customer belongs to the queue. Arrival is not a reliable
    // moment to face on: an agent that stops short because a neighbour is in the
    // way never reports reaching its destination, so it never runs its callback
    // and is left pointing along whatever its last step happened to be
    private bool wantsQueueFacing;
    private bool orderShown;
    private bool arrivalTurnRequested;

    // Close enough that the authored pivot reads as the final step, but not a
    // whole stride: the old 1.5-metre hand-off produced the visible drift.
    private const float arrivalTurnWithin = .3f;

    // The order, as rows. One row is the old single-food order exactly; two is
    // "a burger and a fries", which was unsayable while an order was one food
    // and one number
    private OrderLine[] lines = new OrderLine[0];
    private int[] taken = new int[0];

    // What this customer owes so far. Runs up over a multi-item order and is
    // handed over in one piece when the last item lands
    private int earnings;

    [Header(" Actions ")]
    private Action reachedDestinationCallback;

    // A completed order can be handed over by TapToServe, OrderCounter or one
    // of the automatic FoodServingStations. Waiting in only one of those callers
    // lets the other two start navigation while Chef's Kiss is still playing.
    // The customer owns the final gate instead: no requested walk can begin
    // until its reaction has genuinely released the animator.
    private Coroutine walkAfterReactionRoutine;
    private Vector3 walkAfterReactionTarget;
    private Action walkAfterReactionCallback;

    // Leaving is not an ordinary GoTo: the body has to finish its reaction and
    // authored 180 turn before navigation is allowed to own it.
    private Coroutine leaveRoutine;
    private Vector3 leaveTarget;
    private Action leaveCallback;
    private bool leaveDisappointed;
    private bool leaving;

    [Header(" Order ")]
    [Tooltip("Kafasinin ustundeki siparis balonu. Bos birakilabilir")]
    [SerializeField] private CustomerOrder order;

    public OrderLine[] Lines => lines;

    // Read through the serialized reference that actually drives this
    // customer. GetComponent<CustomerAnimator>() from outside assumes both
    // scripts live on the same GameObject, which is not a contract the prefab
    // hierarchy makes.
    public bool IsReacting => animator != null && animator.IsReacting;

    public int FoodNeededCount
    {
        get
        {
            int total = 0;

            for (int i = 0; i < lines.Length; i++)
                total += lines[i].count;

            return total;
        }
    }

    public int FoodTakenCount
    {
        get
        {
            int total = 0;

            for (int i = 0; i < taken.Length; i++)
                total += taken[i];

            return total;
        }
    }

    // Kept for the callers that only ever had one thing to ask about: the first
    // row still owed. An empty order answers null, which is how the coffee shop
    // scene has always worked -- null means "whatever the counter serves"
    public SpawnableFood RequestedFood
    {
        get
        {
            int row = FirstOwing();

            return row < 0 ? null : lines[row].food;
        }
    }

    // Whether this food is still wanted. The question every serving path
    // actually has, and the one a single RequestedFood could not answer once an
    // order could name two things
    public bool Wants(SpawnableFood food)
    {
        return food != null && RowFor(food) >= 0;
    }

    // Rows are matched on type, not on the instance: the burger in the player's
    // hands is never the same object as the burger in the order
    private int RowFor(SpawnableFood food)
    {
        if (food == null)
            return -1;

        for (int i = 0; i < lines.Length; i++)
        {
            if (taken[i] >= lines[i].count)
                continue;

            // A row that names no food is a customer who has not decided, and
            // it takes whatever turns up. It used to be skipped here, which
            // made "wants anything" mean "wants nothing"
            if (lines[i].food == null || lines[i].food.GetType() == food.GetType())
                return i;
        }

        return -1;
    }

    // Nulls allowed, same reason.
    //
    // This is what CollectFood falls back to, and it was refusing the one kind
    // of row that can never be matched by type -- so an undecided customer was
    // handed their food, the row was never ticked off, and they stood there
    // owed an item that had already been given to them until the clock ran out
    private int FirstOwing()
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (taken[i] < lines[i].count)
                return i;
        }

        return -1;
    }

    // How well this customer was treated, as a multiplier on what they pay.
    // One when there is no bubble, so a scene without the order system behaves
    // exactly as it did before
    public float RewardMultiplier => order == null ? 1f : order.RewardMultiplier;

    // Awake rather than Start: the manager calls Initialize the moment it spawns
    // a customer, and that walks them off before Start would have run. Switching
    // the tray on once here lets Plateau run its own Awake, without which
    // IsEmpty answers false and every empty tray is taken for a loaded one
    private void Awake()
    {
        if (!carriesEmptyPlateau)
            return;

        plateau.gameObject.SetActive(true);
        UpdatePlateauVisibility();
    }

    // An empty tray belongs to standing still. A loaded one stays out whatever
    // the customer is doing, because the food has to go somewhere
    private void UpdatePlateauVisibility()
    {
        if (!carriesEmptyPlateau)
            return;

        // Departure is always empty-handed. A loaded plateau used to win this
        // condition and reappear as soon as StartWalkingState ran, even though
        // the turn animation had just hidden it.
        if (leaving)
        {
            plateau.gameObject.SetActive(false);
            return;
        }

        plateau.gameObject.SetActive(!plateau.IsEmpty || state == State.Idle);
    }

    private void Update()
    {
        // Shot customers have no business left. The walking state asks the
        // NavMeshAgent how far it has to go, and the agent was switched off the
        // moment the bullet landed -- so a customer shot on the way IN spent
        // the rest of their two seconds filling the console with the same
        // complaint, once a frame.
        if (shot)
            return;

        switch (state)
        {
            case State.Idle:
                HandleIdleState();
                break;
            case State.Walking:
                HandleWalkingState();
                break;
            case State.Drinking:
                break;
        }
    }

    // A count with no food named: whatever the counter serves, which is how the
    // coffee shop scene has always worked. One row with a null food
    public void Initialize(int foodNeededCount, Vector3 targetPosition, Vector3 finalFacing)
    {
        Initialize(new[] { new OrderLine(null, foodNeededCount) }, targetPosition, finalFacing);
    }

    // Overload for counters that sell more than one thing. Kept so the existing
    // callers compile untouched
    public void Initialize(int foodNeededCount, Vector3 targetPosition, Vector3 finalFacing,
        SpawnableFood requestedFood)
    {
        Initialize(new[] { new OrderLine(requestedFood, foodNeededCount) },
            targetPosition, finalFacing);
    }

    public void Initialize(OrderLine[] order, Vector3 targetPosition, Vector3 finalFacing)
    {
        lines = order != null && order.Length > 0
            ? order
            : new[] { new OrderLine(null, 1) };

        // Its own array rather than a field on the row: what was ORDERED does
        // not change while the order is being filled, and keeping the two apart
        // means nothing can quietly rewrite the order to match what was served
        taken = new int[lines.Length];

        this.finalFacing = finalFacing;

        // Not shown here. The bubble is nailed to a fixed spot once it opens,
        // and a spot picked while they are still crossing the floor is a spot
        // they were walking through -- the card would end up hanging over the
        // doorway. It opens when they come to rest instead
        orderShown = false;

        // Cleared here rather than trusted to be zero: a reused customer would
        // otherwise arrive already owed the last one's order
        earnings = 0;

        GoTo(targetPosition);
    }

    // Shuffling up the queue, or walking to it for the first time. Either way
    // the customer ends up standing in line, so the facing is restored the
    // moment they stop moving rather than only if they report an arrival
    public void GoTo(Vector3 targetPosition)
    {
        wantsQueueFacing = true;
        GoToThen(targetPosition, null);
    }

    // Callers with their own callback -- leaving, sitting down -- keep it. Those
    // are the moves where the queue facing is exactly what is not wanted.
    //
    // Answers whether the walk actually started. It could always fail and the
    // failure was always silent, which is survivable for a customer being moved
    // up the queue and is not for one being sent out of the door
    public bool GoToThen(Vector3 targetPosition, Action callback)
    {
        if (IsReacting)
        {
            // Only one destination may be armed. If a manager updates the exit
            // while the gesture is playing, the latest request is the honest
            // destination and two coroutines must not start two walks.
            walkAfterReactionTarget = targetPosition;
            walkAfterReactionCallback = callback;

            if (walkAfterReactionRoutine == null)
                walkAfterReactionRoutine = StartCoroutine(WalkAfterReaction());

            // The request was accepted and deliberately postponed. Callers use
            // false as "path failed, destroy/fallback now", which is not true.
            return true;
        }

        return TryStartWalk(targetPosition, callback);
    }

    // One exit door for every serving path. Updating the armed target is safe:
    // managers can discover the same departure on adjacent frames, but only
    // one gesture and one walk may ever start.
    public bool Leave(Vector3 targetPosition, Action callback, bool disappointed = false)
    {
        // A completed order can be observed by the player-serving path and an
        // automatic station on neighbouring frames. Once the first departure
        // owns this customer, a second request must not start another emote or
        // 180 turn while they are already walking out.
        if (leaving)
            return true;

        leaving = true;
        wantsQueueFacing = false;
        leaveTarget = targetPosition;
        leaveCallback = callback;
        leaveDisappointed |= disappointed;

        // Hide from the instant departure is requested, including while a
        // final Chef's Kiss is finishing. CustomerAnimator remembers the old
        // active state only for safe prefab/pool reuse; it will not restore the
        // plateau during the turn or exit walk.
        animator?.HidePlateauForDeparture();
        if (plateau != null)
            plateau.gameObject.SetActive(false);

        EnableNavigation();

        if (leaveRoutine == null)
            leaveRoutine = StartCoroutine(LeaveAfterPerformance());

        return true;
    }

    private IEnumerator LeaveAfterPerformance()
    {
        // Chef's Kiss is also a one-shot owned by CustomerAnimator. Let it
        // finish before starting the timeout emote/about-face sequence.
        while (IsReacting)
            yield return null;

        Vector3 target = leaveTarget;
        Action callback = leaveCallback;
        bool disappointed = leaveDisappointed;

        // Face the first LEGAL path leg. A direct line to the Exit Point can
        // pass through the counter; NavMesh then immediately asks for a
        // different direction and appears to pull the face back towards the
        // camera after the authored turn.
        Vector3 facing = navigationAbility.FirstHeadingTo(target);

        if (animator != null && animator.BeginDeparture(facing, disappointed))
        {
            // Wait through the emote and the authored part of Turn180. The
            // animator then exposes the turn->walk blend so NavMesh can start
            // during it; waiting until IsReacting became false created a tiny
            // in-place walk before the first ground movement.
            while (IsReacting && !animator.DepartureCanMove)
                yield return null;
        }

        leaveRoutine = null;
        leaveCallback = null;
        leaveDisappointed = false;

        // Navigation begins during the visual turn->walk compensation blend.
        // A failed path still invokes the exit callback so a dead customer
        // cannot remain in the shop forever.
        if (!TryStartWalk(target, callback))
            callback?.Invoke();
    }

    private IEnumerator WalkAfterReaction()
    {
        while (IsReacting)
            yield return null;

        Vector3 target = walkAfterReactionTarget;
        Action callback = walkAfterReactionCallback;

        walkAfterReactionRoutine = null;
        walkAfterReactionCallback = null;

        // The original caller already received "accepted", so it cannot run
        // its immediate fallback if pathfinding now fails. Invoking the arrival
        // callback is the equivalent delayed fallback: exits destroy cleanly,
        // and table customers still release/occupy their reserved chair.
        if (!TryStartWalk(target, callback))
            callback?.Invoke();
    }

    private bool TryStartWalk(Vector3 targetPosition, Action callback)
    {
        reachedDestinationCallback = callback;

        if (!navigationAbility.TryGoTo(targetPosition))
        {
            // Cleared, not left armed. A callback belongs to the walk it came
            // in with, and this walk is not happening -- left set, it fires on
            // whatever the customer arrives at next, which is somebody else's
            // business entirely
            reachedDestinationCallback = null;

            return false;
        }

        if (wantsQueueFacing)
            arrivalTurnRequested = false;

        StartWalkingState();

        return true;
    }

    public void CollectFood(SpawnableFood food)
    {
        // A second item may be handed over before the first Chef's Kiss has
        // finished. Re-enabling the plateau here used to put it straight back
        // into the customer's hands midway through that empty-handed gesture.
        // Plateau.Push works while its GameObject is inactive, so add the food
        // invisibly and let CustomerAnimator restore the loaded tray afterwards.
        bool reactionAlreadyPlaying = animator != null && animator.IsReacting;

        if (!reactionAlreadyPlaying)
            plateau.gameObject.SetActive(true);

        plateau.Push(food);

        // Which row this filled. Falls back to the first one still owing, for
        // the counters that hand over whatever they sell without naming it --
        // an order with no food in it is still an order for something
        int row = RowFor(food);

        if (row < 0)
            row = FirstOwing();

        if (row >= 0)
            taken[row]++;

        // The item is already on the customer's plateau, so this cannot fire
        // for a refused or mistapped delivery.
        animator?.ReactToFood();

        if (order == null)
            return;

        // Settled on the last item, and settled BEFORE the reward is worked
        // out. Reading the live clock later would score whatever it had drained
        // to by then rather than what the player was looking at when they served
        if (NeedsMoreFood())
            order.SetCounts(Remaining());
        else
            order.Settle();
    }

    // Per row, in the order's own order, so the bubble can put the number under
    // the picture it belongs to
    private int[] Remaining()
    {
        int[] left = new int[lines.Length];

        for (int i = 0; i < lines.Length; i++)
            left[i] = Mathf.Max(0, lines[i].count - taken[i]);

        return left;
    }

    // Rung up per item, paid once.
    //
    // Someone ordering three of something is ONE sale to the player. Paying
    // them item by item puts money in the air three times for a single order,
    // and the number that flashes over their head is a third of what was
    // earned. So it is added up here and handed over in one piece at the end.
    //
    // Answers what is left for the TILL to pay. A customer with a bubble pays
    // through it -- the number lifts off the card and lands in the counter, and
    // the money goes in when it lands -- so there is nothing left to do and
    // this answers 0. One without a bubble has nothing to fly, and the till
    // pays for them directly
    public int RingUp(int amount)
    {
        earnings += amount;

        if (NeedsMoreFood())
            return 0;

        int total = earnings;
        earnings = 0;

        // This line is reached only for the final requested item: earlier
        // items returned above while NeedsMoreFood was true. The customer is
        // sent home immediately after RingUp returns, so the cash sound lands
        // on the exact frame they begin to leave, not on the delayed money
        // number flight and not once per order tick.
        SoundManager.Play(SoundManager.Sound.Money);

        // The one measurement worth having, printed on every completed sale.
        //
        // How long an order takes is the number every patience setting is
        // guessed from, and guessing it twice already got it wrong twice. This
        // is the same number measured instead: seconds per item, straight into
        // the food prefab's Prep Seconds
        if (order != null)
        {
            int items = FoodNeededCount;

            Debug.Log("SATIS: " + Describe() +
                      "   verilen " + order.PatienceGiven.ToString("0.0") + " sn" +
                      ",  harcanan " + order.Waited.ToString("0.0") + " sn" +
                      ",  PARCA BASI " +
                      (order.Waited / Mathf.Max(1, items)).ToString("0.0") + " sn", this);
        }

        if (order == null)
            return total;

        // A bubble that refuses the job -- no card built, or already
        // celebrating -- hands the sale straight back rather than swallowing it
        return order.Celebrate(total) ? 0 : total;
    }

    // "2 x Burger + 1 x Fries". The measurement line is only useful if it says
    // what was measured, and a two row order timed as one number is a number
    // about nothing
    private string Describe()
    {
        string text = "";

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                text += " + ";

            text += lines[i].count + " x " +
                    (lines[i].food == null ? "?" : lines[i].food.GetType().Name);
        }

        return text;
    }

    // Waited the whole way and got nothing. Answered here rather than read off
    // the bubble by everyone, so a customer with no bubble simply never gives up
    public bool PatienceRanOut => order != null && order.RanOut;

    public float PatienceGiven => order == null ? 0f : order.PatienceGiven;
    public float Waited => order == null ? 0f : order.Waited;

    // ---- being shot --------------------------------------------------------

    // Roughly chest high on a customer standing at the counter. Not measured
    // off a renderer: a bounding box is measured in world axes, so a body that
    // leans or turns reports a different chest every frame, and the aim point
    // is wanted at a place on the CUSTOMER rather than a place in the room.
    private const float chestHeight = 1.2f;

    // How much faster the survivors move than they arrived. The clip is a run,
    // and a run animation played at a walking pace is a moonwalk.
    private const float fleeSpeed = 2.1f;

    private bool shot;

    public bool CanTakeShot => !shot && !leaving;
    public Vector3 ShotAimPoint => transform.position + Vector3.up * chestHeight;

    // Shot. Which is a fine, a body, and a room full of people who saw it.
    public string TakeShot()
    {
        if (!CanTakeShot)
            return null;

        shot = true;

        if (order != null)
            order.Hide();

        wantsQueueFacing = false;

        // Stopped rather than sent somewhere. The agent is what would otherwise
        // keep sliding the body along its last path while it lies on the floor.
        if (navigationAbility != null)
            navigationAbility.Disable();

        if (plateau != null)
            plateau.gameObject.SetActive(false);

        if (animator != null)
            animator.PlayDeath();

        HatPowerCatalogue book = HatPowerCatalogue.Get();

        int fine = book == null ? 25 : book.CustomerFine;
        float lie = book == null ? 2f : book.BodySeconds;

        // The money comes out when the number lands in the counter, not
        // here -- the same way a sale pays when its number lands. FineText
        // charges it immediately only if there is no counter to fly to.
        FineText.Show(transform.position + Vector3.up * (chestHeight + .7f),
                      fine, book == null ? null : book.fineFont);

        Mark(book);

        // Removed by the body itself rather than by whoever fired: this object
        // has to go whether or not the shooter is still around to see it.
        PopAway.After(gameObject, lie);

        Scatter();

        return "musteri vuruldu -- " + fine + " para gitti";
    }

    // How big the mark is and how far clear of the body it sits, in world
    // units. The body is lying down by the time this is seen, so it is clearing
    // a corpse rather than a head.
    private const float markSize = .85f;
    private const float markHeight = 1.9f;

    // The emoji over the body.
    //
    // Parented to the customer rather than left in the world, so it goes when
    // they go -- the pop takes it along without anything having to remember it
    // exists. Parented to the ROOT, not to the animated body: the body is
    // scaled to whatever animal this customer is, and an emoji sized by the
    // rabbit it happens to be sitting on is a different size on every corpse.
    private void Mark(HatPowerCatalogue book)
    {
        if (book == null || book.deadEmoji == null)
            return;

        GameObject mark = Instantiate(book.deadEmoji, transform);

        mark.name = "SHOT";

        Camera eye = Camera.main;

        // These are world space canvases authored 512 units across, and 512
        // units is most of the kitchen -- dropped in as they come they are not
        // an emoji, they are a wall.
        //
        // The authored size is measured rather than assumed, because it is a
        // property of whichever prefab somebody put in the catalogue. Same sum
        // the order bubble does for the mood faces out of the same pack.
        RectTransform rect = mark.transform as RectTransform;

        float authored = rect != null
            ? Mathf.Max(rect.rect.width, rect.rect.height)
            : 512f;

        mark.transform.localScale = Vector3.one * (markSize / Mathf.Max(1f, authored));

        // Lifted along the CAMERA's up, not the world's.
        //
        // Straight up put it in the middle of the body, and from this angle it
        // always would: the camera looks down steeply, so a metre of world
        // height is only a few pixels of screen height. The camera's own up
        // axis is the direction that means "higher on screen", which is the
        // direction the word above actually refers to.
        Vector3 clear = eye != null ? eye.transform.up : Vector3.up;

        mark.transform.position = transform.position + clear * markHeight;

        // Turned to the camera once rather than every frame. This camera never
        // rotates, and the thing it is pinned to has stopped moving for good.
        if (eye != null)
            mark.transform.rotation = eye.transform.rotation;
        else
            mark.transform.localRotation = Quaternion.identity;

        // Drawn over the room instead of being sorted into it by distance. A
        // flat card standing in a kitchen loses to the first counter it happens
        // to be behind, and the whole point of it is being seen.
        Canvas[] layers = mark.GetComponentsInChildren<Canvas>(true);

        for (int i = 0; i < layers.Length; i++)
        {
            layers[i].overrideSorting = true;
            layers[i].sortingOrder = 120;
        }
    }

    // Everyone else leaves, at speed.
    //
    // Asked of the counters rather than of every Customer in the scene, because
    // a counter also has to forget the ones it loses -- a customer torn out of
    // a queue without the queue being told leaves a slot nobody can stand in.
    private void Scatter()
    {
        FoodServingCustomerManager[] counters =
            FindObjectsByType<FoodServingCustomerManager>(FindObjectsSortMode.None);

        int ran = 0;

        for (int i = 0; i < counters.Length; i++)
            ran += counters[i].Scatter(this);

        Debug.Log(name + " vuruldu -- " + ran + " musteri kacti", this);
    }

    // Out of the door, running, and not a sale.
    //
    // Separate from GiveUp because that one costs a life: the customers who run
    // from a gunshot were not failed, they were frightened, and the price for
    // that was already taken out of the till.
    public void Flee(Vector3 exitPosition)
    {
        if (shot || leaving)
            return;

        if (order != null)
            order.Hide();

        wantsQueueFacing = false;

        if (animator != null)
            animator.Run(true);

        if (navigationAbility != null)
            navigationAbility.SetSpeed(navigationAbility.Speed * fleeSpeed);

        if (!Leave(exitPosition, () => Destroy(gameObject)))
            Destroy(gameObject);
    }

    // Off without their food, and it costs a life. No callback and no reward:
    // this is the one exit that is not a sale
    public void GiveUp(Vector3 exitPosition)
    {
        if (order != null)
            order.Hide();

        wantsQueueFacing = false;

        // Off the floor one way or the other.
        //
        // A customer whose exit cannot be pathed to used to stay exactly where
        // they were, for the rest of the game: the counter had already taken
        // the life and dropped them from its list, so nothing was left that
        // would ever ask them to move again. Walking out is the nice version of
        // leaving; this is the other one
        if (!Leave(exitPosition, () => Destroy(gameObject), true))
            Destroy(gameObject);
    }

    // Set before the bubble opens, because the clock starts when it opens.
    // Silently ignored by a customer with no bubble, which is right: no bubble
    // means no clock to give a length to
    public void SetPatience(float seconds)
    {
        if (order != null)
            order.SetPatience(seconds);
    }

    // How high this customer's card floats. Set by the queue, which is the only
    // thing that knows which row they are standing in
    public void SetBubbleLift(float amount)
    {
        if (order != null)
            order.SetLift(amount);
    }

    public void SetBubbleScale(float amount)
    {
        if (order != null)
            order.SetDisplayScale(amount);
    }

    public bool NeedsMoreFood()
    {
        return FoodTakenCount < FoodNeededCount;
    }

    public SpawnableFood Pop()
    {
        SpawnableFood food = plateau.Pop();

        if (food == null)
        {
            PutPlateauAway();
            return null;
        }

        if (plateau.IsEmpty)
            PutPlateauAway();

        return food;
    }

    // Handing the last item over used to put the tray away for good. A customer
    // who carries one keeps carrying it, empty, as long as they are stood
    // waiting rather than snapping back to arms at their sides
    private void PutPlateauAway()
    {
        if (carriesEmptyPlateau)
            UpdatePlateauVisibility();
        else
            plateau.gameObject.SetActive(false);
    }

    public void SitDown(Vector3 targetPosition, Vector3 facing)
    {
        wantsQueueFacing = false;
        DisableNavigation();
        transform.position = targetPosition.With(y: 0);
        StartDrinkingState(facing);
    }

    public void GetUpAndGo(Vector3 targetPosition, Action callback)
    {
        // Whatever they wanted, they have it or they are giving up on it
        if (order != null)
            order.Hide();

        // Off to the exit. Snapping back to face the counter on the way out
        // would be the queue facing applied to someone no longer in the queue
        wantsQueueFacing = false;

        // The caller is waiting on that callback to free the seat they were
        // sitting in. A walk that never starts never reaches its destination,
        // so it never calls back, and the seat stays taken by somebody who is
        // no longer using it
        if (!Leave(targetPosition, callback))
            callback?.Invoke();
    }

    private void EnableNavigation()
    {
        navigationAbility.Enable();
    }

    private void DisableNavigation()
    {
        navigationAbility.Disable();
    }

    private void StartDrinkingState(Vector3 facing)
    {
        state = State.Drinking;
        plateau.gameObject.SetActive(false);
        animator.PlaySitDownAnimation(facing);
    }

    private void FaceFinalFacing()
    {
        // Zero would set forward to nothing. Only reachable before Initialize,
        // but a customer pooled and re-used would hit it
        //
        // Asked for rather than applied. By the time this runs the turn is
        // usually already most of the way done -- it was started a stride out
        // -- and what is left of it finishes over the next frames instead of
        // in one. A customer who arrives facing the wrong way still gets there,
        // just visibly rather than instantly
        if (finalFacing.sqrMagnitude > .0001f)
            animator.TurnTo(finalFacing);
    }

    private void HandleIdleState()
    {
        if (navigationAbility.IsMoving)
        {
            StartWalkingState();
            return;
        }

        // A customer who never got a path never walks, so never reaches
        // StartIdleState, so would never open their bubble at all -- and the
        // failure would look like the bubble being broken rather than the
        // navigation. Standing still is having arrived, as far as an order goes
        if (wantsQueueFacing && !orderShown &&
            (animator == null || !animator.IsArrivalTurning))
            ShowOrder();
    }

    private void HandleWalkingState()
    {
        if (navigationAbility.HasReachedDestination)
        {
            ReachDestination();
            return;
        }

        // Begin the compact pivot just before the feet settle. Navigation
        // keeps covering the final few centimetres at its turn-slowed speed;
        // this avoids both the old long sideways drift and a turn that starts
        // only after the character has visibly stopped.
        if (wantsQueueFacing && !arrivalTurnRequested &&
            navigationAbility.IsWithinDestination(arrivalTurnWithin))
        {
            arrivalTurnRequested = true;
            FaceFinalFacing();
        }

        if (navigationAbility.IsMoving)
        {
            // Once the arrival pivot owns the body, do not feed the path
            // heading back into it. That would cancel the turn visually on
            // alternating frames. The agent still slows against the body's
            // actual forward and reaches the same destination.
            if (!animator.IsArrivalTurning)
                animator.ManageAnimations(navigationAbility.Velocity,
                    navigationAbility.Heading);

            // After the facing is handed over, so the slow-down answers to the
            // turn that was just asked for rather than to the last one
            navigationAbility.MatchSpeedTo(animator.Facing);
        }
        else
        {
            StartIdleState();
        }
    }

    private void StartWalkingState()
    {
        state = State.Walking;

        // Back into the negotiation. Somebody on the move has to be steered
        // around, including by the customer behind them in the queue
        navigationAbility.Standing(false);

        animator.StartWalking();
        UpdatePlateauVisibility();
    }

    private void StartIdleState()
    {
        // Read before the state changes, because the bounce belongs to the walk
        // ending. Idle to idle happens whenever anything re-checks a customer
        // who is already standing there, and a character who bounces every time
        // something asks how they are is a character with a twitch
        bool wasWalking = state == State.Walking;

        state = State.Idle;

        // Out of everyone else's way, in the only sense that costs nothing:
        // still standing exactly where they are, but no longer something the
        // next customer has to negotiate a path around
        navigationAbility.Standing(true);

        animator.Stop();

        // An early UpdatePlateauVisibility here would reveal the tray on the
        // first stopped frame, ahead of the still-running turn. ArrivalTurn
        // reveals it deliberately near the end of its own timeline.
        if (!animator.IsArrivalTurning)
            UpdatePlateauVisibility();

        // The brakes going on. Everyone who arrives gets it, including the ones
        // walking out -- stopping is stopping, and the door is as good a place
        // to land as the counter
        if (wasWalking)
            animator.Land();

        // Covers both ways a queued customer comes to rest: reaching their slot,
        // and giving up short of it because someone is standing in the way
        if (!wantsQueueFacing)
            return;

        FaceFinalFacing();

        // The order appears only after the empty-handed pivot has genuinely
        // finished and the waiting tray is back. Opening it here made the
        // customer serviceable while the turn clip was still running.
        if (animator == null || !animator.IsArrivalTurning)
            ShowOrder();
    }

    // Opened once, re-pinned every time after.
    //
    // Every stop after the first is the queue shuffling forward, and calling
    // Show again would restart the patience clock -- being moved up the line
    // would make a customer patient again, which is the opposite of what
    // standing in a queue does to anybody
    private void ShowOrder()
    {
        if (order == null)
            return;

        if (orderShown)
        {
            order.Pin();
            return;
        }

        orderShown = true;
        order.Show(lines);
    }

    private void ReachDestination()
    {
        StartIdleState();

        if (reachedDestinationCallback != null)
        {
            reachedDestinationCallback.Invoke();
            reachedDestinationCallback = null;
        }
    }
}
