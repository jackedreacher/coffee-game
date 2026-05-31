using System.Collections;
using UnityEngine;
using System;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UISafeZone : MonoBehaviour
{
    private RectTransform rt;

    [Header(" Settings ")]
    [Tooltip("Apply the safe zone settings?")]
    [SerializeField] private bool apply = true;

    IEnumerator Start()
    {
        // wait one frame to ensure Canvas is initialized
        yield return null;

        if (!apply)
            yield break;

        Apply();
    }

    public void Apply()
    {
        StartCoroutine("ApplyCoroutine");        
    }

    public void ApplyEditor()
    {
        rt = GetComponent<RectTransform>();

        Rect safeRect = Screen.safeArea;

        // Convert pixel rect to normalized anchors
        Vector2 anchorMin = safeRect.position;
        Vector2 anchorMax = safeRect.position + safeRect.size;

        float width = Screen.currentResolution.width;
        float height = Screen.currentResolution.height;

        anchorMin.x /= width;
        anchorMin.y /= height;
        anchorMax.x /= width;
        anchorMax.y /= height;

        // Apply to panel
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    IEnumerator ApplyCoroutine()
    {
        yield return null;

        rt = GetComponent<RectTransform>();

        Rect safeRect = Screen.safeArea;

        // Convert pixel rect to normalized anchors
        Vector2 anchorMin = safeRect.position;
        Vector2 anchorMax = safeRect.position + safeRect.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Apply to panel
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

}

#if UNITY_EDITOR
[CustomEditor(typeof(UISafeZone))]
public class UISafeZoneEditor : Editor
{
    // The logic here is to wait for one or two frames after focusing the simulator view for the Screen to configure

    private int updateCount;

    private void EditorUpdate()
    {
        updateCount++;

        if(updateCount >= 2)
        {
            updateCount = 0;
            EditorApplication.update -= EditorUpdate;

            (target as UISafeZone).ApplyEditor();
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
    
        if(GUILayout.Button("Apply"))
        {
            EditorApplication.update += EditorUpdate;
            FocusSimulatorView();
        }
    }

    public static void FocusGameView()
    {
        // UnityEditor.GameView is internal, so we get it via reflection
        var assembly = typeof(EditorWindow).Assembly;
        var type = assembly.GetType("UnityEditor.GameView");
        var gameView = EditorWindow.GetWindow(type);
        gameView.Focus();
    }

    public static void FocusSimulatorView()
    {
        // Assembly path might difer
        // To know the exact path of the assembly, uncomment the following line, and open the original script
        //UnityEditor.DeviceSimulation.DeviceSimulator;

        // You should find the assembly path at line 2


        var assembly = Assembly.LoadFile("C:/Program Files/Unity/Hub/Editor/6000.0.37f1/Editor/Data/Managed/UnityEngine/UnityEditor.DeviceSimulatorModule.dll");
        var type = assembly.GetType("UnityEditor.DeviceSimulation.SimulatorWindow");
        EditorWindow simView = EditorWindow.GetWindow(type);
        
        simView.Focus();
    }
}
#endif