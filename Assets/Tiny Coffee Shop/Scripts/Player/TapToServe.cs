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

    // The one that actually decides it. Measured on screen, where the player is
    // aiming, rather than on the ground where the ray happened to land
    [Tooltip("Musterinin ekranda kac piksel yakinina tiklanirsa sayilir. Buyut = daha kolay")]
    [SerializeField] private float tapScreenRadius = 90f;

    [Tooltip("Musterinin neresine nisan alindigi. 0 = ayaklari, 0.7 = govdesi")]
    [SerializeField] private float tapAimHeight = .7f;

    // How close the player has to stand before the food changes hands.
    // Tapping from across the room walks there first
    // Only used to decide where to walk when tapping from far away. Whether
    // the food actually changes hands is decided by the station trigger
    [SerializeField] private float serveRange = 3f;
    [SerializeField] private float maxRayDistance = 200f;
    [SerializeField] private int baseRevenue = 1;

    // A whitelist instead of a blacklist, and that is the whole argument for
    // it. Naming every patch of floor the player may not walk on means
    // enumerating the mistakes; naming what may be tapped leaves nowhere to go
    // that nobody chose. Every walk then ends on an authored stand point, so
    // the facing, the reach and the station's trigger are all already right.
    //
    // Written false-is-the-new-way on purpose: this field is new on a component
    // already saved in the scene, and a field the file does not have comes back
    // false whatever the initialiser says
    [Tooltip("Acikken bos zemine tiklayinca oyuncu oraya yurur. " +
             "Kapaliyken sadece tiklanabilir seyler -- istasyonlar, tabaklar, musteriler")]
    [SerializeField] private bool walkOnGroundTap;

    [Tooltip("Bir duvarin kac birim arkasindaki istasyon hala secilebilir. " +
             "0 = varsayilan 0.5. Eksi = duvarlar hic engellemez, eski hali")]
    [SerializeField] private float reachThrough = .5f;

    private float ReachThrough => reachThrough < 0f ? float.MaxValue
        : reachThrough > .01f ? reachThrough : .5f;

    // Prints why a tap did nothing. Turn off once serving works
    [SerializeField] private bool logTaps = true;

    private HoldFoodAbility holdFoodAbility;
    private CharacterStats characterStats;
    private PlayerAnimator playerAnimator;

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
        playerAnimator = GetComponent<PlayerAnimator>();
        agent = GetComponent<NavMeshAgent>();
        joystickController = GetComponent<JoystickPlayerController>();

        if (gameCamera == null)
            gameCamera = Camera.main;
    }

    private void Update()
    {
        HandleTap();
        HandlePendingCustomer();
        HandlePendingInteractable();
        FaceTarget();
        ReportIfStuck();
    }

    // ---- standing the right way round ---------------------------------------

    [Header(" Bakis ")]
    [Tooltip("Hedefe donme hizi. Buyut = daha keskin doner")]
    [SerializeField] private float turnSpeed = 10f;

    [Tooltip("Hedefe bu kadar kala donmeye baslar. 0 = ancak durunca doner")]
    [SerializeField] private float turnStartsWithin = 2f;

    private bool hasFacing;
    private Vector3 facingPoint;

    // The animator only turns the model along the move vector, so whatever
    // direction the last step happened to be in is the direction the player is
    // left standing -- which next to a counter is sideways to the customers.
    //
    // Begun before the stop, not after it. Waiting for a standstill meant the
    // walk finished and only then did the character swivel round, which reads as
    // an afterthought; starting it a couple of metres out means they arrive
    // already looking at whoever they came to serve
    private void FaceTarget()
    {
        if (!hasFacing || playerAnimator == null)
            return;

        if (!Arriving())
        {
            playerAnimator.ClearFaceOverride();
            return;
        }

        playerAnimator.FaceOverride(facingPoint - transform.position, turnSpeed);
    }

    private bool Arriving()
    {
        if (agent == null || !agent.enabled)
            return true;

        // Standing still counts however far away it stopped: an agent that gave
        // up short of its destination is not going to get any closer
        if (agent.velocity.sqrMagnitude <= .01f)
            return true;

        if (agent.pathPending)
            return false;

        return agent.remainingDistance <= turnStartsWithin;
    }

    private void FaceOnArrival(Vector3 point)
    {
        hasFacing = true;
        facingPoint = point;
    }

    // ---- walking up to something and using it -------------------------------

    // One path for every station now. Each of them used to have its own pending
    // field, its own reach and its own arrival check that were the same code
    // three times over, and every station added afterwards was a fourth copy
    private Interactable pendingInteractable;

    [Tooltip("Bu hizin altina dusunce durmus sayilir")]
    [SerializeField] private float arrivedSpeed = .15f;

    // Added ON TOP of Reach, not compared against it. Reach is already the
    // radius the work happens in, so a plain maximum of the two is whichever is
    // bigger -- and with Reach at 1.6 that was Reach every time, which is no
    // anticipation at all
    [Tooltip("Menzile girmeden ONCE bu kadar mesafede uzanma animasyonu baslar. 0 = menzilde baslar")]
    [SerializeField] private float actionStartsWithin = .6f;

    private bool actionStarted;

    // Waiting for the hand to close looks better and plays slower, and one of
    // those wins. The reach is around a third of a second, which is nothing to
    // watch once and a lot to sit through on every single pickup -- so the food
    // lands as the arm starts moving and the animation carries on around it.
    //
    // Kept as a field rather than deleted, because the slow version is the one
    // that reads correctly and is worth having back for a trailer
    [Tooltip("Isaretliyse yemek animasyon BITINCE ele gelir. Kapali = hemen gelir")]
    [SerializeField] private bool waitForActionToFinish;

    // Three moments, deliberately separated.
    //
    // The reach BEGINS on approach, so the character is already going for the
    // plate as the last stride lands instead of arriving, stopping, and only
    // then noticing the counter. The work HAPPENS as soon as they have stopped,
    // which is the rule that kept the hand from filling on the way past
    private void HandlePendingInteractable()
    {
        if (pendingInteractable == null)
            return;

        Vector3 here = transform.position;
        Vector3 there = pendingInteractable.StandPosition;

        here.y = 0f;
        there.y = 0f;

        float distance = Vector3.Distance(here, there);

        if (!actionStarted &&
            distance <= pendingInteractable.Reach + Mathf.Max(0f, actionStartsWithin))
        {
            actionStarted = true;

            if (playerAnimator != null)
                playerAnimator.PlayAction(PlayerAnimator.Action.Pan, true);
        }

        if (distance > pendingInteractable.Reach)
            return;

        // Being in range is not the same as having arrived. Firing on range
        // alone meant the hand filled mid-stride, on the way past, and the walk
        // carried on afterwards as if nothing had happened
        if (!HasStopped())
            return;

        if (waitForActionToFinish && playerAnimator != null && playerAnimator.ActionPlaying)
            return;

        Interactable target = pendingInteractable;
        pendingInteractable = null;
        actionStarted = false;

        Interact(target);

        // Only when the reach was waited out. Holding the pose cuts the running
        // animation into its loop, which in the fast version means snapping the
        // arm mid-reach and then standing there for a second -- two costs, and
        // the second one is the slowness this was meant to remove.
        //
        // Left to itself the reach plays out on its own, with the food already
        // in hand, which is the same picture arrived at sooner
        if (waitForActionToFinish && playerAnimator != null)
            playerAnimator.HoldActionPose(PlayerAnimator.Action.Pan, FoodShowTime);
    }

    [Tooltip("Yemek ele geldikten sonra kac saniye elde gosterilsin. " +
             "0 = varsayilan 1, EKSI = hic gosterme")]
    [SerializeField] private float foodShowTime = 1f;

    // Zero is "nobody set this", not "no pause". The field is new on a component
    // that is already saved in the scene, and one of those does not reliably
    // come back carrying its initializer -- a silent zero here would look
    // exactly like the feature never having been added
    private float FoodShowTime => Mathf.Abs(foodShowTime) < .001f ? 1f : foodShowTime;

    // Whatever was being reached for is no longer being reached for. Without
    // this the pose is held all the way to wherever the player was sent next,
    // because the animation was told to ignore movement
    private void DropPendingAction()
    {
        actionStarted = false;

        if (playerAnimator != null)
            playerAnimator.CancelAction();
    }

    private bool HasStopped()
    {
        if (agent == null || !agent.enabled)
            return true;

        if (agent.pathPending)
            return false;

        // Two ways to be finished: the path ran out, or it did not and the
        // player is no longer walking it. The second covers an agent that gave
        // up short because something was in the way -- it will not get closer,
        // so waiting for the path to clear would wait forever
        if (!agent.hasPath)
            return true;

        return agent.velocity.sqrMagnitude <= arrivedSpeed * arrivedSpeed;
    }

    // Which station is on the object decides what arriving means. The order is
    // the order PlayerDetector used, with the tap-only ones in front: an object
    // carrying two of these is a wiring mistake, and answering consistently
    // makes it a findable one
    private void Interact(Interactable target)
    {
        GameObject go = target.gameObject;

        // Opened before the single call each handler is going to get. Without
        // this the delays inside them swallow every second tap
        holdFoodAbility.ReadyForOneTap();

        // The animation is NOT started here. It began on the approach, and by
        // the time this runs it has finished -- that is the whole ordering:
        // reach first, food second, so the plate appears as the hand closes
        // rather than a beat before the character has moved

        if (go.TryGetComponent(out HoldingShelf shelf))
        {
            Log(target.Label + ": " + (shelf.Swap(holdFoodAbility)
                ? "yemek el degistirdi"
                : "yer yok ya da tip uymuyor"));
            return;
        }

        if (go.TryGetComponent(out FridgeDoor fridge))
        {
            Log(target.Label + ": " + Describe(fridge.Tap(holdFoodAbility)));
            return;
        }

        if (go.TryGetComponent(out FryerStation fryer))
        {
            Log(target.Label + ": " + Describe(fryer.Tap(holdFoodAbility)));
            return;
        }

        if (go.TryGetComponent(out Trash trash))
        {
            Log(target.Label + ": " + (trash.Dump(holdFoodAbility, GetComponent<HoldDishAbility>())
                ? "el bosaltildi"
                : "elde bir sey yok"));
            return;
        }

        // Before the spawner: an oven with a FoodSpawnerStation left on it was a
        // real bug once, and asking about the oven first makes it harmless
        if (go.TryGetComponent(out CookingStation cooker))
        {
            holdFoodAbility.HandleCookingStation(cooker);
            Log(target.Label + ": ocak" + Holding());
            return;
        }

        if (go.TryGetComponent(out FoodSpawnerStation spawner))
        {
            holdFoodAbility.HandleFoodSpawnerStation(spawner);
            Log(target.Label + ": tezgah" + Holding());
            return;
        }

        if (go.TryGetComponent(out FoodDropZone dropZone))
        {
            // byTap, so this one may take and swap. The trigger version that
            // fires every frame may only put down
            holdFoodAbility.HandleFoodDropZone(dropZone, true);
            Log(target.Label + ": teslim" + Holding());
            return;
        }

        if (go.TryGetComponent(out TableSet table))
        {
            Log(target.Label + ": " + Clean(table));
            return;
        }

        Log(target.Label + ": uzerinde bilinen bir istasyon yok");
    }

    // Same three checks PlayerDetector made, kept together so the reason a
    // table refuses is reported instead of read as a dead tap
    private string Clean(TableSet table)
    {
        if (!table.IsDirty)
            return "masa zaten temiz";

        if (!TryGetComponent(out HoldDishAbility dishes))
            return "HoldDishAbility yok, bulasik toplanamaz";

        if (!dishes.CanCollectDishes())
            return "eller dolu, once bulasiklari cope at";

        table.GetCleanedBy(dishes);

        return "masa toplandi";
    }

    // Which station a tap meant, out of every one the ray passed through.
    //
    // Two rules, and both were learned the hard way. RaycastAll does not sort
    // its results, so taking the first Interactable in the array took an
    // arbitrary one -- tapping the salad counter picked the bin. And solid
    // colliders beat triggers, because the triggers here are the deliberately
    // oversized boxes a character walks into; the bin's reaches out across the
    // floor in front of it and would otherwise swallow whatever is behind
    private Interactable PickInteractable(RaycastHit[] hits)
    {
        Interactable solid = null;
        float solidDistance = float.MaxValue;

        Interactable trigger = null;
        float triggerDistance = float.MaxValue;

        float wall = NearestWall(hits);
        int throughWall = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;

            if (collider.transform.IsChildOf(transform))
                continue;

            Interactable candidate = collider.GetComponentInParent<Interactable>();

            if (candidate == null)
                continue;

            // Behind a wall. The ray does not stop at one -- a wall is not an
            // Interactable, so it never appeared in this list at all and never
            // blocked anything. Tapping the back wall picked the station in the
            // next room and walked the player out to it
            if (hits[i].distance > wall + ReachThrough)
            {
                throughWall++;
                continue;
            }

            if (collider.isTrigger)
            {
                if (hits[i].distance >= triggerDistance)
                    continue;

                trigger = candidate;
                triggerDistance = hits[i].distance;

                continue;
            }

            if (hits[i].distance >= solidDistance)
                continue;

            solid = candidate;
            solidDistance = hits[i].distance;
        }

        if (solid != null && trigger != null && solid != trigger)
            Log("iki aday vardi: " + solid.Label + " (kati, " + solidDistance.ToString("0.0") +
                ")  ve  " + trigger.Label + " (trigger, " + triggerDistance.ToString("0.0") +
                ") -- kati secildi");

        // Said out loud, because the other way this goes wrong is a wall the
        // player is meant to reach past -- a serving hatch, a rail. Then this
        // number is high on taps that ought to work, and the fix is the field
        if (throughWall > 0)
            Log(throughWall + " aday duvarin arkasinda kaldi (duvar " + wall.ToString("0.0") +
                " birimde). Yanlissa: Player > Tap To Serve > Reach Through");

        return solid != null ? solid : trigger;
    }

    // How far the first solid thing that is NOT a station is.
    //
    // A wall, a floor, a crate. The floor never gets in the way of a station
    // standing on it -- from this camera the station is always the nearer of
    // the two -- so the only thing this really measures is walls
    private float NearestWall(RaycastHit[] hits)
    {
        float nearest = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;

            // The player stands between the camera and everything else, so
            // their own capsule is the nearest solid thing on most taps. Left
            // in, it would be the wall and nothing would ever be reachable
            if (collider.transform.IsChildOf(transform))
                continue;

            // Triggers are the oversized walk-into boxes, not solid geometry
            if (collider.isTrigger)
                continue;

            if (collider.GetComponentInParent<Interactable>() != null)
                continue;

            nearest = Mathf.Min(nearest, hits[i].distance);
        }

        return nearest;
    }

    private string Holding()
    {
        SpawnableFood held = holdFoodAbility.PeekFood();

        return "  | elde: " + (held == null ? "yok" : held.GetType().Name);
    }

    private string Describe(FryerStation.Result result)
    {
        switch (result)
        {
            case FryerStation.Result.Started: return "kizartma basladi";
            case FryerStation.Result.Frying: return "hala kizariyor, sayaci bekle";
            case FryerStation.Result.Taken: return "patates ele alindi";
            case FryerStation.Result.HandFull: return "el dolu ya da tip uymuyor";
        }

        return "kurulum eksik -- Food Prefab ya da Ready Point bos";
    }

    private string Describe(FridgeDoor.Result result)
    {
        switch (result)
        {
            case FridgeDoor.Result.Taken: return "kapi acildi, icecek ele alindi";
            case FridgeDoor.Result.Opened: return "kapi aciliyor";
            case FridgeDoor.Result.Closed: return "kapi kapaniyor";
            case FridgeDoor.Result.HandFull: return "kapi acildi ama el dolu";
            case FridgeDoor.Result.NoDrink:
                return "kapi acildi, ama Drink Prefab BOS -- " +
                       "Cooked Fast > Buzdolabi: 1 - Kapiyi Kur calistir";
        }

        return "?";
    }

    // Serves once the player has actually walked into range
    private void HandlePendingCustomer()
    {
        if (pendingCustomer == null)
            return;

        // The station's own trigger is the rule: stand behind the counter and
        // the tap goes through, stand anywhere else and it waits
        if (!IsInsideZoneOf(pendingCustomer))
        {
            // Waiting forever with nothing said is how the last four of these
            // went. If the walk finished and the trigger still has not fired,
            // the stand point is outside it and only a log will ever say so
            serveWait += Time.deltaTime;

            // Was four seconds. A message that arrives after the player has
            // already tapped three more times is a message about a different
            // tap than the one they are looking at
            if (serveWait > 1.5f)
            {
                serveWait = 0f;
                ReportServeBlocked();
            }

            return;
        }

        serveWait = 0f;

        Customer customer = pendingCustomer;
        pendingCustomer = null;

        TryServe(customer);
    }

    private float serveWait;

    // Names the reason instead of the symptom.
    //
    // "Not inside the serving area" was true and useless: it is the same
    // sentence whether the stand point is in the wrong place, the trigger is
    // the wrong size, the player never got there, or a no-walk zone is sitting
    // on the spot they were sent to. Each of those is a different fix
    private void ReportServeBlocked()
    {
        FoodServingCustomerManager counter = CounterOf(pendingCustomer);

        if (counter == null)
        {
            Log("SERVIS BEKLIYOR: bu musteriyi hicbir tezgah sahiplenmiyor.\n" +
                "  Player > Tap To Serve > Customer Managers listesine tezgahi ekle");
            return;
        }

        Vector3 serve = counter.ServePosition;
        float away = Vector3.Distance(transform.position.With(y: 0), serve.With(y: 0));

        string reason;

        if (away > 1.5f)
        {
            reason = "oyuncu servis noktasina VARMADI (" + away.ToString("0.0") + " birim uzakta).\n" +
                     "  Yol kapali olabilir: " + counter.name + " > Interactable > Stand Point";
        }
        else if (NoWalkZone.Blocks(transform.position) || NoWalkZone.Blocks(serve))
        {
            // The one this command can create by itself, so it gets named
            // first and with the way out attached
            reason = "servis noktasi bir NoWalkZone'un ICINDE -- alan yanlis yere kurulmus.\n" +
                     "  Cooked Fast > Etkilesim: Tezgah Arkasini Ac";
        }
        else
        {
            reason = "oyuncu servis noktasinda ama tezgahin TRIGGER kutusunun disinda.\n" +
                     "  " + counter.name + " uzerindeki Collider'i buyut ya da\n" +
                     "  " + counter.name + " > Interactable > Stand Point'i kutunun icine tasi";
        }

        Log("SERVIS BEKLIYOR: " + reason +
            "\n  oyuncu           : " + transform.position.ToString("0.00") +
            "\n  servis noktasi   : " + serve.ToString("0.00") +
            "\n  aradaki mesafe   : " + away.ToString("0.00") +
            "\n  NoWalkZone oyuncuda: " + NoWalkZone.Blocks(transform.position) +
            "\n  NoWalkZone servis noktasinda: " + NoWalkZone.Blocks(serve) +
            "\n  girilen alan sayisi: " + zonesInside.Count);
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

        // Every hit, not just the first. A shelf sits on a counter top with the
        // counter, its doors and half the kitchen around it, and whichever of
        // those the ray reaches first is not what the player aimed at
        RaycastHit[] hits = Physics.RaycastAll(
            ray, maxRayDistance, ~0, QueryTriggerInteraction.Collide);

        if (hits.Length <= 0)
        {
            Log("raycast hicbir seye carpmadi");
            return;
        }

        // The player now carries a capsule of their own, and standing between
        // the camera and the floor means the nearest thing the ray meets is
        // themselves -- which would turn every such tap into "walk to where you
        // already are"
        RaycastHit hit = default;
        bool haveHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform))
                continue;

            // A no-walk zone is a region, not a thing in the room. Left in the
            // list it becomes the nearest surface over a whole corner of the
            // floor, and every tap there reports hitting it instead of the
            // ground it is drawn over
            if (hits[i].collider.GetComponent<NoWalkZone>() != null)
                continue;

            if (!haveHit || hits[i].distance < hit.distance)
            {
                hit = hits[i];
                haveHit = true;
            }
        }

        if (!haveHit)
        {
            Log("raycast sadece oyuncunun kendisine carpti");
            return;
        }

        Interactable target = PickInteractable(hits);

        if (target != null)
        {
            DropPendingAction();

            pendingCustomer = null;
            pendingInteractable = target;

            // Straight away, before the walk. The whole point is answering "did
            // that register" in the same frame the finger came off the screen
            target.Pop();

            WalkToPoint(target.StandPosition);

            // The station, not the stand point: the point is where the feet go,
            // the station is what the player should be looking at once there
            FaceOnArrival(target.transform.position);

            Log(target.Label + " secildi -> durma noktasina yuruyor");
            return;
        }

        Vector2 screenPoint = Pointer.current.position.ReadValue();
        Customer customer = FindCustomerFrom(hit, screenPoint);

        if (customer == null)
        {
            // Nothing was aimed at. Whether that means "walk there" or "you
            // tapped nothing" is the one setting on this component that changes
            // how the game is played
            if (!walkOnGroundTap)
            {
                Log("bos zemin: " + hit.collider.name + " -- tiklanabilir degil, durdu");
                return;
            }

            // Refused rather than walked around. This is the floor behind the
            // counter, and the only way to it is round the end and in among the
            // customers -- the one side the serving trigger is not on.
            //
            // Checked here and not inside WalkToPoint on purpose: a stand point
            // is a spot somebody chose, and a zone drawn slightly too wide
            // should never be able to lock the player out of their own counter
            if (NoWalkZone.Blocks(hit.point))
            {
                Log("tezgah arkasi: " + hit.collider.name + " -> gidilmez");
                return;
            }

            // Nothing was aimed at, so this is a plain move order. It cancels
            // anything pending: walking away is how you change your mind
            DropPendingAction();

            pendingCustomer = null;
            pendingInteractable = null;

            // A move order has nothing to look at, so the walk direction is
            // left as the facing -- which is what walking somewhere looks like
            hasFacing = false;

            if (playerAnimator != null)
                playerAnimator.ClearFaceOverride();

            WalkToPoint(hit.point);

            Log("bos zemin: " + hit.collider.name + " -> oraya yuruyor");
            return;
        }

        Log("musteri: " + customer.name +
            " | elde: " + (holdFoodAbility.PeekFood() == null ? "yok" : holdFoodAbility.PeekFood().GetType().Name) +
            " | servis alaninda: " + IsInsideZoneOf(customer));

        DropPendingAction();

        pendingInteractable = null;
        pendingCustomer = customer;

        // The customer, never the counter. Standing behind a till looking at the
        // till instead of at the person being served is the whole complaint
        FaceOnArrival(customer.transform.position);

        FoodServingCustomerManager counter = CounterOf(customer);

        if (counter == null)
        {
            // No counter claims them -- a table customer, or a manager missing
            // from the list. Walking up to them is all that is left
            Log("  bu musteriyi hicbir tezgah sahiplenmiyor, yanina yuruyor");
            WalkTo(customer);
            return;
        }

        // The customer is what was tapped, but the counter is where the player
        // is being sent -- so the counter is what has to acknowledge the tap
        if (counter.TryGetComponent(out Interactable counterPoint))
            counterPoint.Pop();

        Log("  " + counter.name + " tezgahina yuruyor, varinca servis eder");
        WalkToPoint(counter.ServePosition);
    }

    // Asked by the counters themselves at startup. One that nobody answers yes
    // to can only ever cost lives, and it says so rather than waiting to be
    // noticed as hearts vanishing for no visible reason
    public bool Serves(FoodServingCustomerManager counter)
    {
        if (customerManagers == null)
            return false;

        for (int i = 0; i < customerManagers.Length; i++)
        {
            if (customerManagers[i] == counter)
                return true;
        }

        return false;
    }

    private FoodServingCustomerManager CounterOf(Customer customer)
    {
        for (int i = 0; i < customerManagers.Length; i++)
        {
            if (customerManagers[i] != null && customerManagers[i].Contains(customer))
                return customerManagers[i];
        }

        return null;
    }

    // Walks to a spot rather than to a character. Same guards as WalkTo: no
    // agent, no navmesh, or the joystick driving means the player walks over
    // themselves and the tap simply waits for them
    private void WalkToPoint(Vector3 target)
    {
        // Five ways to refuse and they all used to look identical from the
        // outside: the tap was logged as accepted and the player stood still.
        // Each one now says which of the five it was
        if (agent == null)
        {
            Log("YURUYEMEDI: NavMeshAgent yok");
            return;
        }

        if (!agent.enabled)
        {
            Log("YURUYEMEDI: NavMeshAgent kapali");
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Log("YURUYEMEDI: oyuncu NavMesh uzerinde degil -- Window > AI > Navigation'dan Bake et");
            return;
        }

        if (joystickController != null && joystickController.enabled)
        {
            Log("YURUYEMEDI: JoystickPlayerController hala acik, agent ile cekisiyor");
            return;
        }

        // A freshly added ClickToMovePlayerController serializes moveSpeed at
        // zero, hands that to the agent, and the player never moves again
        if (agent.speed <= .01f)
        {
            Log("YURUYEMEDI: agent hizi 0 -- Player > Click To Move Player Controller > Move Speed");
            return;
        }

        if (!NavMesh.SamplePosition(target, out NavMeshHit navHit, 1.5f, NavMesh.AllAreas))
        {
            Log("YURUYEMEDI: hedefin 1.5 birim yakininda NavMesh yok -- durma noktasi zeminde mi?");
            return;
        }

        // Nothing here ever set it, so whatever left it true -- a worker script,
        // a task, a load -- kept it true forever, and an agent that is stopped
        // accepts destinations and ignores them
        agent.isStopped = false;

        if (!agent.SetDestination(navHit.position))
        {
            Log("YURUYEMEDI: SetDestination reddetti, hedef " + navHit.position.ToString("0.0"));
            return;
        }

        walking = true;
        walkTimer = 0f;
        walkFrom = transform.position;
    }

    // Watching "we asked to go somewhere", not "the agent has a path".
    //
    // The first version waited on hasPath, which is exactly the flag that stays
    // false when the path could not be built -- so the one failure most worth
    // reporting was the one it could not see
    private bool walking;
    private float walkTimer;
    private Vector3 walkFrom;

    private void ReportIfStuck()
    {
        if (!walking || agent == null)
            return;

        // Moved at all? Then it works and there is nothing to say
        if (Vector3.Distance(transform.position, walkFrom) > .15f)
        {
            walking = false;
            return;
        }

        walkTimer += Time.deltaTime;

        if (walkTimer < 1f)
            return;

        walking = false;

        CharacterController controller = GetComponent<CharacterController>();

        Log("TAKILDI: hedef verildi, 1 sn'de kimildamadi. Ajanin durumu:" +
            "\n  hasPath          : " + agent.hasPath +
            "\n  pathPending      : " + agent.pathPending +
            "\n  pathStatus       : " + agent.pathStatus +
            "\n  isStopped        : " + agent.isStopped +
            "\n  destination      : " + agent.destination.ToString("0.00") +
            "\n  konum            : " + transform.position.ToString("0.00") +
            "\n  remainingDistance: " + agent.remainingDistance.ToString("0.00") +
            "\n  velocity         : " + agent.velocity.magnitude.ToString("0.00") +
            "\n  speed            : " + agent.speed.ToString("0.00") +
            "\n  acceleration     : " + agent.acceleration.ToString("0.00") +
            "\n  updatePosition   : " + agent.updatePosition +
            "\n  isOnNavMesh      : " + agent.isOnNavMesh +
            "\n  radius / height  : " + agent.radius.ToString("0.00") + " / " +
            agent.height.ToString("0.00") +
            "\n  baseOffset       : " + agent.baseOffset.ToString("0.00") +
            "\n  CharacterController acik: " + (controller != null && controller.enabled) +
            "\n  ClickToMove acik : " + ClickToMoveEnabled() +
            "\n  Joystick acik    : " + (joystickController != null && joystickController.enabled));
    }

    private string ClickToMoveEnabled()
    {
        ClickToMovePlayerController click = GetComponent<ClickToMovePlayerController>();

        return click == null ? "bilesen YOK" : click.enabled.ToString();
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

        // Standing off a customer still puts the player on the customers' side
        // when the approach comes from that side. This is the fallback for a
        // customer no counter claims, and it is worth nothing if it walks the
        // player into the queue
        if (NoWalkZone.Blocks(target))
        {
            Log("YURUYEMEDI: musterinin yani tezgah arkasinda kaliyor");
            return;
        }

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

    // Three tries, cheapest and most certain first.
    //
    // The screen test is last but it is the one that carries the load: a ray in
    // an isometric view lands on the counter, the floor or a cabinet door in
    // front of the customer, and no world space radius around that point is both
    // wide enough to reach them and narrow enough to not catch their neighbour
    public Customer FindCustomerFrom(RaycastHit hit, Vector2 screenPoint)
    {
        Customer direct = hit.collider.GetComponentInParent<Customer>();

        if (direct != null)
            return direct;

        Customer near = FindCustomerNear(hit.point);

        if (near != null)
            return near;

        return FindCustomerOnScreen(screenPoint);
    }

    // Kept for ClickToMovePlayerController, which calls the old signature
    public Customer FindCustomerFrom(RaycastHit hit)
    {
        return FindCustomerFrom(hit, Pointer.current == null
            ? Vector2.zero
            : Pointer.current.position.ReadValue());
    }

    public Customer FindCustomerOnScreen(Vector2 screenPoint)
    {
        if (gameCamera == null)
            return null;

        Customer nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < customerManagers.Length; i++)
        {
            if (customerManagers[i] == null)
                continue;

            Customer candidate = customerManagers[i].FindNearestOnScreen(
                gameCamera, screenPoint, tapScreenRadius, tapAimHeight);

            if (candidate == null)
                continue;

            Vector3 screen = gameCamera.WorldToScreenPoint(
                candidate.transform.position + Vector3.up * tapAimHeight);

            float distance = Vector2.Distance(screenPoint, new Vector2(screen.x, screen.y));

            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    // Select the Player in play mode to see exactly what a tap covers. The
    // spheres are the world radius; the screen radius is the number that
    // actually decides, and it cannot be drawn in the scene
    private void OnDrawGizmosSelected()
    {
        if (customerManagers == null)
            return;

        Gizmos.color = new Color(0f, 1f, .4f, .35f);

        for (int i = 0; i < customerManagers.Length; i++)
        {
            if (customerManagers[i] == null)
                continue;

            foreach (Transform child in customerManagers[i].transform)
            {
                Customer customer = child.GetComponentInChildren<Customer>();

                if (customer == null)
                    continue;

                Gizmos.DrawSphere(customer.transform.position + Vector3.up * tapAimHeight, tapRadius);
            }
        }
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

        // Raw food never reaches a customer, whatever they ordered
        if (!heldFood.CanBeServed)
        {
            Log(heldFood.GetType().Name + " cig -- once ocakta pisir");
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

        // Handing a plate over is the same motion as putting one down, and this
        // is the one place a customer is actually served rather than refused
        if (playerAnimator != null)
            playerAnimator.PlayAction(PlayerAnimator.Action.Pan);

        // Collect first, pay second. CollectFood is what stops the patience
        // clock, and the multiplier has to be read after it has stopped or the
        // score is taken from a clock still running
        customer.CollectFood(foodToServe);

        Pay(customer);

        if (!customer.NeedsMoreFood())
            SendCustomerHome(customer);

        return true;
    }

    // Two multipliers, and they mean different things. The stat is what the
    // player has bought; the mood is what they have just earned
    // Hands the amount back as well as banking it. The bubble shows the player
    // what a happy customer was worth, and that only means anything if it is the
    // same number that went into the till rather than one worked out twice
    // Rung up on every item, but only paid out when the order is finished --
    // RingUp answers 0 until then. That is the same moment the bubble shows
    // what it was worth, so the money in the air and the number over their head
    // arrive together and say the same thing
    private void Pay(Customer customer)
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
