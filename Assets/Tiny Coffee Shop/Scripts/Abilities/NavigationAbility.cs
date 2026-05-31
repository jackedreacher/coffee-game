using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavigationAbility : MonoBehaviour
{
    [Header(" Components ")]
    private NavMeshAgent agent;

    public Vector3 Velocity => agent.velocity;

    public bool IsMoving
    {
        get
        {
            if (agent.pathPending)
                return true;

            if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance)
                return true;

            return agent.velocity.sqrMagnitude > 0;
        }
    }

    public bool HasReachedDestination
    {
        get
        {
            if (agent.pathPending)
                return false;

            if (agent.remainingDistance > agent.stoppingDistance)
                return false;

            if (agent.hasPath && agent.velocity.sqrMagnitude != 0)
                return false;

            return true;
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public bool TryGoTo(Vector3 targetPosition)
    {
        targetPosition.y = 0;

        NavMeshPath path = new NavMeshPath();
        bool reachable = agent.CalculatePath(targetPosition, path);

        if (!reachable)
        {
            Debug.LogError(name + " cannot reach destination " + targetPosition);
            return false;
        }

        agent.SetPath(path);
        agent.isStopped = false;

        return true;
    }
}
