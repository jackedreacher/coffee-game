#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Calms the customers' walk down, on the navigation side.
//
// The swaying had two halves. One was the facing: the body was turned to follow
// agent.velocity, which is the steering AFTER local avoidance has pushed the
// agent aside for everyone else on the floor. That half is fixed in code and
// needs nothing from here.
//
// This is the other half -- the agent's own settings, which decide how hard it
// is pushed in the first place. They are prefab data, so they are a command
// rather than a line of code, and a separate command because they change how
// the movement FEELS and that is a thing to look at rather than assume
public static class CustomerWalkSetup
{
    private const string customersFolder = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";

    // How fast the agent may change its velocity, and the reason a small nudge
    // became a visible lurch.
    //
    // The prefabs ship at 800, which reaches full speed in under a hundredth of
    // a second: every sideways correction the avoidance asks for is applied at
    // full size on the frame it is asked for. A moderate figure spends a fifth
    // of a second getting there instead, which averages the corrections out
    // without the character feeling sluggish
    private const float acceleration = 30f;

    // Fewer sampled directions, which sounds worse and is not. High quality
    // considers more ways round an obstacle, and when two of them cost almost
    // the same it can pick a different one each frame -- that indecision IS the
    // sway. A queue walking to fixed spots does not need the extra thinking
    private const ObstacleAvoidanceType avoidance =
        ObstacleAvoidanceType.MedQualityObstacleAvoidance;

    // Slows into the destination rather than running at it flat out and
    // stopping dead. It also has to be on once acceleration comes down: without
    // braking a slower agent overshoots the spot and circles back to it, which
    // is a different and worse wobble at the exact moment it arrives
    private const bool braking = true;

    [MenuItem("Cooked Fast/Musteri/Yuruyusu Sakinlestir", priority = 603)]
    public static void Setup()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { customersFolder });

        if (guids.Length <= 0)
        {
            Show("Musteri prefabi bulunamadi:\n" + customersFolder);
            return;
        }

        StringBuilder report = new StringBuilder();

        // The width the floor was actually cut for.
        //
        // Not a number of ours to choose: the mesh is shrunk back from every
        // wall by this, and an agent wider than it is an agent that does not fit
        // where the floor says it does. Matching it is the only setting here
        // that is not a taste question
        float baked = NavMesh.GetSettingsByID(0).agentRadius;

        report.AppendLine("Zeminin kesildigi yaricap: " + baked.ToString("0.00"));
        report.AppendLine();

        int touched = 0;

        foreach (string guid in guids)
        {
            if (Apply(AssetDatabase.GUIDToAssetPath(guid), baked, report))
                touched++;
        }

        AssetDatabase.SaveAssets();

        report.AppendLine();
        report.AppendLine(touched + " musteri prefabi guncellendi.");
        report.AppendLine();
        report.AppendLine("Ne degisti");
        report.AppendLine("  Radius  -> zeminin kesildigi olcu");
        report.AppendLine("    Ajan zeminin hesapladigindan kalinsa, yol dumduz");
        report.AppendLine("    olsa bile birbirlerini iter. Yalpalamanin sebebi");
        report.AppendLine("    buysa bu satir cozer.");
        report.AppendLine("  Acceleration  800 -> " + acceleration.ToString("0"));
        report.AppendLine("    Yandan gelen her itmeyi ayni karede tam boyuyla");
        report.AppendLine("    uyguluyordu. Artik yayiyor.");
        report.AppendLine("  Obstacle Avoidance  High -> Med");
        report.AppendLine("    High kalite iki yol arasinda kararsiz kalinca her");
        report.AppendLine("    karede birini seciyor. Yalpalamanin kendisi bu.");
        report.AppendLine("  Auto Braking  kapali -> acik");
        report.AppendLine("    Acceleration dusunce sart: freni olmayan yavas");
        report.AppendLine("    ajan hedefi kacirip etrafinda donuyor.");
        report.AppendLine();
        report.AppendLine("Yavas geldiklerini dusunursen Acceleration'i yukselt --");
        report.AppendLine("  hiz degil ivme, yani kalkis sertligi. Hiz ayri: Speed 6.");
        report.AppendLine();
        report.AppendLine("Geri almak icin ustteki eski degerleri elle yaz,");
        report.AppendLine("  ya da Ctrl+Z. Prefablar diske yazildi.");

        Show(report.ToString());
    }

    private static bool Apply(string path, float baked, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            NavMeshAgent agent = root.GetComponentInChildren<NavMeshAgent>(true);

            if (agent == null)
            {
                report.AppendLine(root.name + ": NavMeshAgent yok, atlandi");
                return false;
            }

            report.AppendLine(root.name);
            report.AppendLine("  once : yaricap " + agent.radius.ToString("0.00") +
                              ", ivme " + agent.acceleration.ToString("0") +
                              ", kacinma " + agent.obstacleAvoidanceType +
                              ", fren " + (agent.autoBraking ? "acik" : "kapali"));

            agent.radius = baked;
            agent.acceleration = acceleration;
            agent.obstacleAvoidanceType = avoidance;
            agent.autoBraking = braking;

            report.AppendLine("  sonra: yaricap " + baked.ToString("0.00") +
                              ", ivme " + acceleration.ToString("0") +
                              ", kacinma " + avoidance +
                              ", fren " + (braking ? "acik" : "kapali"));

            PrefabUtility.SaveAsPrefabAsset(root, path);

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Show(string report)
    {
        Debug.Log("[Yuruyus]\n" + report);
        EditorUtility.DisplayDialog("Musteri Yuruyusu", report, "Tamam");
    }
}
#endif
