using UnityEngine;

// One round of the 50, as an asset.
//
// Two numbers, because two numbers are what the difficulty curve is made of:
// how many people turn up, and how close together. Everything else about a
// round -- what they order, how long they wait, what the kitchen can make --
// belongs to the things that already own it
[CreateAssetMenu(fileName = "Round", menuName = "Cooked Fast/Round Data")]
public class RoundData : ScriptableObject
{
    [Header(" Raund ")]
    [Tooltip("Bu raundda toplam kac musteri gelir")]
    [SerializeField][Min(1)] private int totalCustomers = 3;

    [Tooltip("Iki musteri arasi sure, saniye")]
    [SerializeField][Min(.2f)] private float spawnInterval = 15f;

    [Tooltip("Bos birakilabilir. Raund ekraninda gosterilecek not")]
    [SerializeField] private string note;

    public int TotalCustomers => Mathf.Max(1, totalCustomers);
    public float SpawnInterval => Mathf.Max(.2f, spawnInterval);
    public string Note => note;
}
