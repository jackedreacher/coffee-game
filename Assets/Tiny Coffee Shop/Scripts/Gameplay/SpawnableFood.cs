using UnityEngine;

public abstract class SpawnableFood : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private MeshFilter filter;
    [SerializeField] private MeshRenderer meshRenderer;

    [Header(" Settings ")]
    [SerializeField] private float cleanYOffsetOnPlateau;
    [SerializeField] private float dirtyYOffsetOnPlateau;
    [SerializeField] private Mesh dirtyMesh;

    private bool isDirty;

    public float CleanYOffsetOnPlateau => cleanYOffsetOnPlateau;
    public float DirtyYOffsetOnPlateau => dirtyYOffsetOnPlateau;
    public bool IsDirty => isDirty;
    public bool IsVisible => meshRenderer.enabled;

    public void MarkAsDirty()
    {
        isDirty = true;
        filter.mesh = dirtyMesh;
    }

    public void Display()
    {
        meshRenderer.enabled = true;
    }

    public void Hide()
    {
        meshRenderer.enabled = false;
    }
}
