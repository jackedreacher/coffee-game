#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Lists everything the player can tap, and everything that meant to be tappable
// and is not.
//
// Only worth having because ground taps are off. While tapping the floor walked
// the player anywhere, a station with no collider was a small annoyance -- you
// walked next to it by hand. With a whitelist it is unreachable, and the only
// symptom is a tap that does nothing
public static class TappableReport
{
    [MenuItem("Cooked Fast/Etkilesim/Neye Tiklanabilir", priority = 216)]
    public static void Report()
    {
        Interactable[] all = Object.FindObjectsByType<Interactable>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (all.Length <= 0)
        {
            Show("Sahnede hic Interactable yok.\n" +
                 "Bos zemin tiklamasi kapaliysa oyuncu hicbir yere gidemez.");
            return;
        }

        // The centre of everything tappable. Whatever is a long way from it is
        // the thing that walked the player out of the room -- with ground taps
        // off, a station somewhere odd is the ONLY way to get somewhere odd
        Vector3 centre = Vector3.zero;
        int counted = 0;

        foreach (Interactable target in all)
        {
            if (target == null)
                continue;

            centre += target.StandPosition;
            counted++;
        }

        if (counted > 0)
            centre /= counted;

        List<(float away, string line)> good = new List<(float, string)>();
        List<(float away, string line)> broken = new List<(float, string)>();

        Interactable farthest = null;
        float farthestAway = 0f;

        foreach (Interactable target in all)
        {
            if (target == null)
                continue;

            Vector3 stand = target.StandPosition;
            float away = Vector3.Distance(stand.With(y: 0), centre.With(y: 0));

            if (away > farthestAway)
            {
                farthest = target;
                farthestAway = away;
            }

            string where = "  " + target.Label +
                           "  @ " + stand.x.ToString("0") + ", " + stand.z.ToString("0") +
                           "  (merkeze " + away.ToString("0") + ")";

            string fault = Fault(target);

            if (fault == null)
                good.Add((away, where));
            else
                broken.Add((away, where + "\n      " + fault));
        }

        // Farthest first, so whatever is off on its own is the first line read
        good.Sort((a, b) => b.away.CompareTo(a.away));
        broken.Sort((a, b) => b.away.CompareTo(a.away));

        string report = "TIKLANABILIR (" + good.Count + ")\n";
        report += good.Count > 0 ? Lines(good) : "  yok";

        report += "\n\nSORUNLU (" + broken.Count + ")\n";
        report += broken.Count > 0 ? Lines(broken) : "  yok";

        if (farthest != null)
        {
            report += "\n\nEN UZAKTAKI: " + farthest.Label + ", merkezden " +
                      farthestAway.ToString("0") + " birim.\n" +
                      "Hierarchy'de secildi -- Scene'de F ile bak.";

            // Selected rather than described. The name of a station says
            // nothing about whether it is somewhere sensible; seeing where it
            // sits answers it in one look
            Selection.activeObject = farthest.gameObject;
            EditorGUIUtility.PingObject(farthest.gameObject);
        }

        if (broken.Count > 0)
        {
            report += "\n\nBunlar tiklansa da tepki vermez. Bos zemin tiklamasi\n" +
                      "kapaliyken bu, oraya hic gidilemez demek:\n" +
                      "Player > Tap To Serve > Walk On Ground Tap";
        }

        Show(report);
    }

    private static string Lines(List<(float away, string line)> rows)
    {
        string[] text = new string[rows.Count];

        for (int i = 0; i < rows.Count; i++)
            text[i] = rows[i].line;

        return string.Join("\n", text);
    }

    // Names the first thing wrong, or null when it works. First rather than all
    // of them: fixing the collider often makes the rest moot
    private static string Fault(Interactable target)
    {
        if (!target.gameObject.activeInHierarchy)
            return "obje kapali";

        if (!target.enabled)
            return "bilesen kapali";

        // The tap is a raycast. No collider anywhere under it and the ray goes
        // straight through to the floor behind
        if (target.GetComponentInChildren<Collider>(true) == null)
            return "COLLIDER YOK -- isin uzerinden gecer";

        Vector3 stand = target.StandPosition;

        // Where the feet go. Off the navmesh and the walk is refused with
        // "hedefin 1.5 birim yakininda NavMesh yok"
        if (!NavMesh.SamplePosition(stand, out NavMeshHit _, 1.5f, NavMesh.AllAreas))
            return "durma noktasi NavMesh disinda";

        if (NoWalkZone.Blocks(stand))
            return "durma noktasi bir NoWalkZone icinde";

        // A stand point left unset answers the object's own position, which is
        // usually inside the counter itself. Worth saying, not worth refusing
        if (target.StandPoint == null)
            return "Stand Point bos -- ayaklar objenin kendi yerine gider";

        return null;
    }

    private static void Show(string report)
    {
        Debug.Log("Neye Tiklanabilir\n" + report);
        EditorUtility.DisplayDialog("Neye Tiklanabilir", report, "Tamam");
    }
}
#endif
