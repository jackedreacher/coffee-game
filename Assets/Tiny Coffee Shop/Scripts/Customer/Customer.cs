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

    [Header(" State ")]
    private State state;

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

    public void Initialize(Vector3 targetPosition)
    {
        GoTo(targetPosition);
    }

    private void GoTo(Vector3 targetPosition)
    {
        bool canReachDestination = navigationAbility.TryGoTo(targetPosition);

        if (canReachDestination)
            StartWalkingState();
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
    }
}
