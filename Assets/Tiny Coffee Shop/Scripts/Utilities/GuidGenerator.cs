using UnityEngine;

public class GuidGenerator : MonoBehaviour
{
    [SerializeField] private string guid;
    public string Guid => guid;

    private void Awake()
    {
        if (string.IsNullOrEmpty(guid))
            guid = System.Guid.NewGuid().ToString();
    }
}
