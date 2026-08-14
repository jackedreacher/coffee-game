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
    private int foodNeededCount;
    private int foodTakenCount;

    [Header(" Actions ")]
    private Action reachedDestinationCallback;

    [Header(" Order ")]
    // What this customer walked in for. Null means "whatever the counter
    // serves", which is how the coffee shop scene has always worked
    private SpawnableFood requestedFood;

    public int FoodNeededCount => foodNeededCount;
    public int FoodTakenCount => foodTakenCount;
    public SpawnableFood RequestedFood => requestedFood;

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
            StartWalkingState();
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
        if (wantsQueueFacing)
            FaceFinalFacing();
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
