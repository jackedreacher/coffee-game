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
    private int foodNeededCount;
    private int foodTakenCount;

    [Header(" Actions ")]
    private Action reachedDestinationCallback;

    [Header(" Order ")]
    // What this customer walked in for. Null means "whatever the counter
    // serves", which is how the coffee shop scene has always worked
    private SpawnableFood requestedFood;

    [Tooltip("Kafasinin ustundeki siparis balonu. Bos birakilabilir")]
    [SerializeField] private CustomerOrder order;

    public int FoodNeededCount => foodNeededCount;
    public int FoodTakenCount => foodTakenCount;
    public SpawnableFood RequestedFood => requestedFood;

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

    public void Initialize(int foodNeededCount, Vector3 targetPosition, Vector3 finalFacing)
    {
        this.foodNeededCount = foodNeededCount;
        this.finalFacing = finalFacing;

        // Not shown here. The bubble is nailed to a fixed spot once it opens,
        // and a spot picked while they are still crossing the floor is a spot
        // they were walking through -- the card would end up hanging over the
        // doorway. It opens when they come to rest instead
        orderShown = false;

        GoTo(targetPosition);
    }

    // Overload for counters that sell more than one thing. The old signature
    // stays so the existing coffee shop scene keeps working untouched
    public void Initialize(int foodNeededCount, Vector3 targetPosition, Vector3 finalFacing, SpawnableFood requestedFood)
    {
        this.requestedFood = requestedFood;
        Initialize(foodNeededCount, targetPosition, finalFacing);
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
    // are the moves where the queue facing is exactly what is not wanted
    public void GoToThen(Vector3 targetPosition, Action callback)
    {
        reachedDestinationCallback = callback;

        if (navigationAbility.TryGoTo(targetPosition))
            StartWalkingState();
    }

    public void CollectFood(SpawnableFood food)
    {
        plateau.gameObject.SetActive(true);
        plateau.Push(food);
        foodTakenCount++;

        if (order == null)
            return;

        // Settled on the last item, and settled BEFORE the reward is worked
        // out. Reading the live clock later would score whatever it had drained
        // to by then rather than what the player was looking at when they served
        if (NeedsMoreFood())
            order.SetCount(foodNeededCount - foodTakenCount);
        else
            order.Settle();
    }

    // Called by whoever rang it up, after the last item changed hands. Split
    // from CollectFood because the two halves are known in different places:
    // the bubble works out the mood, and only the till knows the money
    public void ShowEarnings(int amount)
    {
        if (order == null || NeedsMoreFood())
            return;

        order.Celebrate(amount);
    }

    public bool NeedsMoreFood()
    {
        return foodTakenCount < foodNeededCount;
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
        GoToThen(targetPosition, callback);
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
        if (finalFacing.sqrMagnitude > .0001f)
            animator.Face(finalFacing);
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
            animator.ManageAnimations(navigationAbility.Velocity);
        }
        else
        {
            StartIdleState();
        }
    }

    private void StartWalkingState()
    {
        state = State.Walking;
        animator.StartWalking();
        UpdatePlateauVisibility();
    }

    private void StartIdleState()
    {
        state = State.Idle;
        animator.Stop();
        UpdatePlateauVisibility();

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
        order.Show(requestedFood, foodNeededCount);
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
