using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavigationAbility : MonoBehaviour
{
    [Header(" Components ")]
    private NavMeshAgent agent;

    public Vector3 Velocity => Ready ? agent.velocity : Vector3.zero;

    // Which way the PATH goes, as opposed to which way the agent is being
    // shoved.
    //
    // velocity is what local avoidance left behind after nudging the agent
    // aside for everyone else on the floor, and it changes direction several
    // times a second. Turning the body to face that is the wobble -- the
    // character swings towards every nudge and back again the whole way across
    // the kitchen. desiredVelocity is the same steering with the avoidance
    // taken out of it: where this agent would go if the floor were empty
    public Vector3 Heading
    {
        get
        {
            if (agent == null || !agent.isActiveAndEnabled)
                return Vector3.zero;

            Vector3 wanted = agent.desiredVelocity;

            wanted.y = 0f;

            if (wanted.sqrMagnitude > .01f)
                return wanted;

            // Zero on the frames between asking for a path and being given one.
            // The next corner is the same answer read a different way, and a
            // facing of nothing is no facing at all
            if (agent.hasPath)
            {
                Vector3 step = agent.steeringTarget - transform.position;

                step.y = 0f;

                if (step.sqrMagnitude > .0001f)
                    return step;
            }

            return agent.velocity;
        }
    }

    // Below this, the character is not walking -- it is being pushed.
    //
    // The old test was "velocity is not exactly zero", and a floor with other
    // agents on it is never exactly still. Local avoidance nudges a customer
    // who is already standing whenever anybody walks past, and the agent creeps
    // for a moment while it settles onto its spot. Both counted as movement, so
    // an arrived customer dropped back into the walk animation, stopped, and did
    // it again: running on the spot beside their own place, forever
    private const float stillSpeed = .15f;

    // Landing exactly on a point is a thing floating point arithmetic does not
    // do, and stoppingDistance is zero on these prefabs. A hand's width of
    // tolerance is the difference between arriving and being forever a few
    // millimetres short of arriving
    private const float arriveWithin = .1f;

    private bool Crawling =>
        !Ready || agent.velocity.sqrMagnitude <= stillSpeed * stillSpeed;

    private float StopWithin => Mathf.Max(agent.stoppingDistance, arriveWithin);

    // The last stretch, where the path has stopped being worth facing.
    //
    // Near the end the steering vector shrinks towards nothing and its
    // DIRECTION goes with it -- and once the agent slips a centimetre past the
    // point it flips outright. That is a body swinging round for no reason in
    // the last stride, and it is the last thing seen before the customer comes
    // to rest, so it is the thing that gets remembered.
    //
    // Long enough for the turn to be over before the feet stop.
    //
    // The first figure was 0.8, which is a stride -- and a stride at walking
    // pace is a seventh of a second, less time than a quarter turn takes. Half
    // of the turn was still happening after the customer had come to rest,
    // which is exactly the pivot-on-the-spot it was meant to replace
    private const float arrivingWithin = 1.5f;

    public bool Arriving
    {
        get
        {
            if (agent == null || !agent.isActiveAndEnabled || agent.pathPending)
                return false;

            return agent.hasPath && agent.remainingDistance <= arrivingWithin;
        }
    }

    // Safe to ask anything of. Every NavMeshAgent property below throws the
    // same "must be on a NavMesh" complaint otherwise, and it throws it once
    // per frame for as long as whoever is asking keeps asking.
    public bool Ready => agent != null && agent.enabled && agent.isOnNavMesh;

    public bool IsMoving
    {
        get
        {
            // Same reason as HasReachedDestination: an agent that is switched
            // off is not moving, and saying so is cheaper than throwing.
            if (!Ready)
                return false;

            if (agent.pathPending)
                return true;

            if (agent.hasPath && agent.remainingDistance > StopWithin)
                return true;

            return !Crawling;
        }
    }

    public bool HasReachedDestination
    {
        get
        {
            // A disabled or off-mesh agent answers every one of the questions
            // below with an error rather than a value. "Nowhere left to walk"
            // is the honest answer for an agent that cannot walk at all, and it
            // is the one that stops the caller asking again.
            if (!Ready)
                return true;

            if (agent.pathPending)
                return false;

            if (agent.remainingDistance > StopWithin)
                return false;

            if (agent.hasPath && !Crawling)
                return false;

            return true;
        }
    }

    // The arrival performance starts in the last hand-width of the path, not
    // after the agent has already become completely still. Kept as a query on
    // NavigationAbility so Customer never reaches into NavMeshAgent internals.
    public bool IsWithinDestination(float distance)
    {
        if (agent == null || !agent.isActiveAndEnabled || agent.pathPending ||
            !agent.hasPath)
            return false;

        return agent.remainingDistance <= Mathf.Max(StopWithin, distance);
    }

    // Every agent gets its own place in the pecking order.
    //
    // Local avoidance is symmetric when two agents share a priority: both step
    // aside, each sees the other step aside, both correct, and the result is two
    // characters swaying around one another the whole way across the floor. All
    // the customer prefabs ship at 50, so every customer in the queue was tied
    // with every other one. Different numbers break the tie -- one gives way,
    // the other walks straight
    private static int spread;

    // This agent's own number while it is going somewhere. Remembered, because
    // standing still borrows a different one and arriving has to give it back
    private int walkingPriority;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        fullSpeed = agent.speed;

        // Around the middle of the range, leaving room above for anything that
        // must be got out of the way of and below for anything that never is
        walkingPriority = 40 + spread;

        agent.avoidancePriority = walkingPriority;

        spread = (spread + 1) % 21;
    }

    // A stopped character is furniture, and should be treated as such.
    //
    // Somebody who has reached their spot is not going anywhere, and the one
    // still walking has to get past them. While both are in the avoidance
    // solver the walker negotiates with every body already standing in the row
    // -- so the customer heading for the far end weaves past two people and the
    // one heading for the near end does not, which is exactly the difference
    // that shows on screen.
    //
    // Unity reads a HIGHER number as a LOWER priority, and an agent ignores
    // everything below itself. Parking a stopped customer at the bottom takes
    // them out of the walker's reasoning without moving them an inch
    public void Standing(bool still)
    {
        if (agent == null)
            return;

        agent.avoidancePriority = still ? 99 : walkingPriority;
    }

    public void Disable()
    {
        agent.enabled = false;
    }

    public void Enable()
    {
        // Warp agent to nearest NavMesh point before enabling
        // Prevents "agent not on NavMesh" errors after sitting off-mesh
        // if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
        //     agent.Warp(hit.position);

        agent.enabled = true;
    }

    // What the agent runs at when it is pointing where it is going. Held apart
    // from agent.speed, because that one is overwritten every frame now
    private float fullSpeed;

    // What it is set to now, so a caller can scale it instead of having to
    // know the number somebody typed into the prefab.
    public float Speed => fullSpeed;

    public void SetSpeed(float speed)
    {
        fullSpeed = speed;
        agent.speed = speed;
    }

    // What is left of the speed when the body is facing entirely the wrong way.
    //
    // Not zero. An agent that stops dead cannot turn INTO anything -- it turns
    // on the spot, and a character pivoting on the spot is the same complaint
    // wearing a different coat. This is a walking pace, so the turn is taken
    // while still moving, just slowly enough that the feet keep up
    private const float turningSpeed = .3f;

    // Only as fast as the body is pointing the right way.
    //
    // Speed and facing were independent, and that independence IS the slide:
    // steering can change direction inside a single frame and a character
    // cannot, so for a fifth of a second the feet carry on one way while the
    // body swings round to another. Tied together, that becomes a slow-down --
    // ease off, come round, pick the pace back up, which is what anybody
    // walking into a queue actually does
    public void MatchSpeedTo(Vector3 bodyForward)
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return;

        // Zero before anything set it, which would stop the customer dead
        if (fullSpeed <= .01f)
            fullSpeed = agent.speed;

        Vector3 going = Heading;

        going.y = 0f;
        bodyForward.y = 0f;

        if (going.sqrMagnitude < .0001f || bodyForward.sqrMagnitude < .0001f)
            return;

        float alignment = Mathf.Clamp01(
            Vector3.Dot(bodyForward.normalized, going.normalized));

        agent.speed = fullSpeed * Mathf.Lerp(turningSpeed, 1f, alignment);
    }

    public bool TryGoTo(Vector3 targetPosition)
    {
        targetPosition.y = 0;

        NavMeshPath path = new NavMeshPath();

        if (agent.CalculatePath(targetPosition, path))
        {
            agent.SetPath(path);
            agent.isStopped = false;

            return true;
        }

        // The point itself is not on the floor, or there is no route to the
        // piece of floor it is on.
        //
        // The nearest walkable spot to it is the same intention -- go over
        // there -- and it is what SetDestination would have settled for on its
        // own. Worth one try before giving up, because the caller giving up
        // means a customer who cannot leave: the exit point is authored by
        // hand, it only has to drift a little off the mesh, and the customer
        // sent to it then stands in the queue for the rest of the game
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit near, retryRadius,
                NavMesh.AllAreas) &&
            agent.CalculatePath(near.position, path))
        {
            agent.SetPath(path);
            agent.isStopped = false;

            return true;
        }

        // Says which of the two it was. "Cannot reach" covers a point off the
        // mesh and a point walled off from here, and those are fixed in
        // completely different places
        bool onMesh = NavMesh.SamplePosition(targetPosition, out NavMeshHit _, retryRadius,
            NavMesh.AllAreas);

        Debug.LogError(name + ": " + targetPosition.ToString("0.0") + " noktasina gidemiyor -- " +
                       (onMesh
                           ? "orasi zeminde ama buradan yol yok"
                           : "orasi zeminin disinda (" + retryRadius.ToString("0.0") +
                             " birim icinde yurunebilir yer yok)"), this);

        return false;
    }

    // The direction of the FIRST walkable leg, not the straight line to the
    // destination. Around a counter those are often different: the door may be
    // behind the customer while the legal path begins sideways around a wall.
    // Turning to the straight line and then feeding Animator the NavMesh line
    // made the body visibly look back immediately after its 180 turn.
    public Vector3 FirstHeadingTo(Vector3 targetPosition)
    {
        targetPosition.y = 0f;

        Vector3 direct = targetPosition - transform.position;
        direct.y = 0f;

        if (agent == null || !agent.isActiveAndEnabled)
            return direct;

        NavMeshPath path = new NavMeshPath();
        bool found = agent.CalculatePath(targetPosition, path);

        if (!found && NavMesh.SamplePosition(targetPosition, out NavMeshHit near,
                retryRadius, NavMesh.AllAreas))
        {
            found = agent.CalculatePath(near.position, path);
        }

        if (!found || path.corners == null)
            return direct;

        for (int i = 0; i < path.corners.Length; i++)
        {
            Vector3 step = path.corners[i] - transform.position;
            step.y = 0f;

            // corner[0] is normally the agent's own position. Skip it and any
            // numerical duplicate until the first real leg appears.
            if (step.sqrMagnitude > .01f)
                return step;
        }

        return direct;
    }

    // How far to look for standable floor around a destination that missed.
    // Wide enough to forgive a point nudged off the edge in the scene view,
    // narrow enough that it cannot land the customer in another room
    private const float retryRadius = 4f;
}
