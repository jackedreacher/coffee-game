#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Takes every no-walk zone and navmesh carve back out.
//
// Separate from the command that builds them, and deliberately blunt: a zone
// drawn over the wrong floor stops the player reaching their own counter, and
// the way out of that has to be one click that needs no thought about which
// box was the wrong one
public static class WalkZoneClear
{
    [MenuItem("Cooked Fast/Etkilesim: Tezgah Arkasini Ac", priority = 214)]
    public static void Clear()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Tezgah Arkasi",
                "Play modundayken calismaz. Once durdur.", "Tamam");
            return;
        }

        NoWalkZone[] zones = Object.FindObjectsByType<NoWalkZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        string report = "";

        foreach (NoWalkZone zone in zones)
        {
            if (zone == null)
                continue;

            report += "- " + Path(zone.transform) + " silindi\n";
            Undo.DestroyObjectImmediate(zone.gameObject);
        }

        if (zones.Length <= 0)
            report += "- Sahnede alan yoktu, degisen bir sey olmadi\n";

        report += "\nTiklama yaricapi oldugu gibi biraktildi -- o sorun cikarmaz.\n" +
                  "Geri kurmak icin: Cooked Fast > Etkilesim: Tezgah Arkasini Kapat";

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Tezgah Arkasi Acildi\n" + report);
        EditorUtility.DisplayDialog("Tezgah Arkasi Acildi", report, "Tamam");
    }

    private static string Path(Transform target)
    {
        string path = target.name;

        for (Transform walk = target.parent; walk != null; walk = walk.parent)
            path = walk.name + "/" + path;

        return path;
    }
}
#endif
