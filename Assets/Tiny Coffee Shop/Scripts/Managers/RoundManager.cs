using System;
using System.Collections;
using UnityEngine;

// Runs the 50 rounds.
//
// The counters already know how to spawn a customer every N seconds; what they
// did not know was when to stop. A round is that: a count and a pace, handed to
// every counter at once, and then a wait until all of them are empty again.
//
// Waits for the queue to CLEAR, not for the last customer to be spawned. A
// round that ends when the last one walks in ends while they are still standing
// there waiting to be served, and the next wave arrives on top of them
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    // Read by the counters in Start, so it has to be answerable from Awake --
    // which is why this is a property on the type and not a scene lookup
    public static bool Exists => Instance != null;

    [Header(" Elements ")]
    [Tooltip("Raund 1'den 50'ye, sirayla")]
    [SerializeField] private RoundData[] rounds;

    [Tooltip("Wave'i paylasan tezgahlar. Bos birakilirsa sahnedekiler bulunur")]
    [SerializeField] private FoodServingCustomerManager[] counters;

    [Header(" Settings ")]
    [Tooltip("Iki raund arasi nefes payi, saniye. 0 = varsayilan 3")]
    [SerializeField] private float breakBetweenRounds = 3f;

    [Tooltip("Kacinci raunddan baslasin. Test icin")]
    [SerializeField][Min(1)] private int startRound = 1;

    [Tooltip("HAZIRLAN yazisi kac saniye durur. Bu bitmeden musteri gelmez. " +
             "0 = varsayilan 2")]
    [SerializeField] private float introSeconds = 2f;

    private float BreakBetweenRounds => breakBetweenRounds > .01f ? breakBetweenRounds : 3f;
    private float IntroSeconds => introSeconds > .01f ? introSeconds : 2f;

    public int Round { get; private set; }
    public int RoundCount => rounds == null ? 0 : rounds.Length;
    public RoundData Current => Valid(Round) ? rounds[Round - 1] : null;

    // Announced, then started. Two events rather than one because the banner
    // has to be up BEFORE anybody walks in -- a "get ready" that arrives with
    // the first customer is not a warning, it is a caption
    public static event Action<int> RoundAnnounced;
    public static event Action<int> RoundStarted;
    public static event Action<int> RoundFinished;
    public static event Action AllRoundsFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (counters == null || counters.Length <= 0)
            counters = FindObjectsByType<FoodServingCustomerManager>(FindObjectsSortMode.None);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (rounds == null || rounds.Length <= 0)
        {
            Debug.LogWarning(name + ": raund listesi bos -- hic musteri gelmez.\n" +
                             "  Cooked Fast > Raund: 50 Raundu Uret", this);
            return;
        }

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Round = Mathf.Clamp(startRound, 1, rounds.Length);

        // Held at the door until whatever is in the way lets go -- the start
        // screen, the game over screen. Asked as a static rather than wired,
        // so a scene with no screens at all simply never waits
        yield return new WaitUntil(() => !GameScreens.Blocking);

        while (Valid(Round))
        {
            RoundAnnounced?.Invoke(Round);

            // Unscaled, so the banner still counts down if anything has paused
            // the game underneath it
            yield return new WaitForSecondsRealtime(IntroSeconds);

            Begin(rounds[Round - 1]);

            RoundStarted?.Invoke(Round);

            yield return new WaitUntil(Clear);

            RoundFinished?.Invoke(Round);

            Round++;

            if (!Valid(Round))
                break;

            yield return new WaitForSeconds(BreakBetweenRounds);
        }

        AllRoundsFinished?.Invoke();
    }

    // The wave is shared out, not repeated. Two counters and a wave of nine is
    // nine people between them -- handing nine to each would quietly double
    // every number in the table
    private void Begin(RoundData data)
    {
        if (data == null || counters == null || counters.Length <= 0)
            return;

        int live = 0;

        for (int i = 0; i < counters.Length; i++)
        {
            if (counters[i] != null)
                live++;
        }

        if (live <= 0)
            return;

        int share = data.TotalCustomers / live;
        int extra = data.TotalCustomers - share * live;

        for (int i = 0; i < counters.Length; i++)
        {
            if (counters[i] == null)
                continue;

            // The remainder goes to the first counter rather than being lost to
            // integer division. Three counters and a wave of ten is 4/3/3
            int mine = share + (extra > 0 ? 1 : 0);

            if (extra > 0)
                extra--;

            counters[i].BeginRound(mine, data.SpawnInterval, data.MaxOrderTypes);
        }
    }

    // Work still owed across every counter.
    //
    // Asked by a counter about to hand a new customer their clock. One player
    // serves the whole kitchen, so a queue at the other end is time this
    // customer will spend waiting whether their own counter is busy or not
    public float WorkInFlight()
    {
        if (counters == null)
            return 0f;

        float total = 0f;

        for (int i = 0; i < counters.Length; i++)
        {
            if (counters[i] != null)
                total += counters[i].QueuedWork();
        }

        return total;
    }

    private bool Clear()
    {
        if (counters == null)
            return true;

        for (int i = 0; i < counters.Length; i++)
        {
            if (counters[i] != null && !counters[i].RoundClear)
                return false;
        }

        return true;
    }

    private bool Valid(int round)
    {
        return rounds != null && round >= 1 && round <= rounds.Length;
    }
}
