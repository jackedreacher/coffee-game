using UnityEngine;

// Takes anything a character is carrying: the dirty dishes collected off tables,
// and now whatever is on the tray as well. No type check on purpose -- a bin
// that only accepts some foods leaves the player stuck holding the others, with
// a half built burger the easiest way to get there
public class Trash : MonoBehaviour
{
    [SerializeField] private Transform workerTargetPoint;
    public Vector3 WorkerTargetPosition => workerTargetPoint.position;

    [Header(" Settings ")]
    // How long a character has to stay in the bin's trigger before it empties
    // their hands. Serialized rather than const so walking past with a pizza on
    // the way somewhere else can be made forgiving
    [Tooltip("Cope dusurmeden once bu kadar saniye icinde durulmali")]
    [SerializeField] private float dumpDelay = .2f;

    private float dumpTimer;

    private void OnTriggerStay(Collider other)
    {
        bool hasDishes = other.TryGetComponent(out HoldDishAbility holdDishAbility) &&
                         holdDishAbility.HasDishes;

        bool hasFood = other.TryGetComponent(out HoldFoodAbility holdFoodAbility) &&
                       holdFoodAbility.IsPlateauActive &&
                       !holdFoodAbility.IsPlateauEmpty;

        if (!hasDishes && !hasFood)
        {
            dumpTimer = 0;
            return;
        }

        dumpTimer += Time.deltaTime;

        if (dumpTimer < dumpDelay)
            return;

        dumpTimer = 0;

        if (hasDishes)
            Burn(holdDishAbility.PopAll());

        if (hasFood)
            Burn(holdFoodAbility.DumpAll());
    }

    // One tap, one emptying. The delay above is there to stop a character who
    // walks across the trigger on the way somewhere else from losing their food;
    // a tap is already a deliberate act and needs no such protection
    public bool Dump(HoldFoodAbility food, HoldDishAbility dishes)
    {
        bool any = false;

        if (dishes != null && dishes.HasDishes)
        {
            Burn(dishes.PopAll());
            any = true;
        }

        if (food != null && food.IsPlateauActive && !food.IsPlateauEmpty)
        {
            Burn(food.DumpAll());
            any = true;
        }

        dumpTimer = 0;

        return any;
    }

    private void Burn(SpawnableFood[] items)
    {
        for (int i = items.Length - 1; i >= 0; i--)
        {
            if (items[i] != null)
                Destroy(items[i].gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out HoldDishAbility _) &&
            !other.TryGetComponent(out HoldFoodAbility _))
            return;

        dumpTimer = 0;
    }
}
