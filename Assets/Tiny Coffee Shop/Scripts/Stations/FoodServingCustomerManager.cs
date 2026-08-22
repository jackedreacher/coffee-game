using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class FoodServingCustomerManager : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform queueStartPoint;

    [Header(" Settings ")]
    // Written closed-is-the-new-way: this field is new on components already
    // saved in the scene, and a field the file does not have comes back false
    [Tooltip("Isaretliyse bu tezgah hic musteri uretmez. " +
             "Oyuncunun servis edemedigi tezgahlar sadece can goturur")]
    [SerializeField] private bool closed;

    [SerializeField] private int maxCustomers;

    public bool Closed => closed;

    // Offset from one ROW of customers to the next, away from the counter
    [SerializeField] private Vector3 queueSpacing;

    // How many customers stand shoulder to shoulder before a new row starts.
    // 1 keeps the classic single file, which is what the coffee shop uses
    [SerializeField][Min(1)] private int customersPerRow = 1;

    // Offset between two neighbours inside the same row
    [SerializeField] private Vector3 sideSpacing = new Vector3(0f, 0f, 1f);

    [Tooltip("Butun yan yana sirayi Side Spacing ekseninde kaydirir. " +
             "Sol dis hedef NavMesh disina cikiyorsa negatif yap")]
    [SerializeField] private float sideCentreOffset;

    [Tooltip("4 kisi yan yanayken siparis balonu boyutu. 0 = varsayilan 0.68")]
    [SerializeField] private float fourWideBubbleScale = .68f;

    [Tooltip("Arkadaki siranin siparis balonu ne kadar yukarida dursun. " +
             "0 = varsayilan 1.1. Balonlar ust uste biniyorsa buyut")]
    [SerializeField] private float bubbleRowLift = 1.1f;

    private float BubbleRowLift => bubbleRowLift > .01f ? bubbleRowLift : 1.1f;
    private float FourWideBubbleScale =>
        fourWideBubbleScale > .01f ? Mathf.Clamp(fourWideBubbleScale, .5f, 1f) : .68f;

    [SerializeField] private Vector2Int minMaxCustomerFoodCount;

    [Tooltip("Ne istedigini bilmeyen musteri yuzdesi -- ne verirsen alir. " +
             "0 = varsayilan 12, eksi bir sayi = hic gelmesin")]
    [SerializeField] private float undecidedChance;

    // Zero has to mean the default here, not "never".
    //
    // The field is new on counters already saved in the scene, and a field the
    // scene file does not carry comes back zero -- so reading zero as "off"
    // would ship the whole thing switched off and say nothing about it. Below
    // zero is how it actually gets turned off, which is a thing somebody has to
    // type on purpose
    private float UndecidedChance =>
        undecidedChance < -.01f ? 0f : undecidedChance > .01f ? undecidedChance : 12f;

    // Was one second, hard coded, for both. Three slots filled in three seconds
    // and a freed one refilled before the player had turned around -- the queue
    // was always full, which is the same as there being no queue to manage
    [Tooltip("Ilk musteri kac saniye sonra gelir. 0 = varsayilan 2")]
    [SerializeField] private float firstCustomerDelay = 2f;

    [Tooltip("Iki musteri arasi sure, saniye. 0 = varsayilan 4")]
    [SerializeField] private float customerInterval = 4f;

    // The interval is a pace, not a promise to keep the player waiting.
    //
    // Round 1 is fifteen seconds between arrivals. Serve the one customer there
    // and the kitchen is empty for the rest of it -- which is not an easy round,
    // it is an empty one. So the clock is cut short whenever the queue thins
    // out, and in the later rounds it never does, because the queue is full
    // One, not two. Two meant the queue filled to a pair immediately -- round
    // one opened with a customer and then a second one a second and a half
    // later, both wanting three of something. One means only this: the kitchen
    // never sits empty. Which is what was actually asked for
    [Tooltip("Kuyrukta en az kac musteri tutulmaya calisilsin. " +
             "Altina duserse bir sonraki musteri sirasini beklemeden gelir. 0 = varsayilan 1")]
    [SerializeField] private int keepBusy = 1;

    [Tooltip("Kuyruk bosaldiginda yeni musteri kac saniyede gelir. 0 = varsayilan 1.5")]
    [SerializeField] private float refillDelay = 1.5f;

    [Tooltip("Dolu kuyrukta bir yer acildiktan sonra yeni musteri gelmeden " +
             "onceki nefes payi. Kiss ve cikis yuruyusunun ustune eklenir. " +
             "0 = varsayilan 1")]
    [SerializeField] private float slotReuseDelay = 1f;

    [Tooltip("Giden musteri servis yerinden bu kadar uzaklasmadan yeri bos " +
             "sayilmaz. 0 = varsayilan 1.1")]
    [SerializeField] private float departureClearance = 1.1f;

    private int KeepBusy => keepBusy > 0 ? keepBusy : 1;
    private float RefillDelay => refillDelay > .01f ? refillDelay : 1.5f;
    private float SlotReuseDelay => slotReuseDelay > .01f ? slotReuseDelay : 1f;
    private float DepartureClearance =>
        departureClearance > .01f ? departureClearance : 1.1f;

    private float FirstCustomerDelay => firstCustomerDelay > .01f ? firstCustomerDelay : 2f;
    private float CustomerInterval => customerInterval > .01f ? customerInterval : 4f;

    // Patience comes out of the order, not out of a dice roll.
    //
    // A random length is variety with nothing behind it: the customer asking
    // for one drink and the customer asking for three burgers get the same
    // clock, and whether that is fair is decided by chance rather than by what
    // was asked for. Read off the order it is a promise instead -- however long
    // this actually takes, that is how long you have
    [Header(" Sabir ")]
    [Tooltip("Her siparise eklenen sabit pay: yurume, sipariste anlasma. 0 = varsayilan 3.5")]
    [SerializeField] private float patienceBase = 3.5f;

    [Tooltip("Yemek kendi suresini soylemiyorsa, bir tanesi icin verilen saniye. 0 = varsayilan 4.5")]
    [SerializeField] private float patiencePerItem = 4.5f;

    [Tooltip("Hicbir musteri bundan az beklemez, hesap ne cikarsa ciksin. 0 = varsayilan 20")]
    [SerializeField] private float minimumPatience = 20f;

    [Tooltip("Ust sinir. Slot sayisiyla birlikte buyumeli: 3 slot icin 60 uygun. " +
             "0 = varsayilan 60")]
    [SerializeField] private float maximumPatience = 60f;

    [Tooltip("Son raundda sabir bu katsayiyla carpilir. 1 = raundlar sabri hic degistirmez. " +
             "0 = varsayilan 0.7")]
    [SerializeField] private float latePressure = .7f;

    private float PatienceBase => patienceBase > .01f ? patienceBase : 3.5f;
    private float PatiencePerItem => patiencePerItem > .01f ? patiencePerItem : 4.5f;
    private float MinimumPatience => minimumPatience > .01f ? minimumPatience : 20f;
    private float MaximumPatience => maximumPatience > .01f ? maximumPatience : 60f;
    private float LatePressure => latePressure > .01f ? Mathf.Min(latePressure, 1f) : .7f;

    // Sabit pay + kendi isi + onundeki kuyrugun isi, sonra raund baskisi.
    //
    // The queue term is the one that matters. Two customers each wanting one
    // drink used to get two identical short clocks running at once, and the
    // second one was impossible through no fault of the player -- they were
    // being timed on work that had not started yet. Counting what is owed to
    // the people in front turns that into seven seconds and fourteen
    private float PatienceFor(OrderLine[] order, float ahead)
    {
        // Summed across the rows. A burger and a fries is the burger work plus
        // the fries work -- pricing it off one of the two would make every
        // mixed order short by whichever half was ignored
        float work = 0f;

        if (order != null)
        {
            for (int i = 0; i < order.Length; i++)
                work += WorkFor(order[i].food, order[i].count);
        }

        float raw = (PatienceBase + work + ahead) * RoundPressure();

        // The floor is the job itself. Round pressure is allowed to eat the
        // slack -- the walk, the queue in front -- and nothing else: a clock
        // shorter than the work it is timing is a customer who leaves angry
        // however well the game is played
        float floor = Mathf.Max(MinimumPatience, PatienceBase + work);

        // Max on the ceiling too, in case the two fields end up crossed over.
        // A minimum that loses to a maximum is a minimum that does nothing
        float ceiling = Mathf.Max(floor, MaximumPatience);

        // The ceiling is the one place this can still be unfair, so it says so
        // rather than quietly handing out a clock that cannot be met. Hitting
        // it means the queue holds more work than the longest clock allowed --
        // somebody at the back runs out before their turn ever comes
        if (raw > ceiling + .01f && !warnedAboutCap)
        {
            warnedAboutCap = true;

            Debug.LogWarning(name + ": kuyruk, saatin anlatabileceginden derin.\n" +
                             "  hesaplanan " + raw.ToString("0") + " sn, ust sinir " +
                             ceiling.ToString("0") + " sn.\n" +
                             "  Bu musteri sirasi gelmeden suresi dolabilir -- kendi hatasi olmadan.\n" +
                             "  Max Customers'i dusur ya da Maximum Patience'i yukselt.", this);
        }

        return Mathf.Clamp(raw, floor, ceiling);
    }

    private bool warnedAboutCap;

    private float WorkFor(SpawnableFood order, int count)
    {
        float perItem = order != null && order.PrepSeconds > .01f
            ? order.PrepSeconds
            : PatiencePerItem;

        return perItem * Mathf.Max(1, count);
    }

    // What is still owed to everyone standing at THIS counter.
    //
    // Counted in items left rather than items ordered: someone three quarters
    // served is nearly out of the way, and charging the next customer for food
    // already handed over would inflate every clock behind them
    public float QueuedWork()
    {
        if (slots == null)
            return 0f;

        float total = 0f;

        for (int i = 0; i < slots.Length; i++)
        {
            Customer customer = slots[i];

            if (customer == null)
                continue;

            // Row by row, because a mixed order is not "so many of one thing".
            // Charging the whole remainder at the first row's rate is wrong in
            // both directions depending on which row happens to be first
            int left = customer.FoodNeededCount - customer.FoodTakenCount;

            if (left <= 0)
                continue;

            OrderLine[] lines = customer.Lines;

            if (lines == null || lines.Length <= 0)
            {
                total += WorkFor(null, left);
                continue;
            }

            // Spread over the rows in proportion, since which rows are still
            // owed is the customer's own business and not worth exposing
            float share = left / (float)Mathf.Max(1, customer.FoodNeededCount);

            for (int row = 0; row < lines.Length; row++)
                total += WorkFor(lines[row].food, lines[row].count) * share;
        }

        return total;
    }

    // Every counter, not just this one. One player serves all of them, so a
    // queue at the other end of the kitchen is time this customer spends
    // waiting exactly the same
    private float WorkAhead()
    {
        return RoundManager.Exists ? RoundManager.Instance.WorkInFlight() : QueuedWork();
    }

    // Later rounds give less slack, never less work.
    //
    // Read across the whole list rather than off round 50, so the curve is the
    // same shape whether somebody ships fifty rounds or fifteen
    private float RoundPressure()
    {
        if (!RoundManager.Exists)
            return 1f;

        RoundManager manager = RoundManager.Instance;

        if (manager.RoundCount <= 1)
            return 1f;

        float through = Mathf.Clamp01((manager.Round - 1f) / (manager.RoundCount - 1f));

        return Mathf.Lerp(1f, LatePressure, through);
    }

    // Where somebody who gave up walks out. Defaults to the door they came in
    // by, which is both the obvious answer and one that needs no wiring
    [Tooltip("Kacan musterinin cikacagi nokta. Bos ise geldigi yerden cikar")]
    [SerializeField] private Transform exitPoint;

    private Vector3 ExitPosition =>
        exitPoint != null ? exitPoint.position : spawnPoint.position;

    // Fixed standing spots rather than a queue. A customer keeps their spot
    // for as long as they are here, so nobody shuffles sideways when someone
    // else is served — only the column that opened up moves forward
    private Customer[] slots;

    // A customer who has finished ordering is still occupying physical space.
    // Keep the slot reserved through Chef's Kiss and the first part of the exit
    // walk, or the next customer is sent into exactly the same place while the
    // old one is still standing there. It also lets every serving path -- hand,
    // OrderCounter, worker and give-up -- obey the same rule.
    private readonly HashSet<Customer> departing = new HashSet<Customer>();

    // Handed over by OrderCounter in Awake, so the orders a customer can ask
    // for are always exactly what the counter is able to serve. Left null by
    // the single-item coffee shop stations, which fall back to the old behaviour
    // Serialised so it can be filled by hand.
    //
    // It was private and set only by OrderCounter, which meant a scene without
    // one -- or with one whose drop zones are empty -- produced customers with
    // no order at all, and nothing anywhere said so. The field being invisible
    // was the reason it could not even be checked
    [Tooltip("Musterilerin isteyebilecegi yemekler. Bos birakilirsa OrderCounter doldurur")]
    [SerializeField] private SpawnableFood[] possibleOrders;

    private const float arrivedDistance = .1f;

    private Interactable standPoint;

    // Where the player has to be to hand anything over. Walking towards the
    // CUSTOMER is the wrong move and was the old one: it ends up on their side
    // of the counter, which is the one place the serving trigger is not
    public Vector3 ServePosition =>
        standPoint != null ? standPoint.StandPosition : transform.position;

    [Tooltip("Servis noktasi trigger kenarindan kac birim iceri cekilsin. " +
             "Buyutmek oyuncuyu musteriden uzaklastirir. 0 = varsayilan 0.9")]
    [SerializeField] private float serveInset = .9f;

    private float ServeInset => serveInset > .01f ? serveInset : .9f;

    // Where to stand to hand THIS customer their food.
    //
    // One fixed spot per counter meant walking away from somebody in order to
    // serve them: the customer at the far end of a wide counter was served from
    // a point behind the middle of it, and on the way there the player put more
    // distance between themselves and the person they were walking to.
    //
    // The trigger already decides whether serving is allowed. So it decides
    // where to stand too -- the nearest point inside it. No second piece of
    // geometry to author, and nowhere it can send the player that the serving
    // rule would then refuse
    public Vector3 ServePositionFor(Customer customer)
    {
        if (customer == null)
            return ServePosition;

        Collider[] zones = GetComponents<Collider>();

        if (zones.Length <= 0)
            return ServePosition;

        Vector3 wanted = customer.transform.position;

        Vector3 nearest = Vector3.zero;
        float nearestDistance = float.MaxValue;

        foreach (Collider zone in zones)
        {
            if (zone == null || !zone.enabled)
                continue;

            Vector3 point = zone.ClosestPoint(wanted);
            float distance = Vector3.SqrMagnitude(point.With(y: 0) - wanted.With(y: 0));

            if (distance >= nearestDistance)
                continue;

            nearest = point;
            nearestDistance = distance;
        }

        if (nearestDistance >= float.MaxValue)
            return ServePosition;

        // Pushed off the surface, straight back from the customer. Standing
        // exactly on the skin of a trigger is standing on the line where enter
        // and exit disagree, and the serve waits forever on a frame that says
        // outside
        Vector3 back = nearest.With(y: 0) - wanted.With(y: 0);

        // Already inside it somehow -- no direction to push, and no reason to
        if (back.sqrMagnitude < .0001f)
            return ServePosition;

        Vector3 stand = nearest + back.normalized * ServeInset;

        return stand.With(y: ServePosition.y);
    }

    private void Awake()
    {
        slots = new Customer[Mathf.Max(1, maxCustomers)];
        standPoint = GetComponent<Interactable>();
    }

    private void Start()
    {
        WarnIfUnreachable();

        if (closed)
            return;

        // A round manager decides the wave: how many and how fast. Without one
        // this keeps its own pace, which is what every scene did before rounds
        // existed and what the coffee shop still does
        if (RoundManager.Exists)
            return;

        StartSpawningCustomers();
    }

    // A counter the player cannot serve is a counter that can only take lives.
    //
    // Tapping a customer walks the player to the counter that CLAIMS them, and
    // the claim is TapToServe's own list. A counter missing from it spawns
    // people nobody can ever reach, and every one of them times out. On screen
    // that is a life disappearing with nothing visible causing it
    private void WarnIfUnreachable()
    {
        if (closed)
            return;

        TapToServe[] players = FindObjectsByType<TapToServe>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (TapToServe player in players)
        {
            if (player != null && player.Serves(this))
                return;
        }

        Debug.LogWarning(name + ": bu tezgahi hicbir oyuncu servis edemiyor.\n" +
                         "  Musteri uretiyor ama kimse yetisemez -- her biri can goturur.\n" +
                         "  Ya Player > Tap To Serve > Customer Managers listesine ekle,\n" +
                         "  ya da bu tezgahta Closed'i isaretle.\n" +
                         "  Denetim: Cooked Fast > Musteri: Tezgahlari Denetle", this);
    }

    // ---- rounds -------------------------------------------------------------

    private bool roundDriven;
    private int roundTotal;
    private int roundSpawned;

    // Handed down by the round. One until a round says otherwise, so a scene
    // with no RoundManager behaves exactly as it did before orders could name
    // two things
    private int maxOrderTypes = 1;

    // Restarts the spawning loop on this round's numbers. Stopping the old one
    // first matters: two loops running is a wave arriving at twice the pace it
    // was designed for, and the round's own count would be spent in half the time
    public void BeginRound(int total, float interval, int orderTypes)
    {
        roundDriven = true;
        roundTotal = closed ? 0 : Mathf.Max(0, total);
        roundSpawned = 0;
        maxOrderTypes = Mathf.Max(1, orderTypes);

        if (roundTotal <= 0)
        {
            StopSpawning();
            return;
        }

        Restart(interval);
    }

    public int ActiveCustomers
    {
        get
        {
            if (slots == null)
                return 0;

            int count = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                    count++;
            }

            return count;
        }
    }

    // Served AND gone, not just spawned. A round that ends the moment the last
    // customer walks in ends while they are still waiting to be served
    public bool RoundClear => !roundDriven || (roundSpawned >= roundTotal && ActiveCustomers == 0);

    // One per frame, deliberately.
    //
    // Sending somebody home shifts the column behind them, so walking the slots
    // while changing them is walking a list that is being rewritten. Two
    // customers running out on the same frame is rare, and the second one waits
    // a frame -- which is a sixtieth of a second added to a wait that has
    // already lasted the better part of a minute
    private void Update()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Customer customer = slots[i];

            if (customer == null || departing.Contains(customer) || !customer.PatienceRanOut)
                continue;

            GiveUp(customer);
            return;
        }
    }

    // Clears the queue because somebody was shot in front of it.
    //
    // Dequeued before being told to run, in that order: a customer still listed
    // in a slot is a customer this counter will keep steering, and it would
    // steer them straight back into the line they are trying to leave.
    public int Scatter(Customer except)
    {
        int ran = 0;

        // A counter that has not been set up yet has no queue to clear.
        if (slots == null)
            return 0;

        // The body is dropped from the queue too, though it is not going
        // anywhere. A customer still listed in a slot is one this counter will
        // keep arranging -- and re-seating a corpse turns off the very thing
        // that was stopping it sliding across the floor.
        Dequeue(except);

        for (int i = 0; i < slots.Length; i++)
        {
            Customer customer = slots[i];

            if (customer == null || customer == except ||
                departing.Contains(customer))
                continue;

            Dequeue(customer);
            customer.Flee(ExitPosition);
            ran++;
        }

        return ran;
    }

    private void GiveUp(Customer customer)
    {
        Dequeue(customer);

        // Printed every time, because a life is the most expensive thing that
        // happens in this game and "one just went" is not enough to tell a
        // fair loss from a broken clock
        Debug.Log(name + ": " + customer.name + " kacti.  verilen sabir " +
                  customer.PatienceGiven.ToString("0.0") + " sn,  bekledigi " +
                  customer.Waited.ToString("0.0") + " sn", customer);

        // This method is reached only when the order clock genuinely expires.
        // Put the sound beside the life loss rather than in Customer.Leave:
        // successful customers use the same exit machinery and must not play
        // the failure cue.
        // The final miss has its own disappointed-crowd game-over cue. Do not
        // pile this short miss sound underneath it on the same frame.
        if (Lives.Instance == null || Lives.Instance.Left > 1)
            SoundManager.Play(SoundManager.Sound.OrderFailed);

        if (Lives.Instance != null)
            Lives.Instance.Lose(customer.transform.position + Vector3.up * 1.7f);
        else
            Debug.LogWarning(name + ": musteri kacti ama sahnede Lives yok -- " +
                             "can eksilmedi.\n  Cooked Fast > Can: Slotlari Kur", this);

        customer.GiveUp(ExitPosition);
    }

    private void StartSpawningCustomers()
    {
        Restart(CustomerInterval);
    }

    private Coroutine spawning;

    // A coroutine rather than InvokeRepeating, and that is the whole change.
    // InvokeRepeating owns its own clock and cannot be told the queue just
    // emptied -- the only thing it can do when a slot is free early is nothing
    private void Restart(float interval)
    {
        StopSpawning();

        if (closed)
            return;

        spawning = StartCoroutine(Spawning(Mathf.Max(.2f, interval)));
    }

    private void StopSpawning()
    {
        if (spawning != null)
            StopCoroutine(spawning);

        spawning = null;
    }

    private IEnumerator Spawning(float interval)
    {
        yield return new WaitForSeconds(FirstCustomerDelay);

        while (!roundDriven || roundSpawned < roundTotal)
        {
            // Full. Nothing to do but wait for somebody to be served, and no
            // clock worth running while that is true
            if (GetFirstEmptySlot() < 0)
            {
                // The interval has usually elapsed long before a full queue
                // opens. Spawning on the very first free frame made the new
                // customer step into the departing customer's body. Wait until
                // a slot genuinely opens, then start a NEW short breath clock.
                while ((!roundDriven || roundSpawned < roundTotal) &&
                       GetFirstEmptySlot() < 0)
                    yield return null;

                if (roundDriven && roundSpawned >= roundTotal)
                    break;

                float openingAge = 0f;

                while (openingAge < SlotReuseDelay && GetFirstEmptySlot() >= 0)
                {
                    openingAge += Time.deltaTime;
                    yield return null;
                }

                // Another column may have advanced into the opening while we
                // were breathing. If so, go back to waiting instead of spawning
                // through a now-full queue.
                if (GetFirstEmptySlot() < 0)
                    continue;
            }

            SpawnNewCustomer();

            float waited = 0f;

            // Cut short the moment the queue thins out. Standing in an empty
            // kitchen waiting for a timer is the one thing no round should ask
            while (waited < interval && ActiveCustomers >= KeepBusy)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // A beat, not nothing. Two customers walking in on the same step
            // reads as a glitch even when the numbers are right
            if (waited < interval)
                yield return new WaitForSeconds(RefillDelay);
        }

        spawning = null;
    }

    private void SpawnNewCustomer()
    {
        // The wave is a count, not a duration: the loop that calls this stops
        // at the total, and a full queue simply waits. The round takes as long
        // as it takes
        if (roundDriven && roundSpawned >= roundTotal)
            return;

        int slot = GetFirstEmptySlot();

        if (slot < 0)
            return;

        // Read BEFORE this customer is put in a slot. A pooled customer still
        // carries the last order's counts until Initialize clears them, so one
        // counted here would be charging themselves for somebody else's food
        float ahead = WorkAhead();

        Customer newCustomer = CustomerManager.Instance.Pop(spawnPoint.position);
        newCustomer.name = "Customer " + Random.Range(0, 1000);

        slots[slot] = newCustomer;
        roundSpawned++;

        int foodCount = Random.Range(minMaxCustomerFoodCount.x, minMaxCustomerFoodCount.y + 1);
        Vector3 targetPosition = GetTargetCustomerPosition(slot);

        // Every so often, somebody who has not decided.
        //
        // Their row names no food, which every serving path already reads as
        // "anything will do" -- so the player hands over whatever is nearest
        // and they go. It is a breather in the middle of a round, and a use for
        // the thing that came off the grill while the order it was for expired
        OrderLine[] order = Random.value * 100f < UndecidedChance
            ? new[] { new OrderLine(null, 1) }
            : PickOrder(foodCount);

        // After the order is known, because the order is what decides it, and
        // before Initialize, which eventually opens the bubble -- the bubble is
        // what starts the clock, and a clock cannot be given its length after
        // it has started running
        newCustomer.SetPatience(PatienceFor(order, ahead));

        // The row decides how high the card floats. Standing further back does
        // not separate two cards on an isometric screen -- it puts the far one
        // directly behind the near one
        newCustomer.SetBubbleLift(slot / Mathf.Max(1, customersPerRow) * BubbleRowLift);
        newCustomer.SetBubbleScale(customersPerRow >= 4 ? FourWideBubbleScale : 1f);

        // All slots face perpendicular to the counter, not towards one shared
        // point on it. Aiming every customer at Cashier_Machine made the side
        // slots turn diagonally inward. Using the queue axis keeps every body
        // parallel and looking straight across the counter.
        Vector3 counterFacing = queueStartPoint != null &&
                                QueueOffset.sqrMagnitude > .0001f
            ? -QueueOffset.normalized
            : -transform.forward;

        newCustomer.Initialize(order, targetPosition, counterFacing);
    }

    // Builds the whole order: which foods, and how many of each.
    //
    // Two things is not two orders. The item count is shared out between them
    // rather than doubled, so "the round where orders get mixed" is a round
    // where the same amount of food arrives in a more awkward shape -- which is
    // the difficulty being asked for. Doubling it would be a different game
    private OrderLine[] PickOrder(int items)
    {
        items = Mathf.Max(1, items);

        int types = Mathf.Clamp(maxOrderTypes, 1, items);

        // Never more kinds than there are things on the menu, and never the
        // same thing twice: two rows of burger is one row of two burgers drawn
        // badly
        List<SpawnableFood> chosen = new List<SpawnableFood>();

        for (int i = 0; i < types; i++)
        {
            SpawnableFood pick = PickFood(chosen);

            if (pick == null)
                break;

            chosen.Add(pick);
        }

        if (chosen.Count <= 0)
            return new[] { new OrderLine(null, items) };

        OrderLine[] order = new OrderLine[chosen.Count];

        int share = items / chosen.Count;
        int extra = items - share * chosen.Count;

        for (int i = 0; i < chosen.Count; i++)
        {
            int mine = share + (extra > 0 ? 1 : 0);

            if (extra > 0)
                extra--;

            order[i] = new OrderLine(chosen[i], Mathf.Max(1, mine));
        }

        return order;
    }

    // Empty rows skipped rather than handed out.
    //
    // The list arrives one entry per drop zone, and a zone whose accepted food
    // has not been set yet contributes a null. Handing that out produces a
    // customer whose order is nothing, which on screen is an empty bubble --
    // indistinguishable from the bubble being broken, and that is exactly how
    // it was read
    // Takes what is already spoken for, so a two row order names two different
    // things. Comparing by type rather than by instance: the menu may hold two
    // burger prefabs, and asking for both is asking for burgers twice
    private SpawnableFood PickFood(List<SpawnableFood> taken)
    {
        if (possibleOrders == null)
            return null;

        int filled = 0;

        for (int i = 0; i < possibleOrders.Length; i++)
        {
            if (Available(possibleOrders[i], taken))
                filled++;
        }

        if (filled <= 0)
            return null;

        int wanted = Random.Range(0, filled);

        for (int i = 0; i < possibleOrders.Length; i++)
        {
            if (!Available(possibleOrders[i], taken))
                continue;

            if (wanted-- <= 0)
                return possibleOrders[i];
        }

        return null;
    }

    // Two rows have to look different, which is a stronger test than being
    // different.
    //
    // Type was the first rule and it is not enough: the menu holds "burger" and
    // "burger 1" as separate prefabs, and the icon baker gives both of them the
    // same hamburger picture. Two rows of the same picture is one order drawn
    // twice however different the classes behind them are, and the player is
    // reading the picture
    private static bool Available(SpawnableFood food, List<SpawnableFood> taken)
    {
        if (food == null)
            return false;

        for (int i = 0; i < taken.Count; i++)
        {
            SpawnableFood other = taken[i];

            if (other == null)
                continue;

            if (other.GetType() == food.GetType())
                return false;

            // Only when both actually have one. Two foods falling back to their
            // own models are told apart by the models
            if (other.Icon != null && other.Icon == food.Icon)
                return false;
        }

        return true;
    }

    // Called by OrderCounter before the first spawn tick fires
    public void SetPossibleOrders(SpawnableFood[] orders)
    {
        // An empty answer from the counter does not wipe a list somebody filled
        // in by hand. The counter builds its list from drop zones, and a scene
        // being rebuilt has moments where it has none
        if (orders == null || orders.Length <= 0)
            return;

        possibleOrders = orders;
    }

    // Both spacings are authored in the queue start point's LOCAL space, so
    // rotating the station turns the whole block with it. In world space a
    // rotated station lines its customers up into a wall and pathing fails
    private Vector3 QueueOffset => queueStartPoint.rotation * queueSpacing;
    private Vector3 SideOffset => queueStartPoint.rotation * sideSpacing;
    private Vector3 SideDirection => SideOffset.sqrMagnitude > .0001f
        ? SideOffset.normalized
        : Vector3.zero;

    private Vector3 GetTargetCustomerPosition(int index)
    {
        int row = index / customersPerRow;
        int column = index % customersPerRow;

        // Centre the row on the start point instead of growing to one side,
        // so a 3-wide block sits square in front of the counter. With
        // customersPerRow = 1 this term is zero and nothing moves
        float centredColumn = column - (customersPerRow - 1) * .5f;

        return queueStartPoint.position + QueueOffset * row +
               SideOffset * centredColumn + SideDirection * sideCentreOffset;
    }

    // The block of floor the customers stand on, every row of it.
    //
    // Built from maxCustomers rather than the slot array so it can be asked
    // before Awake -- the editor needs this while the game is not running, to
    // put a no-walk zone over it.
    //
    // Axis aligned, so a queue running at an angle gets a box a little larger
    // than the customers actually occupy. The alternative is an oriented box
    // nobody can nudge in the inspector afterwards
    public Bounds CustomerArea(float margin)
    {
        if (queueStartPoint == null)
            return new Bounds(transform.position, Vector3.one);

        Bounds area = new Bounds(GetTargetCustomerPosition(0), Vector3.zero);

        int count = Mathf.Max(1, maxCustomers);

        for (int i = 1; i < count; i++)
            area.Encapsulate(GetTargetCustomerPosition(i));

        area.Expand(margin * 2f);

        return area;
    }

    // Away from the counter, along the queue. Row 0 stands closest to the
    // counter and every row after it is one of these further out
    public Vector3 QueueDirection =>
        queueStartPoint == null ? Vector3.forward : QueueOffset.normalized;

    // Where the standing spots are, so they can be checked from outside.
    //
    // These are worked out from a start point and two offsets and never asked
    // about afterwards -- nothing verifies that the floor they land on is floor
    // anybody can walk on. A slot off the navigation mesh, or too close to its
    // edge to stand in, is a customer that never quite arrives, and the way that
    // looks from the outside is a customer weaving on the way there
    public int Slots => Mathf.Max(1, maxCustomers);

    public Vector3 SlotPosition(int index) => GetTargetCustomerPosition(index);

    public Transform SpawnPoint => spawnPoint;
    public Transform ExitPoint => exitPoint;

    // Where the last customer stands. The line past which nobody in this game
    // has any business walking
    public Vector3 BackRow
    {
        get
        {
            if (queueStartPoint == null)
                return transform.position;

            return GetTargetCustomerPosition(Mathf.Max(1, maxCustomers) - 1);
        }
    }

    private int GetFirstEmptySlot()
    {
        int best = -1;
        float farthest = -1f;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                continue;

            if (spawnPoint == null)
                return i;

            // Fill the hardest-to-reach outside places first. Four customers
            // leave one spawn point almost together in the test wave; filling
            // the middle first parks bodies directly across the outer paths,
            // so the last customer spends forever negotiating around them.
            // Outside-first lets the paths fan apart before anyone stops.
            float distance = (GetTargetCustomerPosition(i) -
                              spawnPoint.position).sqrMagnitude;

            if (distance <= farthest)
                continue;

            farthest = distance;
            best = i;
        }

        return best;
    }

    // Only the front row can be reached across the counter
    public int ServableSlotCount => Mathf.Min(customersPerRow, slots.Length);

    // Null while the customer is still walking to their spot, so a slow one
    // never blocks the neighbours standing next to them
    public Customer GetArrivedCustomer(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return null;

        Customer customer = slots[slot];

        if (customer == null || departing.Contains(customer))
            return null;

        float distance = Vector3.Distance(
            customer.transform.position.With(y: 0),
            GetTargetCustomerPosition(slot).With(y: 0));

        return distance < arrivedDistance ? customer : null;
    }

    // Used by tap-to-serve. Nearest rather than an exact hit, because the
    // customer prefab has no collider and a finger is not a pixel
    public Customer FindNearestCustomer(Vector3 worldPoint, float maxDistance)
    {
        Customer nearest = null;
        float nearestDistance = maxDistance;

        for (int i = 0; i < slots.Length; i++)
        {
            Customer customer = slots[i];

            if (customer == null || departing.Contains(customer))
                continue;

            float distance = Vector3.Distance(
                customer.transform.position.With(y: 0),
                worldPoint.With(y: 0));

            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = customer;
        }

        return nearest;
    }

    // The same question asked on the screen instead of on the ground.
    //
    // A tap ray in an isometric view lands on whatever is nearest the camera --
    // the counter, the floor in front, a cabinet door -- and that point can be
    // metres from the customer standing behind it while being directly under the
    // finger. Measuring in pixels is measuring what the player actually aimed at
    public Customer FindNearestOnScreen(Camera camera, Vector2 screenPoint, float maxPixels, float aimHeight)
    {
        if (camera == null)
            return null;

        Customer nearest = null;
        float nearestDistance = maxPixels;

        for (int i = 0; i < slots.Length; i++)
        {
            Customer customer = slots[i];

            if (customer == null || departing.Contains(customer))
                continue;

            // Aimed at the body rather than the feet: a tap lands on the middle
            // of a character, and their pivot is on the floor
            Vector3 screen = camera.WorldToScreenPoint(
                customer.transform.position + Vector3.up * aimHeight);

            // Behind the camera projects to a valid looking point in front of it
            if (screen.z <= 0f)
                continue;

            float distance = Vector2.Distance(screenPoint, new Vector2(screen.x, screen.y));

            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = customer;
        }

        return nearest;
    }

    // Lets a server ask "is this customer mine?" without exposing the slots
    public bool Contains(Customer customer)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == customer)
                return true;
        }

        return false;
    }

    private int GetServableSlot()
    {
        for (int i = 0; i < ServableSlotCount; i++)
        {
            if (GetArrivedCustomer(i) != null)
                return i;
        }

        return -1;
    }

    public bool IsCustomerReadyToTakeFood()
    {
        return GetServableSlot() >= 0;
    }

    public Customer PeekFirstCustomer()
    {
        int slot = GetServableSlot();

        return slot < 0 ? null : slots[slot];
    }

    // Takes the customer rather than assuming the front of a line: the order
    // counter serves whoever it happens to have food for, not who arrived first
    public void Dequeue(Customer customer)
    {
        if (customer == null || departing.Contains(customer))
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != customer)
                continue;

            departing.Add(customer);
            StartCoroutine(ReleaseSlotWhenClear(customer, i));
            return;
        }
    }

    private IEnumerator ReleaseSlotWhenClear(Customer customer, int slot)
    {
        Vector3 occupied = GetTargetCustomerPosition(slot).With(y: 0);
        float age = 0f;

        // IsReacting is the newly added time the old spawn mechanism did not
        // know about. Distance is equally important: ending the Kiss and
        // freeing the slot on that exact frame still starts the replacement
        // while the old customer is standing on the mark.
        while (customer != null)
        {
            float distance = Vector3.Distance(
                customer.transform.position.With(y: 0), occupied);

            if (!customer.IsReacting && distance >= DepartureClearance)
                break;

            age += Time.deltaTime;

            // A broken NavMesh path must not deadlock the entire round. Normal
            // exits clear this in well under two seconds after the reaction;
            // eight seconds means something external failed, so release with a
            // diagnostic instead of leaving the queue full forever.
            if (age >= 8f && !customer.IsReacting)
            {
                Debug.LogWarning(name + ": " + customer.name +
                                 " cikis yerinden uzaklasamadi; slot 8 sn sonra serbest birakildi.",
                                 customer);
                break;
            }

            yield return null;
        }

        departing.Remove(customer);

        // The slot may have been repaired or cleared by an Editor/runtime tool
        // while this coroutine waited. Only release the reservation we own.
        if (slot < 0 || slot >= slots.Length || slots[slot] != customer)
            yield break;

        slots[slot] = null;
        AdvanceColumn(slot);
    }

    // Only the freed column steps forward one row. Every other customer keeps
    // standing exactly where they were
    private void AdvanceColumn(int freedSlot)
    {
        for (int i = freedSlot; i + customersPerRow < slots.Length; i += customersPerRow)
        {
            Customer behind = slots[i + customersPerRow];

            slots[i] = behind;
            slots[i + customersPerRow] = null;

            if (behind != null)
                behind.GoTo(GetTargetCustomerPosition(i));
        }
    }
}
