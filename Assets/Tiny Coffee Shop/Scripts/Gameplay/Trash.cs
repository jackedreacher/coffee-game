using UnityEngine;

public class Trash : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out HoldDishAbility holdDishAbility))
            return;

        if (!holdDishAbility.HasDishes)
            return;

        SpawnableFood[] dishes = holdDishAbility.PopAll();

        for (int i = dishes.Length - 1; i >= 0; i--)
            Destroy(dishes[i].gameObject);
    }
}
