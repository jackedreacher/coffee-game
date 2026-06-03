using UnityEngine;

[RequireComponent(typeof(NavigationAbility))]
public class Worker : MonoBehaviour
{
    private enum State { Idle = 0, PerformingTask = 1 }

    [Header(" Components ")]
    private NavigationAbility navigationAbility;
    private HoldFoodAbility holdFoodAbility;

    [Header(" Elements ")]
    [SerializeField] private CustomerAnimator animator;

    private State state;
    private WorkerTask currentTask;

    public bool IsIdle => state == State.Idle;
    public bool HasReachedDestination => navigationAbility.HasReachedDestination;
    public bool IsPlateauFull => holdFoodAbility.IsPlateauFull;
    public bool IsPlateauEmpty => holdFoodAbility.IsPlateauEmpty;
    public bool IsPlateauDirty => holdFoodAbility.IsPlateauDirty;

    private void Awake()
    {
        navigationAbility = GetComponent<NavigationAbility>();
        holdFoodAbility = GetComponent<HoldFoodAbility>();
        state = State.Idle;
    }

    private void Update()
    {
        HandleStateMachine();
    }

    private void HandleStateMachine()
    {
        currentTask?.Update();

        switch (state)
        {
            case State.Idle:
                HandleIdleState();
                break;
            case State.PerformingTask:
                HandlePerformingTaskState();
                break;
        }
    }

    private void HandleIdleState()
    {
        if (navigationAbility.IsMoving)
            StartWalkingState();
    }

    private void HandlePerformingTaskState()
    {
        if (HasReachedDestination)
        {
            ReachDestination();
            return;
        }

        if (navigationAbility.IsMoving)
            animator.ManageAnimations(navigationAbility.Velocity);
        else
            StartIdleState();
    }

    private void StartIdleState()
    {
        state = State.Idle;
        animator.Stop();
    }

    private void StartWalkingState()
    {
        state = State.PerformingTask;
        animator.StartWalking();
    }

    private void ReachDestination()
    {
        StartIdleState();
    }

    public void AssignTask(WorkerTask task)
    {
        currentTask = task;
        currentTask.Start();
        MarkAsBusy();
    }

    public void GoTo(Vector3 position)
    {
        navigationAbility.TryGoTo(position);
    }

    public void MarkAsBusy()
    {
        state = State.PerformingTask;
    }

    public void CompleteTask()
    {
        currentTask = null;
        StartIdleState();
    }
}
