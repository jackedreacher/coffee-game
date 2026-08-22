#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

// One portrait canvas, in the one place both online setup commands can reach
// it. Twelve lines, but the reference resolution and the match axis have to be
// the same in both or the same panel is laid out twice at two sizes.
public static class CoopCanvas
{
    public static GameObject Build(string name, int order)
    {
        GameObject root = new GameObject(name);

        Canvas canvas = root.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);

        // Portrait, so height is what the layout is measured against
        scaler.matchWidthOrHeight = 1f;

        root.AddComponent<GraphicRaycaster>();

        return root;
    }
}
#endif
