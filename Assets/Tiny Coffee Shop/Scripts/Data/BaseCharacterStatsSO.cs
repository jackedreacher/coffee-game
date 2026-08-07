using UnityEngine;

[CreateAssetMenu(fileName = "BaseCharacterStatsSO", menuName = "Scriptable Objects/BaseCharacterStatsSO")]
public class BaseCharacterStatsSO : ScriptableObject
{
    [Header(" Stats ")]
    [SerializeField][Range(1, 4)] private float speed;
    [SerializeField][Range(7, 14)] private int capacity;
    [SerializeField][Range(1, 2)] private float revenue;

    #region Properties
    public float Speed => speed;
    public int Capacity => capacity;
    public float Revenue => revenue;
    #endregion
}
