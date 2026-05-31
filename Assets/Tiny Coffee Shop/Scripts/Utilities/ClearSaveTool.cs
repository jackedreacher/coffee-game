#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class ClearSaveTool
{
    [MenuItem("Tools/Clear Save")]
    public static void ClearGameData()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Clear Game Data",
            "Are you sure you want to delete GameData.txt and clear the PlayerPrefs?\n\nThis cannot be undone.",
            "Yup",
            "Cancel"
        );

        if (!confirm)
            return;

        string filePath = Path.Combine(Application.dataPath, "GameData.txt");

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            File.Delete(filePath + ".meta");
            Debug.Log("Game Save deleted!");

            // Refresh the project window
            AssetDatabase.Refresh();
        }
        else
        {
            Debug.LogWarning("File not found: " + filePath);
        }

        // Clear PlayerPrefs
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("PlayerPrefs cleared!");
    }
}
#endif