using UnityEngine;

public class Trash : MonoBehaviour
{
    private float dumpTimer;
    private const float dumpDelay = 0.2f;

    private void OnTriggerStay(Collider other)
    {
        if (!other.TryGetComponent(out HoldDishAbility holdDishAbility))
            return;

        if (!holdDishAbility.HasDishes)
        {
            dumpTimer = 0;
            return;
        }

        dumpTimer += Time.deltaTime;

        if (dumpTimer < dumpDelay)
            return;

        dumpTimer = 0;

        SpawnableFood[] dishes = holdDishAbility.PopAll();

        for (int i = dishes.Length - 1; i >= 0; i--)
            Destroy(dishes[i].gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        dumpTimer = 0;
    }
}
