using System;
using UnityEngine;

public class Customer : MonoBehaviour
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

    [Header(" Order ")]
    [Tooltip("Kafasinin ustundeki siparis balonu. Bos birakilabilir")]
    [SerializeField] private CustomerOrder order;

    public OrderLine[] Lines => lines;

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

        plateau.gameObject.SetActive(!plateau.IsEmpty || state == State.Idle);
    }

    private void Update()
    {
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

        StartWalkingState();

        return true;
    }

    public void CollectFood(SpawnableFood food)
    {
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

    // Off without their food, and it costs a life. No callback and no reward:
    // this is the one exit that is not a sale
    public void GiveUp(Vector3 exitPosition)
    {
        if (order != null)
            order.Hide();

        wantsQueueFacing = false;

        EnableNavigation();

        // Off the floor one way or the other.
        //
        // A customer whose exit cannot be pathed to used to stay exactly where
        // they were, for the rest of the game: the counter had already taken
        // the life and dropped them from its list, so nothing was left that
        // would ever ask them to move again. Walking out is the nice version of
        // leaving; this is the other one
        if (!GoToThen(exitPosition, () => Destroy(gameObject)))
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

        EnableNavigation();

        // The caller is waiting on that callback to free the seat they were
        // sitting in. A walk that never starts never reaches its destination,
        // so it never calls back, and the seat stays taken by somebody who is
        // no longer using it
        if (!GoToThen(targetPosition, callback))
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
        if (wantsQueueFacing && !orderShown)
            ShowOrder();
    }

    private void HandleWalkingState()
    {
        if (navigationAbility.HasReachedDestination)
        {
            ReachDestination();
            return;
        }

        if (navigationAbility.IsMoving)
        {
            // The path steers the body for the whole walk except the last
            // stride, where it stops being worth listening to -- and where the
            // customer already knows which way they mean to end up standing.
            //
            // Handing the final facing over early is what turns two separate
            // events, arriving and then squaring up, into one movement
            bool squaringUp = wantsQueueFacing &&
                              finalFacing.sqrMagnitude > .0001f &&
                              navigationAbility.Arriving;

            animator.ManageAnimations(navigationAbility.Velocity,
                squaringUp ? finalFacing : navigationAbility.Heading);

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
