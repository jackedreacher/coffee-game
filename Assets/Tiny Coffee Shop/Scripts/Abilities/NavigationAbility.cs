using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavigationAbility : MonoBehaviour
{
    [Header(" Components ")]
    private NavMeshAgent agent;

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
