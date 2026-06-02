using UnityEngine;

public class HoldDishAbility : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Plateau plateau;

    public bool CanCollectDishes()
    {
        // Plateau devre dışı = üzerinde hiçbir şey yok = kirli tabak alabiliriz
        if (!plateau.gameObject.activeInHierarchy)
            return true;

        // Plateau aktif ama temiz yemek var = tabak alamayız
        if (!plateau.IsEmpty && !plateau.IsDirty)
            return false;

        // Plateau boş veya zaten kirli = alabiliriz
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
