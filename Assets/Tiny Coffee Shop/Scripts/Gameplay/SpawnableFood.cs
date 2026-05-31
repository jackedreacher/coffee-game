using UnityEngine;

public abstract class SpawnableFood : MonoBehaviour
{
    [Header(" Settings ")]
    [SerializeField] private float cleanYOffsetOnPlateau;

    public float CleanYOffsetOnPlateau => cleanYOffsetOnPlateau;
}
