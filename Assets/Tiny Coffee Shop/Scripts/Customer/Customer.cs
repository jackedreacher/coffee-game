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

    private void StartWalkingState()
    {
        state = State.Walking;
        animator.StartWalking();
    }
}
