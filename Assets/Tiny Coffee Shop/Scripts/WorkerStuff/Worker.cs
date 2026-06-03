using UnityEngine;

[RequireComponent(typeof(NavigationAbility))]
public class Worker : MonoBehaviour
{
    private enum State { Idle = 0, PerformingTask = 1 }

    [Header(" Components ")]
    private NavigationAbility navigationAbility;
    private HoldFoodAbility holdFoodAbility;

    private State state;
    private WorkerTask currentTask;

    public bool HasReachedDestination => navigationAbility.HasReachedDestination;
    public bool IsPlateauFull => holdFoodAbility.IsPlateauFull;
    public bool IsPlateauEmpty => holdFoodAbility.IsPlateauEmpty;
    public bool IsPlateauDirty => holdFoodAbility.IsPlateauDirty;

    private void Awake()
    {
        navigationAbility = GetComponent<NavigationAbility>();
        holdFoodAbility = GetComponent<HoldFoodAbility>();
    }

    private void Update()
    {
        currentTask?.Update();
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
        Debug.Log("Worker task completed");
    }
}
