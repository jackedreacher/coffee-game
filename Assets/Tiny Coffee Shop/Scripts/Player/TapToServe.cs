using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tap a customer to hand them the item the player is carrying. Works with the
// mouse in the editor and with touch on device, since Pointer covers both.
//
// Self contained on purpose: it does not care which kind of station spawned
// the customer, so it works next to CashierStation, PickupStation or nothing
[RequireComponent(typeof(HoldFoodAbility))]
[RequireComponent(typeof(CharacterStats))]
public class TapToServe : MonoBehaviour
{
    [Header(" Elements ")]
    // One per counter. The nearest customer across all of them wins
    [SerializeField] private FoodServingCustomerManager[] customerManagers;
    [SerializeField] private CashFile cashFile;

    // Where a satisfied customer walks off to. Leave empty to just remove them
    [SerializeField] private Transform customerExitPoint;
    [SerializeField] private Camera gameCamera;

    // UI that covers the screen but should not swallow a tap. The mobile
    // joystick zone is full-stretch by design, so IsPointerOverGameObject
    // reports every single tap as "on the HUD" without this
    [SerializeField] private RectTransform[] ignoredUI;

    [Header(" Settings ")]
    // How far from the tapped point we still count a customer as tapped.
    // Generous on purpose: touch targets are much bigger than a pixel
    [SerializeField] private float tapRadius = 1.5f;

    // How close the player has to stand before the food changes hands.
    // Tapping from across the room walks there first
    // Only used to decide where to walk when tapping from far away. Whether
    // the food actually changes hands is decided by the station trigger
    [SerializeField] private float serveRange = 3f;
    [SerializeField] private float maxRayDistance = 200f;
    [SerializeField] private int baseRevenue = 1;

    // Prints why a tap did nothing. Turn off once serving works
    [SerializeField] private bool logTaps = true;

    private HoldFoodAbility holdFoodAbility;
    private CharacterStats characterStats;

    // Optional. Present with click-to-move, absent with the joystick, where
    // the player walks over themselves and the tap just waits for them
    private NavMeshAgent agent;
    private JoystickPlayerController joystickController;

    private Customer pendingCustomer;

    // Serving spots the player is currently standing in. A customer can only
    // be handed food from behind their own counter
    private readonly HashSet<FoodServingCustomerManager> zonesInside =
        new HashSet<FoodServingCustomerManager>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out FoodServingCustomerManager zone))
            zonesInside.Add(zone);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out FoodServingCustomerManager zone))
            zonesInside.Remove(zone);
    }

    private bool IsInsideZoneOf(Customer customer)
    {
        foreach (FoodServingCustomerManager zone in zonesInside)
        {
            if (zone != null && zone.Contains(customer))
                return true;
        }

        return false;
    }

    private void Awake()
    {
        holdFoodAbility = GetComponent<HoldFoodAbility>();
        characterStats = GetComponent<CharacterStats>();
        agent = GetComponent<NavMeshAgent>();
        joystickController = GetComponent<JoystickPlayerController>();

        if (gameCamera == null)
            gameCamera = Camera.main;
    }

    private void Update()
    {
        HandleTap();
        HandlePendingCustomer();
    }

    // Serves once the player has actually walked into range
    private void HandlePendingCustomer()
    {
        if (pendingCustomer == null)
            return;

        // The station's own trigger is the rule: stand behind the counter and
        // the tap goes through, stand anywhere else and it waits
        if (!IsInsideZoneOf(pendingCustomer))
            return;

        Customer customer = pendingCustomer;
        pendingCustomer = null;

        TryServe(customer);
    }

    private void HandleTap()
    {
        if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame)
            return;

        // A tap on the HUD is not a serving order
        if (IsPointerOverBlockingUI(out string blocker))
        {
            Log("tap UI uzerinde: " + blocker);
            return;
        }

        if (gameCamera == null)
        {
            Log("gameCamera yok");
            return;
        }

        Ray ray = gameCamera.ScreenPointToRay(Pointer.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, ~0, QueryTriggerInteraction.Collide))
        {
            Log("raycast hicbir seye carpmadi");
            return;
        }

        Customer customer = FindCustomerFrom(hit);

        if (customer == null)
        {
            // Tapping the floor is a move order, so it cancels a pending serve
            pendingCustomer = null;
            Log("carpilan: " + hit.collider.name + " | musteri bulunamadi");
            return;
        }

        Log("musteri: " + customer.name +
            " | elde: " + (holdFoodAbility.PeekFood() == null ? "yok" : holdFoodAbility.PeekFood().GetType().Name) +
            " | servis alaninda: " + IsInsideZoneOf(customer));

        pendingCustomer = customer;
        WalkTo(customer);
    }

    // Stops short of the customer instead of walking into them
    private void WalkTo(Customer customer)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        // With the joystick driving, an agent destination fights the player's
        // own input: the stick pushes, the agent pulls back, nothing moves
        if (joystickController != null && joystickController.enabled)
            return;

        Vector3 fromCustomer = transform.position - customer.transform.position;
        fromCustomer.y = 0;

        if (fromCustomer.sqrMagnitude < .01f)
            return;

        // Stop at the edge of serving range, not on top of them. Walking into
        // the customer looks wrong and paths the player around the counter
        Vector3 target = customer.transform.position + fromCustomer.normalized * (serveRange * .95f);

        // Tight sample radius on purpose. A wide one snaps to the far side of
        // a counter and sends the player all the way around it. If there is no
        // navmesh right here, standing still beats taking the long way
        if (NavMesh.SamplePosition(target, out NavMeshHit navHit, .5f, NavMesh.AllAreas))
            agent.SetDestination(navHit.position);
    }

    // Does the UI raycast ourselves instead of IsPointerOverGameObject, which
    // cannot tell a real button from a full-screen input catcher
    public bool IsPointerOverBlockingUI(out string blocker)
    {
        blocker = null;

        if (EventSystem.current == null || Pointer.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Pointer.current.position.ReadValue()
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hit = results[i].gameObject;

            if (IsIgnored(hit.transform))
                continue;

            // Only real controls swallow the tap. Full-screen decoration and
            // organisational roots are raycast targets too, and blocking on
            // those means no tap ever reaches the game
            if (!IsInteractive(hit))
                continue;

            blocker = hit.name;
            return true;
        }

        return false;
    }

    private bool IsInteractive(GameObject candidate)
    {
        if (candidate.GetComponentInParent<Selectable>() != null)
            return true;

        return candidate.GetComponentInParent<IPointerClickHandler>() != null;
    }

    private bool IsIgnored(Transform hit)
    {
        for (int i = 0; i < ignoredUI.Length; i++)
        {
            if (ignoredUI[i] == null)
                continue;

            if (hit == ignoredUI[i] || hit.IsChildOf(ignoredUI[i]))
                return true;
        }

        return false;
    }

    private void Log(string message)
    {
        if (logTaps)
            Debug.Log("[TapToServe] " + message);
    }

    // A direct hit on the customer's own collider wins. The floor fallback
    // matters because in an isometric view the ray passes through a character's
    // body and lands well behind their feet
    public Customer FindCustomerFrom(RaycastHit hit)
    {
        Customer direct = hit.collider.GetComponentInParent<Customer>();

        return direct != null ? direct : FindCustomerNear(hit.point);
    }

    // Public so ClickToMovePlayerController can tell "tapped a customer" from
    // "tapped the floor" and skip walking there
    public Customer FindCustomerNear(Vector3 worldPoint)
    {
        Customer nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < customerManagers.Length; i++)
        {
            if (customerManagers[i] == null)
                continue;

            Customer candidate = customerManagers[i].FindNearestCustomer(worldPoint, tapRadius);

            if (candidate == null)
                continue;

            float distance = Vector3.Distance(
                candidate.transform.position.With(y: 0),
                worldPoint.With(y: 0));

            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    private bool TryServe(Customer customer)
    {
        if (!customer.NeedsMoreFood())
        {
            SendCustomerHome(customer);
            return true;
        }

        SpawnableFood heldFood = holdFoodAbility.PeekFood();

        if (heldFood == null)
        {
            Log("el bos, verilecek bir sey yok");
            return false;
        }

        // Refuse silently when it is not what they ordered, so a mistap does
        // not quietly burn the item the player is carrying. A customer with no
        // requested food takes anything, which is how the old stations work
        if (customer.RequestedFood != null &&
            heldFood.GetType() != customer.RequestedFood.GetType())
        {
            Log("istedigi " + customer.RequestedFood.GetType().Name + ", elde " + heldFood.GetType().Name);
            return false;
        }

        SpawnableFood foodToServe = holdFoodAbility.PopFood();

        if (foodToServe == null)
            return false;

        GenerateRevenue();
        customer.CollectFood(foodToServe);

        if (!customer.NeedsMoreFood())
            SendCustomerHome(customer);

        return true;
    }

    private void GenerateRevenue()
    {
        float revenueMultiplier = Mathf.Max(1f, characterStats.Revenue);
        int revenue = Mathf.CeilToInt(baseRevenue * revenueMultiplier);

        cashFile?.GenerateCash(revenue);
    }

    private void SendCustomerHome(Customer customer)
    {
        // Cheaper than tracking which counter owns them: Dequeue is a no-op
        // for a manager that never had this customer
        for (int i = 0; i < customerManagers.Length; i++)
            customerManagers[i]?.Dequeue(customer);

        if (customerExitPoint == null)
        {
            Destroy(customer.gameObject);
            return;
        }

        customer.GoToThen(customerExitPoint.position, () => Destroy(customer.gameObject));
    }
}
