using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/Gradient Image")]
[RequireComponent(typeof(Image))]
public class UIGradient : BaseMeshEffect
{
    [SerializeField] private Color topColor = Color.white;
    [SerializeField] private Color bottomColor = Color.black;

    public Color TopColor
    {
        get => topColor;
        set { topColor = value; graphic.SetVerticesDirty(); }
    }

    public Color BottomColor
    {
        get => bottomColor;
        set { bottomColor = value; graphic.SetVerticesDirty(); }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        UIVertex vertex = new UIVertex();
        int count = vh.currentVertCount;

        // Find the min and max Y positions
        vh.PopulateUIVertex(ref vertex, 0);
        float bottomY = vertex.position.y;
        float topY = vertex.position.y;

        for (int i = 1; i < count; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            float y = vertex.position.y;
            if (y > topY) topY = y;
            if (y < bottomY) bottomY = y;
        }

        float height = topY - bottomY;

        // Apply gradient based on each vertex's Y position
        for (int i = 0; i < count; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);

            float t = (height > 0) ? (vertex.position.y - bottomY) / height : 0f;
            Color targetColor = Color.Lerp(bottomColor, topColor, t);

            // Preserve the original vertex alpha
            //float originalAlpha = vertex.color.a;
            //targetColor.a *= originalAlpha;

            vertex.color = targetColor;
            vh.SetUIVertex(vertex, i);
        }
    }
}
