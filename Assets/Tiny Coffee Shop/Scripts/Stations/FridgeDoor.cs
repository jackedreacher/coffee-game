using UnityEngine;

// A fridge door that swings open when the fridge is tapped.
//
// The swing is a rotation in the PARENT's space, not the door's own. The door
// mesh comes out of the FBX turned -90 on X, so its local up points off
// sideways -- rotating around that would lay the door flat on the floor rather
// than swinging it. Left multiplying by a rotation built from a parent space
// axis is what keeps "up" meaning what everyone looking at the kitchen thinks
// it means.
//
// The closed pose is remembered as numbers rather than read off the transform
// at startup, so previewing the swing in the editor and saving the scene mid
// swing cannot lose the door's real closed position
public class FridgeDoor : MonoBehaviour
{
    [Header(" Elements ")]
    [Tooltip("Donecek kapi. Bos birakilirsa adinda 'door' gecen cocuk aranir")]
    [SerializeField] private Transform door;

    [Header(" Acilis ")]
    [Tooltip("Kapi kac derece acilsin. Ters tarafa aciliyorsa basina eksi koy")]
    [SerializeField] private float openAngle = -105f;

    [Tooltip("Acilma hizi -- saniyede kac derece")]
    [SerializeField] private float speed = 320f;

    [Tooltip("Kac saniye sonra kendi kapansin. 0 = kendi kapanmaz, tekrar tikla")]
    [SerializeField] private float autoCloseAfter = 2.5f;

    [Header(" Mentese ")]
    [Tooltip("Dondugu eksen, dolabin kendi eksenlerinde. Yukari = kapi yana acilir")]
    [SerializeField] private Vector3 hingeAxis = Vector3.up;

    [Tooltip("Mentese kapinin pivotunda degilse buradan kaydir. Genelde sifir kalir")]
    [SerializeField] private Vector3 hingeOffset;

    [Header(" Icecek ")]
    // No timer, and that is the point of the fridge existing next to the fryer:
    // the fryer is somewhere you start something and come back to, this is
    // somewhere you grab from. One tap is the door and the can together
    [Tooltip("Dolaptan cikan icecek. Bos birakilirsa sadece kapi acilir")]
    [SerializeField] private SpawnableFood drinkPrefab;

    [Header(" Oyuncu ")]
    [Tooltip("Oyuncunun gelip duracagi nokta. Bos ise dolabin kendisi")]
    [SerializeField] private Transform standPoint;

    [Tooltip("Isaretliyse tik yeter, oyuncunun yanina gelmesi beklenmez")]
    [SerializeField] private bool openFromAnywhere = true;

    [Header(" Kapali Hali ")]
    [Tooltip("Kurulumda kaydedilir. Elle degistirme -- kapinin sifir noktasi bu")]
    [SerializeField] private Vector3 closedPosition;
    [SerializeField] private Vector3 closedEuler;

    [Tooltip("Kapali hali kaydedildi mi. Kapali degilken isaretlersen kapi kayar")]
    [SerializeField] private bool captured;

    private float angle;
    private float target;
    private float closeTimer;

    public Transform Door => door;
    public bool NeedsPlayer => !openFromAnywhere;
    public bool IsOpen => Mathf.Abs(target) > .01f;
    public bool Captured => captured;

    public Vector3 StandPosition => standPoint == null ? transform.position : standPoint.position;

    private void Awake()
    {
        if (door == null)
            door = FindDoor();

        // Adding the component by hand rather than through the setup leaves
        // nothing recorded, and whatever the door is sitting at right now is the
        // best guess at closed anyone has
        if (!captured)
            CaptureClosed();

        angle = 0f;
        target = 0f;

        Apply();

        // The one failure with no symptom: the door swings exactly as it should
        // and nothing ever comes out of it
        if (drinkPrefab == null)
            Debug.LogWarning(name + ": Drink Prefab bos -- kapi acilir ama icecek vermez.\n" +
                             "Cooked Fast > Buzdolabi: 1 - Kapiyi Kur calistir.", this);
    }

    public Transform FindDoor()
    {
        foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
        {
            if (candidate != transform && candidate.name.ToLower().Contains("door"))
                return candidate;
        }

        return null;
    }

    public void CaptureClosed()
    {
        if (door == null)
            door = FindDoor();

        if (door == null)
            return;

        closedPosition = door.localPosition;
        closedEuler = door.localRotation.eulerAngles;
        captured = true;
    }

    public enum Result
    {
        Taken,
        Opened,
        Closed,
        HandFull,

        // Its own answer rather than folding into Opened. The two look identical
        // on screen -- the door swings either way -- and reading "door opening"
        // while wondering where the can went is no help at all
        NoDrink
    }

    // One tap does both: the door swings and a can comes out of it. There is
    // nothing to wait for here, so there is no second tap to wait for either.
    //
    // A full hand still gets the door, because a tap that does nothing at all
    // reads as a broken fridge rather than as a full pair of hands
    public Result Tap(HoldFoodAbility hand)
    {
        if (drinkPrefab == null)
        {
            Toggle();
            return Result.NoDrink;
        }

        if (hand == null)
        {
            Toggle();
            return IsOpen ? Result.Opened : Result.Closed;
        }

        // Asked with the prefab: every check in there is on the type, so a full
        // hand costs nothing built and thrown away
        if (!hand.CanTake(drinkPrefab))
        {
            Open();
            return Result.HandFull;
        }

        SpawnableFood drink = Instantiate(drinkPrefab);

        if (!hand.TryPush(drink))
        {
            Destroy(drink.gameObject);

            Open();
            return Result.HandFull;
        }

        Open();

        SoundManager.Play(SoundManager.Sound.DrinkTaken);

        return Result.Taken;
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        target = openAngle;

        // Restarted on every tap, so tapping an open door twice keeps it open
        // rather than leaving it to close halfway through the second look
        closeTimer = autoCloseAfter;
    }

    public void Close()
    {
        target = 0f;
        closeTimer = 0f;
    }

    private void Update()
    {
        if (door == null)
            return;

        if (closeTimer > 0f)
        {
            closeTimer -= Time.deltaTime;

            if (closeTimer <= 0f)
                Close();
        }

        if (Mathf.Approximately(angle, target))
            return;

        angle = Mathf.MoveTowards(angle, target, speed * Time.deltaTime);

        Apply();
    }

    // Also what the editor preview calls, so the angle dialled in there is the
    // angle the door swings to in play
    public void PreviewAt(float degrees)
    {
        if (door == null)
            door = FindDoor();

        angle = degrees;
        target = degrees;

        Apply();
    }

    public float OpenAngle => openAngle;

    private void Apply()
    {
        if (door == null)
            return;

        Vector3 axis = hingeAxis.sqrMagnitude < .0001f ? Vector3.up : hingeAxis.normalized;

        Quaternion spin = Quaternion.AngleAxis(angle, axis);
        Quaternion closed = Quaternion.Euler(closedEuler);

        // Turning about the hinge point rather than about the door's pivot. With
        // no offset the two are the same, and on this model they already are --
        // the door's origin sits on its own hinge edge
        Vector3 pivot = closedPosition + hingeOffset;

        door.localPosition = pivot + spin * (closedPosition - pivot);
        door.localRotation = spin * closed;
    }

    // The hinge is invisible and getting it wrong is the one mistake that makes
    // the door swing through the fridge, so it is drawn
    private void OnDrawGizmosSelected()
    {
        Transform target = door == null ? FindDoor() : door;

        if (target == null)
            return;

        Vector3 pivot = transform.TransformPoint(
            (captured ? closedPosition : target.localPosition) + hingeOffset);

        Vector3 axis = hingeAxis.sqrMagnitude < .0001f ? Vector3.up : hingeAxis.normalized;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(pivot, .08f);
        Gizmos.DrawLine(pivot, pivot + transform.TransformDirection(axis) * .8f);
    }
}
