using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GuidGenerator : MonoBehaviour
{
    [SerializeField, ReadOnly] private string guid;
    public string GUID => guid;

    private void Reset()
    {
        GenerateGuid();
    }

    public void GenerateGuid()
    {
        guid = System.Guid.NewGuid().ToString();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GuidGenerator))]
public class GuidGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GuidGenerator script = (GuidGenerator)target;

        if (GUILayout.Button("Generate GUID"))
        {
            script.GenerateGuid();
            EditorUtility.SetDirty(script);
        }
    }
}
#endif


// Helper attribute to make guid field readonly
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;
    }
}
#endif
