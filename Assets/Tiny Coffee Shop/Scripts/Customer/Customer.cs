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
    private Vector3 finalFacing;
    private int foodNeededCount;
    private int foodTakenCount;

    [Header(" Actions ")]
    private Action reachedDestinationCallback;

    public int FoodNeededCount => foodNeededCount;
    public int FoodTakenCount => foodTakenCount;

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
        GoToThen(targetPosition, FaceFinalFacing);
    }

    private void GoTo(Vector3 targetPosition)
    {
        bool canReachDestination = navigationAbility.TryGoTo(targetPosition);

        if (canReachDestination)
            StartWalkingState();
    }

    private void GoToThen(Vector3 targetPosition, Action callback)
    {
        reachedDestinationCallback = callback;
        GoTo(targetPosition);
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

    private void FaceFinalFacing()
    {
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
    }

    private void StartIdleState()
    {
        state = State.Idle;
        animator.Stop();
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
