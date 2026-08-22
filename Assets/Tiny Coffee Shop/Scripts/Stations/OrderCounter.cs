using System.Collections.Generic;
using UnityEngine;

// A counter that sells several different foods at once. Each drop zone holds
// one food type; a customer asks for one of them by name and is only served
// from the matching zone.
//
// Deliberately NOT a FoodServingStation subclass: that hierarchy is built
// around a single drop zone and drags the worker task system (TaskRequester,
// GuidGenerator, fill/serve requests) along with it. This scene has no workers,
// so the ~30 lines of shared trigger code are cheaper than the coupling
public class OrderCounter : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private FoodServingCustomerManager customerManager;
    [SerializeField] private FoodDropZone[] dropZones;
    [SerializeField] private CashFile cashFile;
    [SerializeField] private Transform customerExitPoint;

    [Header(" Settings ")]
    [SerializeField] private float servingDelay = .3f;
    [SerializeField] private int baseRevenue = 1;

    private float servingTimer;
    private int serverCount;

    private void Awake()
    {
        // Tell the queue what this counter can actually serve, so a customer
        // can never walk in wanting something that is impossible to hand over
        customerManager.SetPossibleOrders(GetSellableFoods());
    }

    // Only the zones that actually sell something. One row per drop zone was
    // the obvious shape and the wrong one: a zone whose accepted food is not
    // set yet contributed a null, the queue handed that null to a customer as
    // their order, and the bubble opened over their head with nothing in it
    private SpawnableFood[] GetSellableFoods()
    {
        List<SpawnableFood> foods = new List<SpawnableFood>();

        for (int i = 0; i < dropZones.Length; i++)
        {
            if (dropZones[i] == null || dropZones[i].AcceptedFood == null)
                continue;

            foods.Add(dropZones[i].AcceptedFood);
        }

        return foods.ToArray();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerDetector _) && !other.TryGetComponent(out Worker _))
            return;

        serverCount++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out PlayerDetector _) && !other.TryGetComponent(out Worker _))
            return;

        serverCount--;
        serverCount = Mathf.Max(0, serverCount);
    }

    private void OnTriggerStay(Collider other)
    {
        // Whoever is behind the counter decides the payout, same rule as
        // FoodServingStation: no stats, no serving
        if (!other.TryGetComponent(out CharacterStats characterStats))
            return;

        if (serverCount > 0)
            HandleServing(characterStats);
    }

    private void HandleServing(CharacterStats characterStats)
    {
        if (servingTimer < servingDelay)
        {
            servingTimer += Time.deltaTime;
            return;
        }

        // No serving order: walk the whole front row and serve the first
        // customer whose order we actually have in stock. Someone waiting on
        // pizza we ran out of does not block the person next to them who
        // wanted a drink
        for (int i = 0; i < customerManager.ServableSlotCount; i++)
        {
            Customer customer = customerManager.GetArrivedCustomer(i);

            if (customer == null)
                continue;

            if (!customer.NeedsMoreFood())
            {
                servingTimer = 0;
                SendCustomerHome(customer);
                return;
            }

            FoodDropZone zone = GetZoneFor(customer.RequestedFood);

            if (zone == null || zone.GetFirstFullPosition() == null)
                continue;

            servingTimer = 0;
            Serve(customer, zone, characterStats);
            return;
        }
    }

    private FoodDropZone GetZoneFor(SpawnableFood requestedFood)
    {
        for (int i = 0; i < dropZones.Length; i++)
        {
            if (dropZones[i].CanAcceptFood(requestedFood))
                return dropZones[i];
        }

        return null;
    }

    // Hand-serving: the player carries one item and taps the customer they
    // want to give it to. No counter, no queue order — TapToServe calls this
    public bool TryServeByHand(Customer customer, HoldFoodAbility holdFoodAbility, CharacterStats characterStats)
    {
        if (customer == null || holdFoodAbility == null)
            return false;

        if (!customer.NeedsMoreFood())
        {
            SendCustomerHome(customer);
            return true;
        }

        SpawnableFood heldFood = holdFoodAbility.PeekFood();

        if (heldFood == null)
            return false;

        // Raw food never reaches a customer, whatever they ordered
        if (!heldFood.CanBeServed)
            return false;

        // Refuse silently when it is not what they ordered, so a mistap does
        // not quietly burn the item the player is carrying
        // An order can name two things now, so this asks whether the held food
        // is wanted rather than whether it matches the one requested food
        if (customer.RequestedFood != null && !customer.Wants(heldFood))
            return false;

        SpawnableFood foodToServe = holdFoodAbility.PopFood();

        if (foodToServe == null)
            return false;

        customer.CollectFood(foodToServe);
        Pay(customer, characterStats);

        if (!customer.NeedsMoreFood())
            SendCustomerHome(customer);

        return true;
    }

    // Collect first, pay second. CollectFood is what stops the patience clock,
    // and a multiplier read before it stops is read off a clock still running.
    //
    // The mood is new here. This path paid a flat rate whoever was kept waiting,
    // so the whole patience system only counted when the player served by hand.
    //
    // Rung up per item, paid once: RingUp answers 0 until the order is done, so
    // the money only leaves the customer at the moment the bubble says what the
    // order was worth -- and it is the same number, added up rather than the
    // last item's share
    private void Pay(Customer customer, CharacterStats characterStats)
    {
        float revenueMultiplier = Mathf.Max(1f, characterStats.Revenue);

        int revenue = Mathf.CeilToInt(
            baseRevenue * revenueMultiplier * Mathf.Max(.05f, customer.RewardMultiplier));

        int due = customer.RingUp(revenue);

        if (due <= 0)
            return;

        // Off the customer, because paying is something the customer does
        cashFile?.GenerateCash(due, customer.transform.position);
    }

    private void Serve(Customer customer, FoodDropZone zone, CharacterStats characterStats)
    {
        // Only reached once the zone has been checked for stock, so this pops
        // something. Moved above the till so the clock is stopped first
        SpawnableFood foodToServe = zone.Pop();

        customer.CollectFood(foodToServe);
        Pay(customer, characterStats);

        if (customer.NeedsMoreFood())
            return;

        SendCustomerHome(customer);
    }

    private void SendCustomerHome(Customer customer)
    {
        customerManager.Dequeue(customer);

        // Gone either way. They are already out of the queue by this line, so
        // nothing will ever ask them to move again -- a walk that fails to
        // start leaves them standing in the shop for the rest of the game
        if (!customer.Leave(customerExitPoint.position, () => Destroy(customer.gameObject)))
            Destroy(customer.gameObject);
    }
}
