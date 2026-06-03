using UnityEngine;

[RequireComponent(typeof(HoldFoodAbility))]
public class HoldDishAbility : MonoBehaviour
{
    private Plateau plateau;

    private void Awake()
    {
        plateau = GetComponent<HoldFoodAbility>().Plateau;
    }

    public bool CanCollectDishes()
    {
        if (!plateau.gameObject.activeInHierarchy)
            return true;

        if (!plateau.IsEmpty && !plateau.IsDirty)
            return false;

        return true;
    }

    public bool HasDishes => !plateau.IsEmpty && plateau.IsDirty;

    public void CollectDishes(SpawnableFood[] dishes)
    {
        for (int i = 0; i < dishes.Length; i++)
        {
            plateau.gameObject.SetActive(true);
            plateau.Push(dishes[i]);
        }
    }

    public SpawnableFood[] PopAll()
    {
        SpawnableFood[] dishes = plateau.PopAll();
        plateau.gameObject.SetActive(false);
        return dishes;
    }
}
