using UnityEngine;

[RequireComponent(typeof(NavigationAbility))]
[RequireComponent(typeof(CharacterStats))]
public class Worker : MonoBehaviour
{
    private enum State { Idle = 0, PerformingTask = 1 }

    [Header(" Components ")]
    private NavigationAbility navigationAbility;
    private HoldFoodAbility holdFoodAbility;
    private CharacterStats characterStats;

    [Header(" Elements ")]
    [SerializeField] private CustomerAnimator animator;
    private WorkerManager workerManager;

    [Header(" Settings ")]
    private int level;
    private float revenueMultiplier;

    private State state;
    private WorkerTask currentTask;

    public bool IsIdle => state == State.Idle;
    public bool CanCancelTask => currentTask is IdleTask || !holdFoodAbility.IsPlateauActive;
    public WorkerTask CurrentTask => currentTask;
    public bool HasReachedDestination => navigationAbility.HasReachedDestination;
    public bool IsPlateauFull => holdFoodAbility.IsPlateauFull;
    public bool IsPlateauEmpty => holdFoodAbility.IsPlateauEmpty;
    public bool IsPlateauDirty => holdFoodAbility.IsPlateauDirty;

    private void Awake()
    {
        navigationAbility = GetComponent<NavigationAbility>();
        holdFoodAbility = GetComponent<HoldFoodAbility>();
        characterStats = GetComponent<CharacterStats>();
        state = State.Idle;
    }

    public void Initialize(WorkerManager workerManager, int workerLevel)
    {
        this.workerManager = workerManager;
        level = workerLevel;

        SetupStats();
    }

    public void LevelUp()
    {
        level++;

        // Re-run the whole setup so the new level reaches the agent and the plateau
        SetupStats();
    }

    private void SetupStats()
    {
        Vector3Int levels = WorkerUtilities.GetStatsLevelsFromLevel(level);

        characterStats.SetupStats(levels);

        navigationAbility.SetSpeed(characterStats.Speed);
        holdFoodAbility.SetPlateauCapacity(characterStats.Capacity);
        revenueMultiplier = characterStats.Revenue;
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
            animator.ManageAnimations(navigationAbility.Velocity, navigationAbility.Heading);
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

    public void CancelTask()
    {
        currentTask.Cancel();
        CompleteTask();
    }

    public void CompleteTask()
    {
        currentTask = null;
        StartIdleState();
    }
}
