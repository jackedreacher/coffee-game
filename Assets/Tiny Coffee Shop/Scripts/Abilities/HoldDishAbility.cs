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

    public void CollectDishes(SpawnableFood[] dishes)
    {
        for (int i = 0; i < dishes.Length; i++)
        {
            plateau.gameObject.SetActive(true);
            plateau.Push(dishes[i]);
        }
    }
}
