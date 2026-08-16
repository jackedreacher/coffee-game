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

    [Tooltip("Move Speed sifir kalmissa bunun yerine bu kullanilir")]
    [SerializeField] private float fallbackMoveSpeed = 5f;

    // Off by default, and that is the setting that makes a speed number mean
    // anything in a room this size.
    //
    // A braking agent starts slowing the moment the destination is within
    // stopping range, and in a kitchen the destination is ALWAYS within that
    // range -- so the whole trip is one long deceleration and the top speed is
    // never reached. Raising speed from 20 to 40 changed nothing on screen for
    // exactly this reason. Switching it back on trades the pace for a softer
    // arrival
    [Tooltip("Hedefe yaklasirken yavaslasin mi. KAPALI = tam hizda gidip aninda durur")]
    [SerializeField] private bool brakeOnArrival;

    private NavMeshAgent agent;

    protected override void Start()
    {
        base.Start();

        agent = GetComponent<NavMeshAgent>();

        // Left switched off by the joystick setup, which had no use for it. This
        // controller cannot do anything at all without it, so it is switched on
        // here rather than left as a checkbox nobody knows to tick
        if (!agent.enabled)
        {
            Debug.LogWarning(name + ": NavMeshAgent kapaliydi, acildi.", this);

            agent.enabled = true;
        }

        // An agent switched on away from the mesh comes up unusable and stays
        // that way. Warping it onto the nearest point is what the commented out
        // block in NavigationAbility.Enable was reaching for
        if (agent.enabled && !agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit onMesh, 3f, NavMesh.AllAreas))
        {
            agent.Warp(onMesh.position);

            Debug.LogWarning(name + ": NavMesh disindaydi, en yakin noktaya tasindi.", this);
        }

        // A component added at edit time serializes moveSpeed at zero. Handing
        // that straight to the agent produces a player who accepts every tap,
        // reports every destination and never moves -- so it is caught here
        // rather than left to look like a broken navmesh
        if (moveSpeed <= .01f)
        {
            Debug.LogWarning(name + ": Move Speed 0 idi, " + fallbackMoveSpeed +
                             " kullanildi.\nClick To Move Player Controller > Move Speed'i ayarla.",
                             this);

            moveSpeed = fallbackMoveSpeed;
        }

        ApplySpeed(moveSpeed);

        // PlayerAnimator already faces the character along its move vector,
        // so letting the agent rotate too would fight it
        agent.updateRotation = false;

        if (gameCamera == null)
            gameCamera = Camera.main;
    }

    protected override void UpdateMovement()
    {
        // TapToServe owns every tap when it is present. It walks to stand
        // points, to customers and to bare floor, and a second handler setting
        // its own destination on the same tap would overwrite whichever of the
        // two happened to run first -- which is script execution order, not a
        // decision anyone made
        if (tapToServe == null)
            HandleTap();

        // Move Speed dragged in the inspector while the game runs lands on the
        // agent immediately.
        //
        // Speed is a feel number and there is no way to reason it out from
        // outside the game -- it was set from an asset at Start, so finding the
        // right one meant stop, edit, play, judge, repeat. PlayerStatsHandler
        // still wins at startup; this only tracks changes made after that
        if (!Mathf.Approximately(agent.speed, moveSpeed))
            ApplySpeed(moveSpeed);

        // Drive the animator from where the agent is actually going, not from
        // an input vector. A stopped agent has zero velocity, which reads as idle
        playerAnimator.ManageAnimations(agent.velocity.normalized, agent.velocity.magnitude);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ReportSpeed();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Which of the two numbers actually won.
    //
    // Move Speed is serialised on this component and PlayerStatsHandler writes
    // over it from the stats asset in Start -- but only if it is wired, and if
    // it is not, the field stands. Both are plausible, neither is visible, and
    // editing the wrong one looks exactly like editing nothing
    private float reportedSpeed = -1f;
    private float peakVelocity;

    private void ReportSpeed()
    {
        if (agent == null)
            return;

        if (!Mathf.Approximately(agent.speed, reportedSpeed))
        {
            bool first = reportedSpeed < 0f;

            reportedSpeed = agent.speed;
            peakVelocity = 0f;

            // Everything that throttles an agent, in one line. Speed alone was
            // reported for two rounds and speed alone was never the limit
            Debug.Log("[Hiz] agent.speed = " + agent.speed.ToString("0.0") +
                      "   ivme " + agent.acceleration.ToString("0.0") +
                      "   (Move Speed alani " + moveSpeed.ToString("0.0") + ")" +
                      "\n  autoBraking      : " + (agent.autoBraking
                          ? "ACIK  <-- kisa mesafede tavana hic cikilamaz"
                          : "kapali") +
                      "\n  stoppingDistance : " + agent.stoppingDistance.ToString("0.00") +
                      "\n  radius / height  : " + agent.radius.ToString("0.00") +
                      " / " + agent.height.ToString("0.00") +
                      (first
                          ? "\n  ilk deger"
                          : "\n  DEGISTI -- birisi SetMoveSpeed cagirdi, " +
                            "muhtemelen PlayerStatsHandler ya da yukseltme masasi"),
                      this);
        }

        // The ceiling and the speed actually reached are different numbers, and
        // it is the second one the walk animation is scaled against. Guessing
        // which it was is what produced a run cycle in slow motion
        float now = agent.velocity.magnitude;

        if (now <= peakVelocity * 1.1f + .5f)
            return;

        peakVelocity = now;

        Debug.Log("[Hiz] ulasilan en yuksek hiz: " + now.ToString("0.0") +
                  "  (tavan " + agent.speed.ToString("0.0") + ")" +
                  "\n  Player Animator > Walk Speed Reference'i bu sayiya yakin tut:" +
                  "\n  esit yaparsan bacaklar tam tempoda, dusurursen hizlanir",
                  this);
    }
#endif

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
            ApplySpeed(speed);
    }

    // Acceleration travels with the speed, and it has to.
    //
    // A NavMeshAgent left on the default 8 needs five seconds of straight
    // running to reach 40, and a kitchen has no straight line that long -- so
    // raising the speed did nothing but raise a ceiling the player never
    // touched. It showed up as the run animation playing in slow motion,
    // because the animation is driven by how fast the character IS going, and
    // that was still about ten
    private void ApplySpeed(float speed)
    {
        agent.speed = speed;

        // Half a second from a standstill to full speed at any setting
        agent.acceleration = Mathf.Max(8f, speed * 2f);

        agent.autoBraking = brakeOnArrival;
    }
}
