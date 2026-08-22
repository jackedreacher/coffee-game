using UnityEngine;

public class HoldFoodAbility : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;

    public Plateau Plateau => plateau;
    public bool IsPlateauEmpty => plateau.IsEmpty;
    public bool IsPlateauFull => plateau.IsFull;
    public bool IsPlateauDirty => plateau.IsDirty;
    public bool IsPlateauActive => plateau.gameObject.activeInHierarchy;

    [Header(" Timer ")]

    // How long after reaching a station before the item actually changes hands.
    //
    // Was a const at a tenth of a second, which is why the item appeared to pop
    // into the hand: the pickup animation is a whole reach -- the arm has to
    // travel out, close and come back -- and a tenth of a second is over while
    // the hand is still on its way. The item was in the hand before the hand
    // got there.
    //
    // Serialized rather than raised and left, because the right number is the
    // one that matches whichever clip is on PickUp, and that has changed twice
    // already. Time the reach in the animation window and put that here
    [Tooltip("Istasyona varinca esyanin ele gecmesi kac saniye sursun. " +
             "Alma animasyonundaki elin uzanip yakaladigi ana denk gelmeli. " +
             "Kucuk = esya elden once belirir, buyuk = el bos donuyormus gibi olur")]
    [SerializeField] private float canGrabFoodDelay = .35f;

    private float grabFoodTimer;
    private float dropFoodTimer;

    private void Start()
    {
        grabFoodTimer = canGrabFoodDelay;
        dropFoodTimer = canGrabFoodDelay;
    }

    public void SetPlateauCapacity(int capacity)
    {
        plateau.UpdateMaxCapacity(capacity);
    }

    // The two delays below pace someone STANDING in a trigger, where the handler
    // runs every frame and would otherwise empty a counter instantly. A tap gets
    // one call and one only, so without opening the gate first the first tap
    // works, the second merely ticks the timer, and the station appears to
    // answer every other press
    public void ReadyForOneTap()
    {
        grabFoodTimer = canGrabFoodDelay;
        dropFoodTimer = canGrabFoodDelay;
    }

    // The first food and the plateau must read as one arrival. Add PopIn while
    // the plateau is inactive, then enable it and parent the food in the same
    // frame. OnEnable starts the plateau at zero scale; because the food is its
    // child before that frame is rendered, both grow together and there is no
    // hand-bone sweep from the station into the character.
    private void PopOntoPlateau(SpawnableFood food)
    {
        if (food == null)
            return;

        // Must happen before PopIn learns/zeros the tray scale. Parent repair
        // is idempotent; placement is deliberately NOT touched here. The Hand
        // Adjuster placement is the source of truth and must survive every
        // take/drop cycle unchanged.
        PlateauLevel.StabiliseFingerMount(plateau.transform);

        bool opening = !plateau.gameObject.activeSelf;

        if (opening)
            PopIn.Ensure(plateau.gameObject);

        plateau.gameObject.SetActive(true);
        plateau.Push(food);
    }

    public SpawnableFood PeekFood()
    {
        return plateau.Peek();
    }

    // Hands the top item over and hides the tray once it runs dry, the same
    // bookkeeping HandleFoodDropZone does when dropping onto a counter
    // Quiet exists because some callers pop SPECULATIVELY.
    //
    // A swap has to empty both hands before it can ask either of them whether
    // it will accept -- a full container answers no to everything -- so the pop
    // happens before anyone knows the swap will go through, and it is undone
    // again if it does not. The item goes back; the sound does not. Sounding
    // the mechanical pop rather than the finished action is what put two
    // noises on a tap that did nothing
    public SpawnableFood PopFood(bool quiet = false)
    {
        SpawnableFood food = plateau.Pop();

        if (food == null)
            return null;

        if (!quiet)
            Gave();

        if (plateau.IsEmpty)
            plateau.gameObject.SetActive(false);

        return food;
    }

    // The two hand-off noises, in one place each.
    //
    // Not left to the stations to remember: there are eleven of them and a new
    // one added next month would be silent, which is the kind of gap nobody
    // notices until the build is on a phone. Everything that reaches the hand
    // goes through a handful of lines and those lines are here.
    //
    // The drink exception lives here for the same reason. It is not the drop
    // zone's business that the fridge has its own sound, and a check spread
    // over four call sites is four places to get it wrong
    private static void Took(SpawnableFood food)
    {
        if (food is Drink)
            return;

        SoundManager.Play(SoundManager.Sound.ItemTaken);
    }

    private static void Gave()
    {
        SoundManager.Play(SoundManager.Sound.ItemGiven);
    }

    // A swap is ONE action, not a give plus a take. Two clips starting on the
    // same frame read as a double tap rather than as an exchange, so only the
    // arrival sounds -- what the player looks at afterwards is what is now in
    // their hand
    public static void Swapped(SpawnableFood arriving)
    {
        Took(arriving);
    }

    public void HandleFoodSpawnerStation(FoodSpawnerStation station)
    {
        // The recipe is the one case where a tray may hold two different things.
        // Everywhere else mixing is refused, and rightly so: a stack of cups and
        // pizzas has no sensible spacing
        bool forRecipe = FitsRecipe(station.SpawnableFoodPrefab);

        if (plateau.IsFull && !forRecipe)
            return;

        // The activeInHierarchy guard matters: while the plateau is off its
        // FoodPositions have not run Awake, so the emptiness check lies
        if (plateau.gameObject.activeInHierarchy &&
            !plateau.CanAccept(station.SpawnableFoodPrefab) &&
            !forRecipe)
            return;

        if (grabFoodTimer < canGrabFoodDelay)
        {
            grabFoodTimer += Time.deltaTime;
            return;
        }

        grabFoodTimer = 0;

        SpawnableFood foodToGrab = station.Pop();

        if (foodToGrab == null)
            return;

        Took(foodToGrab);

        if (forRecipe)
        {
            AbsorbIntoBurger(foodToGrab);
            return;
        }

        PopOntoPlateau(foodToGrab);
    }

    // ---- building a burger in the hand -------------------------------------

    [Header(" Tarif ")]
    [Tooltip("Elde birlesecek malzemeler. Sira onemsiz")]
    [SerializeField] private SpawnableFood[] recipeParts;

    [Tooltip("Malzemelerin uzerine eklendigi yemek -- katmanlari tek tek acilir")]
    [SerializeField] private Burger recipeResult;

    private bool IsRecipePart(SpawnableFood food)
    {
        if (food == null || recipeResult == null || recipeParts == null)
            return false;

        for (int i = 0; i < recipeParts.Length; i++)
        {
            if (recipeParts[i] != null && recipeParts[i].GetType() == food.GetType())
                return true;
        }

        return false;
    }

    // The burger being carried, or null. Only ever one thing on the tray, so
    // none of the capacity juggling an assembled-at-the-end recipe needed
    private Burger HeldBurger()
    {
        if (!plateau.gameObject.activeInHierarchy)
            return null;

        return plateau.Peek() as Burger;
    }

    // A part may be picked up when the hand is empty, or when it is holding a
    // burger that does not have this layer yet. Anything else on the tray and
    // the ordinary "one food type" rule stands
    private bool FitsRecipe(SpawnableFood incoming)
    {
        // Checked here as well as in Burger.Wants, because this is the branch
        // that starts a NEW burger from a loose part -- Wants is only asked once
        // one already exists
        if (incoming != null && incoming.IsBurnt)
            return false;

        if (!IsRecipePart(incoming))
            return false;

        Burger held = HeldBurger();

        if (held != null)
            return held.Wants(incoming);

        return !plateau.gameObject.activeInHierarchy || plateau.IsEmpty;
    }

    // The ingredient never reaches the tray. It is read for its type, switched
    // on inside the burger and thrown away -- which is why picking bread up
    // first shows a bun rather than a loaf
    private void AbsorbIntoBurger(SpawnableFood part)
    {
        Burger held = HeldBurger();

        if (held == null)
        {
            held = Instantiate(recipeResult);

            PopOntoPlateau(held);
        }

        held.Add(part);

        Destroy(part.gameObject);
    }

    // The oven is the one station that goes both ways. Taking is tried first --
    // if something is ready, that is what the player came for -- and giving is
    // only tried when there was nothing to take, so walking up holding raw meat
    // while a cooked one waits does not deadlock on the plateau refusing to mix
    public void HandleCookingStation(CookingStation station)
    {
        if (TryTakeCooked(station))
            return;

        TryPutInRaw(station);
    }

    private bool TryTakeCooked(CookingStation station)
    {
        if (!station.HasCooked)
            return false;

        // The piece in the pan, not the prefab it came from.
        //
        // This asked the prefab, and that is why burnt meat still went into a
        // burger: the prefab is never burnt, so the burnt check inside
        // FitsRecipe was answering about the wrong object every time. The real
        // piece then got absorbed and destroyed, and what the player ended up
        // holding was the burger's own patty mesh -- which nothing had blackened
        SpawnableFood waiting = station.PeekCooked();

        if (waiting == null)
            waiting = station.CookedFoodPrefab;

        // Cooked meat is a recipe part like any other, so walking up to the oven
        // holding bread has to work the same way as walking up to the bread
        bool forRecipe = FitsRecipe(waiting);

        if (plateau.IsFull && !forRecipe)
            return false;

        // The activeInHierarchy guard matters: while the plateau is off its
        // FoodPositions have not run Awake, so the emptiness check lies
        if (plateau.gameObject.activeInHierarchy &&
            !plateau.CanAccept(waiting) &&
            !forRecipe)
            return false;

        if (grabFoodTimer < canGrabFoodDelay)
        {
            grabFoodTimer += Time.deltaTime;
            return true;
        }

        grabFoodTimer = 0;

        SpawnableFood cooked = station.TakeCooked();

        if (cooked == null)
            return false;

        // Checked once more against what actually came out. The decision above
        // was made a frame earlier and on a different question; this is the last
        // gate before the piece is destroyed inside a burger
        Took(cooked);

        if (forRecipe && !cooked.IsBurnt)
        {
            AbsorbIntoBurger(cooked);
            return true;
        }

        PopOntoPlateau(cooked);

        return true;
    }

    private bool TryPutInRaw(CookingStation station)
    {
        if (!plateau.gameObject.activeSelf)
            return false;

        if (plateau.IsDirty)
            return false;

        // Peek rather than Pop: refusing has to leave the plateau exactly as it
        // was, the same way the drop zone does it
        if (!station.CanAccept(plateau.Peek()))
            return false;

        if (dropFoodTimer < canGrabFoodDelay)
        {
            dropFoodTimer += Time.deltaTime;
            return true;
        }

        dropFoodTimer = 0;

        SpawnableFood raw = plateau.Pop();

        if (raw == null)
            return false;

        station.PutIn(raw);

        if (plateau.IsEmpty)
            plateau.gameObject.SetActive(false);

        return true;
    }

    // Would taking this join it onto the burger already in the hand?
    //
    // Not the same question as CanTake, which an empty hand answers yes to for
    // anything. The shelf needs this one to know whether taking should beat its
    // ordinary "full hand puts down" rule
    public bool Merges(SpawnableFood waiting)
    {
        Burger held = HeldBurger();

        if (held == null || waiting == null)
            return false;

        // Parked halves come back as burgers, not as loose ingredients
        if (waiting is Burger other)
            return held.CanTake(other);

        return held.Wants(waiting);
    }

    // Asked before anything is taken off a shelf, so a refusal costs nothing
    public bool CanTake(SpawnableFood food)
    {
        if (food == null)
            return false;

        // Anything joining the burger takes no room on the tray -- it is read
        // for its layers and destroyed -- so IsFull has nothing to say about it
        if (Merges(food) || FitsRecipe(food))
            return true;

        if (plateau.IsFull)
            return false;

        // The activeInHierarchy guard matters: while the plateau is off its
        // FoodPositions have not run Awake, so the emptiness check lies
        return !plateau.gameObject.activeInHierarchy || plateau.CanAccept(food);
    }

    // Quiet exists for ROLL-BACKS. The shelf pops an item out of the hand, finds
    // the swap refused and pushes the same item straight back -- nothing was
    // taken, the hand simply never let go, and announcing that as a pickup would
    // be a sound describing an event that did not happen
    public bool TryPush(SpawnableFood food, bool quiet = false)
    {
        if (!CanTake(food))
            return false;

        if (!quiet)
            Took(food);

        if (food is Burger incoming && Merges(food))
        {
            HeldBurger().Take(incoming);
            Destroy(incoming.gameObject);

            return true;
        }

        if (FitsRecipe(food))
        {
            AbsorbIntoBurger(food);
            return true;
        }

        PopOntoPlateau(food);

        return true;
    }

    // Everything on the tray at once. The bin is the only caller that wants this
    // -- every other hand-off takes one item and cares which one it is
    public SpawnableFood[] DumpAll()
    {
        if (!plateau.gameObject.activeSelf)
            return new SpawnableFood[0];

        SpawnableFood[] dumped = plateau.PopAll();

        if (dumped.Length <= 0)
            return dumped;

        // Once, not once per item. Emptying a full tray into the bin is one
        // action and four overlapping copies of the same clip is just louder
        Gave();

        plateau.gameObject.SetActive(false);

        return dumped;
    }

    // A drop zone used to be one way: things went onto it and only a customer
    // ever took them off. So a hand holding the wrong thing could do nothing
    // with a counter holding the right one -- the tap was simply ignored, and
    // the only way out was the bin.
    //
    // Three cases now, decided by what is in each hand rather than by a mode:
    // empty hand takes, full hand puts down, and a full hand meeting a full
    // counter swaps. The last one is the whole point, and it falls out of the
    // first two rather than being a fourth rule
    // Standing in the zone. PlayerDetector calls this every frame the player is
    // inside the trigger, which is why it may only ever put things DOWN: taking
    // and swapping have to be asked for, or an item would shuttle between the
    // counter and the hand for as long as somebody stood there
    public void HandleFoodDropZone(FoodDropZone dropZone)
    {
        HandleFoodDropZone(dropZone, false);
    }

    public void HandleFoodDropZone(FoodDropZone dropZone, bool byTap)
    {
        if (plateau.IsDirty)
            return;

        bool handEmpty = !plateau.gameObject.activeSelf || plateau.IsEmpty;

        if (handEmpty)
        {
            if (byTap)
                TakeFromZone(dropZone);

            return;
        }

        // The drop is refused -- no room, or the wrong type for this counter.
        // Which is exactly when there is something here worth trading for
        if (dropZone.IsFull || !dropZone.CanAcceptFood(plateau.Peek()))
        {
            if (byTap)
                SwapWithZone(dropZone);

            return;
        }

        if (dropFoodTimer < canGrabFoodDelay)
        {
            dropFoodTimer += Time.deltaTime;
            return;
        }

        dropFoodTimer = 0;

        SpawnableFood food = plateau.Pop();

        if (food == null)
            return;

        Gave();

        dropZone.Push(food);

        if (plateau.IsEmpty)
            plateau.gameObject.SetActive(false);
    }

    private bool TakeFromZone(FoodDropZone dropZone)
    {
        SpawnableFood waiting = dropZone.Peek();

        // Peek and check before popping, the same as everywhere else here. A
        // refusal has to leave the counter exactly as it was
        if (waiting == null || !CanTake(waiting))
            return false;

        SpawnableFood taken = dropZone.Pop();

        return taken != null && TryPush(taken);
    }

    // Both emptied before either is asked whether it will accept.
    //
    // Asking first is asking the wrong question: a full container answers no to
    // everything, so "will the hand take this" gets answered by whatever is
    // already in it. Emptying both and then asking is the only order in which
    // the question means what it is meant to
    private bool SwapWithZone(FoodDropZone dropZone)
    {
        SpawnableFood waiting = dropZone.Peek();
        SpawnableFood held = plateau.Peek();

        if (waiting == null || held == null)
            return false;

        // Burnt food does not get parked and forgotten about. The point of
        // burning it is the walk to the bin
        if (held.IsBurnt)
            return false;

        SpawnableFood given = PopFood(quiet: true);

        if (given == null)
            return false;

        SpawnableFood taken = dropZone.Pop();

        if (taken == null)
        {
            TryPush(given, quiet: true);
            return false;
        }

        if (CanTake(taken) && dropZone.CanAcceptFood(given))
        {
            TryPush(taken, quiet: true);
            dropZone.Push(given);

            Swapped(taken);

            return true;
        }

        // Refused after all. Both go back exactly where they were -- nothing
        // here may destroy an item by half completing, and nothing here may
        // announce an exchange that was undone
        dropZone.Push(taken);
        TryPush(given, quiet: true);

        return false;
    }
}
