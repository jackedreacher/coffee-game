using UnityEngine;

// A clock face that empties clockwise from the top.
//
// This is Image.fillAmount with fillMethod Radial360 -- and that is a Canvas
// feature, which this bubble deliberately does not have. So the arc is built as
// a mesh instead: an annulus sector, rebuilt when the angle actually changes.
//
// The mesh version is not a workaround, it is the cheaper one here. A world
// space Canvas would bring a scale, a facing, a draw order and a rect, every one
// of them decided somewhere else and every one of them able to come out
// invisible without saying anything. A mesh is either there in the scene view or
// it is not
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RadialTimer : MonoBehaviour
{
    [Header(" Settings ")]
    [Tooltip("Ic yaricap -- emojinin siğacagi bosluk")]
    [SerializeField] private float innerRadius = .36f;

    [Tooltip("Dis yaricap")]
    [SerializeField] private float outerRadius = .48f;

    [Tooltip("Kac parcaya bolunsun. Buyut = daha yuvarlak")]
    [SerializeField] private int segments = 48;

    private float Inner => innerRadius > .001f ? innerRadius : .36f;
    private float Outer => outerRadius > .001f ? outerRadius : .48f;
    private int Segments => segments > 3 ? segments : 48;

    private Mesh mesh;
    private MeshRenderer meshRenderer;

    // The last angle actually built. Rebuilding on every frame would be forty
    // triangles of work for a change too small to see; this rebuilds when the
    // arc has moved by about a degree
    private float built = -1f;

    private static readonly int baseColourId = Shader.PropertyToID("_BaseColor");
    private static readonly int colourId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh { name = "Radial Timer" };

        // Not shared. Two customers with different amounts of patience left
        // would otherwise be drawing the same arc
        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }

    public void SetFill(float fill)
    {
        fill = Mathf.Clamp01(fill);

        if (mesh == null)
            return;

        if (Mathf.Abs(fill - built) < 1f / Segments * .5f && built >= 0f)
            return;

        built = fill;

        Build(fill);
    }

    public void SetColour(Color colour)
    {
        if (meshRenderer == null)
            return;

        // renderer.material, not a property block. A block only lands if the
        // shader declares the property being written and is a silent no-op
        // otherwise, which is a whole afternoon nobody should spend twice
        Material material = meshRenderer.material;

        if (material == null)
            return;

        if (material.HasProperty(baseColourId))
            material.SetColor(baseColourId, colour);

        if (material.HasProperty(colourId))
            material.SetColor(colourId, colour);
    }

    private void Build(float fill)
    {
        mesh.Clear();

        if (fill <= 0f)
            return;

        int steps = Mathf.Max(1, Mathf.CeilToInt(Segments * fill));

        Vector3[] vertices = new Vector3[(steps + 1) * 2];
        int[] triangles = new int[steps * 6];

        float sweep = 360f * fill;

        for (int i = 0; i <= steps; i++)
        {
            // From the top, clockwise. Both halves of that matter: a clock that
            // starts at three o'clock or runs backwards is read wrong before it
            // is read at all
            float angle = (90f - sweep * i / steps) * Mathf.Deg2Rad;

            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            vertices[i * 2] = direction * Inner;
            vertices[i * 2 + 1] = direction * Outer;
        }

        for (int i = 0; i < steps; i++)
        {
            int at = i * 6;
            int corner = i * 2;

            triangles[at] = corner;
            triangles[at + 1] = corner + 1;
            triangles[at + 2] = corner + 2;

            triangles[at + 3] = corner + 1;
            triangles[at + 4] = corner + 3;
            triangles[at + 5] = corner + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
    }
}
