using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Four customers, further apart, walking in faster, kicking up dust.
//
// Two halves that live in different places: speed and dust belong to the
// customer prefabs, count and spacing belong to the counter in the scene. One
// command does both so nobody has to remember which is where
public static class CustomerSetup
{
    private const string customerFolder = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";
    private const string dustMaterialPath = "Assets/Tiny Coffee Shop/Materials/Effects/Smoke Particle.mat";
    private const string dustName = "Dust Trail";

    // 3.5 was the NavMeshAgent default. Acceleration is already 800 and angular
    // speed is deliberately 0 -- the animator turns them, not the agent -- so
    // speed is the only number worth touching
    private const float walkSpeed = 6f;

    private const int wantedCustomers = 3;

    // Metres between one customer and the one behind. Set as a length along the
    // direction already authored, so re-running does not keep pushing the queue
    // further out every time
    private const float queueGap = 1.9f;

    // Opened up from 1.7 because the customers stopped being the widest thing
    // standing there. A rabbit is well under a metre across, an order bubble is
    // OrderBubbleSetup.bubbleSize wide and floats over the head -- so the bubbles
    // touch long before the shoulders do, and this number now answers to them
    private const float sideGap = 2.05f;

    [MenuItem("Cooked Fast/Musteri/Hiz + Toz + Sayi + Mesafe", priority = 190)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz.\n\n" +
                "Prefab degisiklikleri Play durunca geri alinir.\n" +
                "Once Play'i durdur, sonra tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.Append(SetupPrefabs());
        report.AppendLine();
        report.Append(SetupCounters());

        report.AppendLine();
        report.AppendLine("Ayarlar");
        report.AppendLine("  Hiz      : her Customer_Rabbit_* > Nav Mesh Agent > Speed");
        report.AppendLine("  Toz      : ayni prefab > " + dustName + " > Particle System");
        report.AppendLine("    yogunluk: Emission > Rate over Distance  (metrede kac parcacik)");
        report.AppendLine("    buyukluk: Main > Start Size");
        report.AppendLine("    omur    : Main > Start Lifetime");
        report.AppendLine("  Sayi     : sahnedeki istasyon > Food Serving Customer Manager > Max Customers");
        report.AppendLine("  Mesafe   : ayni yer > Queue Spacing  (arka arkaya)");
        report.AppendLine("             ayni yer > Side Spacing   (yan yana, Customers Per Row > 1 ise)");

        Debug.Log("[Musteriler]\n" + report);
        EditorUtility.DisplayDialog("Musteri Ayarlari", report.ToString(), "Tamam");
    }

    // ---- the prefabs: speed and dust ---------------------------------------

    private static string SetupPrefabs()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(dustMaterialPath);

        StringBuilder report = new StringBuilder();

        report.AppendLine("Prefablar  (hiz " + walkSpeed.ToString("0.0") + " + toz)");

        if (material == null)
            report.AppendLine("  UYARI: " + dustMaterialPath + " yok, toz materyalsiz kalacak");

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { customerFolder });

        if (guids.Length <= 0)
        {
            report.AppendLine("  " + customerFolder + " icinde prefab yok");
            return report.ToString();
        }

        int touched = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            if (root == null)
            {
                report.AppendLine("  " + System.IO.Path.GetFileName(path) + ": acilamadi");
                continue;
            }

            try
            {
                if (root.GetComponent<Customer>() == null)
                {
                    report.AppendLine("  " + root.name + ": Customer degil, atlandi");
                    continue;
                }

                string line = "  " + root.name + ": ";

                line += ApplySpeed(root);
                line += ", " + ApplyDust(root, material);

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);

                report.AppendLine(line + (saved ? "" : "  <-- KAYIT BASARISIZ"));

                if (saved)
                    touched++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        report.AppendLine("  toplam " + touched + " prefab yazildi");

        return report.ToString();
    }

    private static string ApplySpeed(GameObject root)
    {
        UnityEngine.AI.NavMeshAgent agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (agent == null)
            return "agent yok";

        float was = agent.speed;

        agent.speed = walkSpeed;

        return "hiz " + was.ToString("0.0") + " -> " + walkSpeed.ToString("0.0");
    }

    private static string ApplyDust(GameObject root, Material material)
    {
        Transform existing = root.transform.Find(dustName);

        if (existing != null)
        {
            ParticleSystem found = existing.GetComponent<ParticleSystem>();

            if (found == null)
                return "toz objesi var ama ParticleSystem yok";

            Configure(found, material);

            return "toz yenilendi";
        }

        GameObject dust = new GameObject(dustName);

        dust.transform.SetParent(root.transform, false);

        // Just off the floor. On it, half of every particle is buried
        dust.transform.localPosition = new Vector3(0f, .05f, 0f);
        dust.transform.localRotation = Quaternion.identity;
        dust.transform.localScale = Vector3.one;

        ParticleSystem particles = dust.AddComponent<ParticleSystem>();

        Configure(particles, material);

        return "toz eklendi";
    }

    // White puffs left at the feet.
    //
    // Emitted per metre walked rather than per second, which is what makes it a
    // trail: standing still emits nothing, and no script has to watch the agent's
    // velocity to switch it on and off. World simulation space is the other half
    // -- in local space the whole cloud is dragged along with the customer and
    // reads as a stuck-on decoration rather than as something left behind
    private static void Configure(ParticleSystem particles, Material material)
    {
        ParticleSystem.MainModule main = particles.main;

        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .35f);
        main.startSize = new ParticleSystem.MinMaxCurve(.18f, .38f);
        main.startColor = Color.white;

        // Negative against a downward gravity, so the dust drifts up a little
        main.gravityModifier = -.06f;
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particles.emission;

        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 6f;

        // A flat disc at the feet, particles spreading outwards along it. A
        // sphere would fire a third of them into the floor
        ParticleSystem.ShapeModule shape = particles.shape;

        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .15f;
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;

        color.enabled = true;

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(.5f, 0f),
                new GradientAlphaKey(.35f, .3f),
                new GradientAlphaKey(0f, 1f)
            });

        color.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;

        size.enabled = true;

        AnimationCurve curve = new AnimationCurve();

        curve.AddKey(0f, .5f);
        curve.AddKey(1f, 1.4f);

        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();

        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Pulled towards the camera, or the floor wins the depth fight and
            // the dust flickers in and out along the edges
            renderer.sortingFudge = -2f;

            if (material != null)
                renderer.sharedMaterial = material;
        }

        // Module writes go through the serialized object; on a prefab being
        // saved that is not always enough on its own
        EditorUtility.SetDirty(particles);
    }

    // ---- the scene: count and spacing --------------------------------------

    private static string SetupCounters()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("Tezgahlar  (sayi " + wantedCustomers + " + mesafe)");

        FoodServingCustomerManager[] managers =
            UnityEngine.Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (managers.Length <= 0)
        {
            report.AppendLine("  sahnede FoodServingCustomerManager yok");
            return report.ToString();
        }

        foreach (FoodServingCustomerManager manager in managers)
        {
            // No RecordObject: ApplyModifiedProperties registers its own undo,
            // and both together make one change take two Ctrl+Z to get back
            SerializedObject so = new SerializedObject(manager);

            SerializedProperty max = so.FindProperty("maxCustomers");
            SerializedProperty queue = so.FindProperty("queueSpacing");
            SerializedProperty side = so.FindProperty("sideSpacing");
            SerializedProperty perRow = so.FindProperty("customersPerRow");

            int wasMax = max.intValue;

            max.intValue = wantedCustomers;

            report.AppendLine("  " + manager.name);
            report.AppendLine("    sayi: " + wasMax + " -> " + wantedCustomers +
                              "  (yan yana " + perRow.intValue + ")");

            report.AppendLine("    " + Stretch(queue, queueGap, "Queue Spacing"));

            // Only says anything when the station actually uses rows
            if (perRow.intValue > 1)
            {
                report.AppendLine("    " + Stretch(side, sideGap, "Side Spacing"));
                report.AppendLine("    " + BubbleClearance());
            }

            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }

        return report.ToString();
    }

    // The two numbers that have to agree, said out loud. Bubbles touching is
    // something you only find by running the game and squinting at three cards,
    // and once found it looks like a bubble problem rather than a spacing one
    private static string BubbleClearance()
    {
        float bubble = OrderBubbleSetup.CardWidth;
        float clear = sideGap - bubble;

        if (clear < .1f)
            return "balon eni " + bubble.ToString("0.00") +
                   " -- ARALIK YETMIYOR (" + clear.ToString("0.00") +
                   "), balonlar ust uste biner. sideGap'i buyut";

        return "balon eni " + bubble.ToString("0.00") + ", aralarinda " +
               clear.ToString("0.00") + " birim bosluk kaliyor";
    }

    // Length changed, direction kept. Overwriting the vector outright would
    // point the queue somewhere the counter is not facing, and multiplying it
    // would push the queue another metre out on every run
    private static string Stretch(SerializedProperty property, float gap, string label)
    {
        Vector3 was = property.vector3Value;

        if (was.sqrMagnitude < .0001f)
            return label + ": SIFIR -- yonu bilinmiyor, dokunulmadi. Elle ver";

        property.vector3Value = was.normalized * gap;

        return label + ": " + was.magnitude.ToString("0.00") + " -> " +
               gap.ToString("0.00") + " birim  " + property.vector3Value.ToString("0.00");
    }
}
