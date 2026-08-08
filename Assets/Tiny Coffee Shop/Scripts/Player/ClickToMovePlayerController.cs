using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Tap-to-move alternative to JoystickPlayerController. Both are swappable
// because PlayerController leaves movement and IsMoving abstract, so save,
// stat upgrades and every station's IsMoving check keep working untouched.
// Only ONE PlayerController may be enabled on the player at a time
[RequireComponent(typeof(NavMeshAgent))]
public class ClickToMovePlayerController : PlayerController
{
    [Header(" Elements ")]
    [SerializeField] private Camera gameCamera;

    // Optional. When assigned, tapping a customer serves them instead of
    // walking to them. Leave empty and every tap is a move order
    [SerializeField] private TapToServe tapToServe;

    [Header(" Settings ")]
    // How far from the tapped point we are willing to look for walkable
    // ground. Tapping a counter should walk us to the floor beside it
    [SerializeField] private float navSampleRadius = 2f;
    [SerializeField] private float maxRayDistance = 200f;

    private NavMeshAgent agent;

    protected override void Start()
    {
        base.Start();

        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        // PlayerAnimator already faces the character along its move vector,
        // so letting the agent rotate too would fight it
        agent.updateRotation = false;

        if (gameCamera == null)
            gameCamera = Camera.main;
    }

    protected override void UpdateMovement()
    {
        HandleTap();

        // Drive the animator from where the agent is actually going, not from
        // an input vector. A stopped agent has zero velocity, which reads as idle
        playerAnimator.ManageAnimations(agent.velocity.normalized, agent.velocity.magnitude);
    }

    private void HandleTap()
    {
        // Pointer covers mouse in the editor and touch on device, no split needed
        if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame)
            return;

        // A tap that lands on the HUD is not a movement order
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (gameCamera == null)
            return;

        Ray ray = gameCamera.ScreenPointToRay(Pointer.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, ~0, QueryTriggerInteraction.Collide))
            return;

        // TapToServe already claimed this tap
        if (tapToServe != null && tapToServe.FindCustomerFrom(hit) != null)
            return;

        // Snapping to the NavMesh rather than filtering by layer means tapping
        // a counter, a customer or a wall still produces a sensible destination
        if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, navSampleRadius, NavMesh.AllAreas))
            return;

        agent.SetDestination(navHit.position);
    }

    public override bool IsMoving()
    {
        return agent.velocity.sqrMagnitude > 0.01f;
    }

    public override void SetMoveSpeed(float speed)
    {
        base.SetMoveSpeed(speed);

        // Speed upgrades land on moveSpeed at runtime; without this the agent
        // never hears about them and every speed upgrade does nothing
        if (agent != null)
            agent.speed = speed;
    }
}
