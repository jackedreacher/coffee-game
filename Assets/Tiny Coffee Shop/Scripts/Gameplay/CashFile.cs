using System.Collections;
using NaughtyAttributes;
using Tabsil.Sijil;
using UnityEngine;

// The till. It has two ways of paying out and only one of them is on.
//
// The old one: bills stack on the floor in a grid and wait for the player to
// walk over them. That is where the tower against the wall came from -- one
// bill per unit of revenue, saved between sessions, and the pile grew faster
// than anyone walked into it.
//
// The new one: the bills leave the customer, rise, and fly into the counter at
// the top of the screen. The money is banked when a bill lands, so the number
// on screen goes up at the moment something visibly arrives in it
[RequireComponent(typeof(GuidGenerator))]
public class CashFile : MonoBehaviour, IWantToBeSaved
{
    [Header(" Components ")]
    private GuidGenerator guidGenerator;

    [Header(" Elements ")]
    [SerializeField] private GameObject cashPrefab;

    [Header(" Settings ")]
    [SerializeField] private Vector2Int gridSize;
    [SerializeField] private Vector3 gridSpacing;

    [Header(" Para ")]
    // Ters yazildi bilerek: sahnede zaten kayitli bir bilesene eklenen alan
    // dosyada yok, ve olmayan bir alan her zaman false gelir. Yani "false =
    // yeni davranis" olmasi, dogru tarafta olmayi tesadufe birakmiyor
    [Tooltip("Acikken eski hali: para yere yigilir, oyuncu ustunden gecince toplanir. " +
             "Kapaliyken banknotlar dogruca ustteki sayaca ucar")]
    [SerializeField] private bool pileOnFloor;

    [Tooltip("Bir satista kac banknot ucar. Tutar bunlara bolunur, kac tane olursa olsun toplam ayni")]
    [SerializeField] private int billsPerSale = 5;

    [SerializeField] private float flightTime = .7f;

    [Tooltip("Sayaca gitmeden once ne kadar yukselsin. Ekran yuksekliginin orani")]
    [SerializeField] private float liftHeight = .1f;

    [Tooltip("Ayni anda kalkan banknotlarin yana acilmasi. Ekran genisliginin orani")]
    [SerializeField] private float spread = .05f;

    [Tooltip("Ucus boyunca banknot kameraya ne kadar yaklassin. " +
             "1 = hic. Kucultmek duvarin ya da tezgahin arkasinda kalmasini onler")]
    [SerializeField] private float approach = .5f;

    // Read through these, not off the fields. Every one of them is new on a
    // component that is already saved in the scene, and a zero here is not a
    // setting anybody chose -- it is the field arriving without its default.
    // A flight lasting no time is the one that fails hardest: it teleports
    private float FlightTime => flightTime > .01f ? flightTime : .7f;
    private int BillsPerSale => billsPerSale > 0 ? billsPerSale : 5;
    private float Approach => approach > 0f ? Mathf.Min(approach, 1f) : .5f;

    private Vector3[] basePositions;
    private int index;
    private bool loaded;

    private Camera view;

    private void Awake()
    {
        guidGenerator = GetComponent<GuidGenerator>();
        StoreBasePositions();
    }

    private void Start()
    {
        if (!loaded)
            Load();
    }

    private void StoreBasePositions()
    {
        basePositions = new Vector3[gridSize.x * gridSize.y];

        Vector3 startPosition = transform.position
            - Vector3.right * gridSpacing.x * gridSize.x / 2
            - Vector3.forward * gridSpacing.z * gridSize.y / 2;

        startPosition += gridSpacing / 2;

        for (int z = 0; z < gridSize.y; z++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Vector3 targetPosition = startPosition
                    + Vector3.right * x * gridSpacing.x
                    + Vector3.forward * z * gridSpacing.z;

                int i = x + z * gridSize.x;
                basePositions[i] = targetPosition;
            }
        }
    }

    private Vector3 GetTargetGridPosition(int targetIndex)
    {
        int elevationIndex = targetIndex / basePositions.Length;
        float y = elevationIndex * gridSpacing.y;
        int basePositionIndex = targetIndex % basePositions.Length;
        return basePositions[basePositionIndex] + Vector3.up * y;
    }

    // The till's own position is the fallback origin, for the callers that have
    // nobody to take the money from
    public void GenerateCash(int amount) => GenerateCash(amount, transform.position);

    public void GenerateCash(int amount, Vector3 origin)
    {
        if (amount <= 0)
            return;

        if (pileOnFloor)
        {
            Pile(amount, true);
            return;
        }

        Fly(amount, origin);
    }

    private void Pile(int amount, bool save)
    {
        if (basePositions == null)
            StoreBasePositions();

        for (int i = 0; i < amount; i++)
        {
            Vector3 targetPosition = GetTargetGridPosition(index + i);
            GameObject cash = Instantiate(cashPrefab, targetPosition, Quaternion.identity, transform);
            if (cash.TryGetComponent(out Collider col))
                col.enabled = false;
        }

        index += amount;

        if (save)
            Save();
    }

    // A handful of bills, not one per coin. The amount is split across them so
    // the total banked is the revenue exactly, whatever billsPerSale is set to
    private void Fly(int amount, Vector3 origin)
    {
        int bills = Mathf.Clamp(BillsPerSale, 1, Mathf.Max(1, amount));
        int share = amount / bills;
        int first = share + amount - share * bills;

        for (int i = 0; i < bills; i++)
            StartCoroutine(FlyOne(origin, i == 0 ? first : share, i * .07f));
    }

    private IEnumerator FlyOne(Vector3 origin, int value, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Camera eye = View();

        // The picture can fail; the income cannot. No camera and no prefab
        // still pays -- it just pays without anything to watch
        if (eye == null || cashPrefab == null)
        {
            MoneyCounter.Instance.Deposit(value);
            yield break;
        }

        GameObject bill = Instantiate(cashPrefab, origin, Quaternion.identity);

        if (bill.TryGetComponent(out Collider col))
            col.enabled = false;

        Vector3 restScale = bill.transform.localScale;

        // Flown in screen space, because that is what the effect is about: it
        // starts on the customer and ends inside a counter pinned to the top of
        // the screen. Doing it in world space means guessing which world
        // direction happens to be "up" and "towards the counter" from here
        float lift = liftHeight * Screen.height;
        float sideways = Random.Range(-spread, spread) * Screen.width;
        float spin = Random.Range(-220f, 220f);

        float flight = FlightTime;
        float age = 0f;

        while (age < flight && bill != null)
        {
            age += Time.deltaTime;
            float p = Mathf.Clamp01(age / flight);

            // Both ends recomputed every frame: the camera follows the player,
            // so neither the spot it left nor the counter stays put
            Vector3 from = eye.WorldToScreenPoint(origin);
            Vector2 to = MoneyCounter.Instance.ScreenPoint;

            // Coming towards the camera as it goes, so no wall or counter along
            // the way gets to be in front of it
            float depth = Mathf.Lerp(from.z, from.z * Approach, p);

            // p * p leaves slowly and arrives fast, which is what reads as the
            // bill being pulled in rather than merely travelling
            Vector2 flat = Vector2.Lerp(from, to, p * p);
            flat.y += Mathf.Sin(p * Mathf.PI) * lift;
            flat.x += Mathf.Sin(p * Mathf.PI) * sideways;

            bill.transform.position = eye.ScreenToWorldPoint(new Vector3(flat.x, flat.y, depth));

            // Getting closer magnifies it under a perspective camera, so that
            // much is taken back out -- the shrinking left over is the intended
            // part, the bill disappearing into the counter
            float perspective = eye.orthographic || from.z <= 0f ? 1f : depth / from.z;

            bill.transform.localScale = restScale * perspective * Mathf.Lerp(1f, .25f, p * p);
            bill.transform.Rotate(eye.transform.forward, spin * Time.deltaTime, Space.World);

            yield return null;
        }

        if (bill != null)
            Destroy(bill);

        MoneyCounter.Instance.Deposit(value);
    }

    private Camera View()
    {
        if (view == null)
            view = Camera.main;

        return view;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Nothing on the floor to walk into
        if (!pileOnFloor)
            return;

        if (!other.TryGetComponent(out PlayerController _))
            return;

        AnimateCashToPlayer(other.transform);
        index = 0;
        Save();
    }

    private void AnimateCashToPlayer(Transform playerTransform)
    {
        if (transform.childCount <= 0)
            return;

        // Cached: unparenting below shrinks childCount as we go, and the
        // staggering has to stay based on the original count
        int cashCount = transform.childCount;

        float duration = 2f;
        float delayStep = duration / cashCount;
        delayStep = Mathf.Min(delayStep, 0.01f);

        for (int i = cashCount - 1; i >= 0; i--)
        {
            Transform cash = transform.GetChild(i);
            float delay = (cashCount - 1 - i) * delayStep;
            delay = Mathf.Min(delay, duration);

            // Unparent before animating: otherwise cash generated while these
            // are still flying gets mixed into the pile's children, and the
            // next collection grabs bills that are already being destroyed
            cash.parent = null;

            ArcAnimator.Animate(cash, playerTransform, 1f, delay, 3f, HandleCashMovedAlongArc);
        }
    }

    private void HandleCashMovedAlongArc(GameObject cash)
    {
        // ArcAnimator passes null when the transform vanished mid-flight
        if (cash == null)
            return;

        CurrencyManager.instance.AddCurrency(2);
        Destroy(cash);
    }

    public void Save()
    {
        if (guidGenerator == null)
            guidGenerator = GetComponent<GuidGenerator>();

        // Zero rather than nothing while flying: the pile already in the file
        // is thousands of bills high, and not writing over it leaves it there
        // to be rebuilt the moment anyone turns the floor mode back on
        Sijil.Save(this, guidGenerator.GUID, pileOnFloor ? index : 0);
    }

    public void Load()
    {
        loaded = true;

        if (guidGenerator == null)
            guidGenerator = GetComponent<GuidGenerator>();

        if (!pileOnFloor)
        {
            index = 0;
            Save();
            return;
        }

        if (!Sijil.TryLoad(this, guidGenerator.GUID, out object _index))
            return;

        Pile((int)_index, false);
    }

    [Button]
    private void GenerateOneCash()
    {
        GenerateCash(1);
    }
}
