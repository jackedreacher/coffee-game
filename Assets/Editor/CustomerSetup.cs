using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Four customers, the right size, far enough apart, walking in faster and
// kicking up dust.
//
// Three halves that live in different places: speed, dust and body size belong
// to the customer prefabs; count and spacing belong to the counter in the
// scene; the order bubble's height belongs to the prefab but is measured off
// the body, so it has to be rebuilt whenever the body changes size. One command
// does all of it, because doing any one of them alone leaves the other two
// wrong and the symptom shows up somewhere else entirely.
//
// Only customerHeight is typed. The spacing is measured off the artwork and
// checked against the camera, so "make them bigger" cannot silently mean "push
// the outside one off the screen" or "let the order bubbles overlap".
public static class CustomerSetup
{
    private const string customerFolder = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";
    private const string dustMaterialPath = "Assets/Tiny Coffee Shop/Materials/Effects/Smoke Particle.mat";
    private const string dustName = "Dust Trail";

    // 3.5 was the NavMeshAgent default. Acceleration is already 800 and angular
    // speed is deliberately 0 -- the animator turns them, not the agent -- so
    // speed is the only number worth touching
    private const float walkSpeed = 6f;

    private const int wantedCustomers = 4;

    // Starting scale for a freshly built capsule character. No longer what
    // decides how big a customer is -- SetupSizes measures each prefab and
    // solves for customerHeight -- but CapsuleCharacterSetup needs something to
    // put on a body it has only just created.
    //
    // Only the visible DGN body is touched either way. The prefab root stays at
    // one so the NavMeshAgent, tap target, order bubble and queue maths do not
    // quietly change size with the artwork.
    internal const float CapsuleVisualScale = .92f;

    // How tall every customer ends up standing. THE one number to change if
    // they are the wrong size; everything below is solved from it.
    //
    // Solved, not multiplied. A multiplier cannot be re-run -- the second pass
    // multiplies the first pass's result -- and the customers do not start from
    // one place anyway: the capsule prefabs were normalised to .92 by this tool
    // while the rabbits were never touched at all, so no single factor makes
    // them agree.
    private const float customerHeight = 2.45f;

    // Clear floor between two neighbours, on top of whatever they measure.
    private const float bodyClearance = .30f;

    // ... and between two order bubbles, which are the wider of the two. A
    // rabbit is well under a metre across and a bubble is 1.75, so the cards
    // touch long before the shoulders do and the spacing answers to them.
    private const float bubbleClearance = .25f;

    // Bubbles at full size unless the screen says otherwise.
    private const float wantedBubbleScale = 1f;

    // Fraction of the frame that stays empty around the outermost customer.
    private const float screenMargin = .04f;

    // Front to back never gets tighter than this, however slim the animals
    // turn out to be. Two customers a body's width apart look like one queue;
    // this is the distance at which they read as two people in a line.
    private const float shortestQueueGap = 1.9f;

    // Both spacings are now measured rather than typed. They stay as fields so
    // the report can print what was decided and why.
    private static float queueGap;
    private static float sideGap;

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
        report.Append(SetupSizes(out CustomerScale.Body body));
        report.AppendLine();
        report.Append(RebuildBubbles(body));
        report.AppendLine();
        report.Append(SetupCounters(body));

        report.AppendLine();
        report.AppendLine("Ayarlar");
        report.AppendLine("  Boyut    : CustomerSetup > customerHeight  (tek sayi, gerisi ondan cikiyor)");
        report.AppendLine("  Hiz      : her Customer_Rabbit_* > Nav Mesh Agent > Speed");
        report.AppendLine("  Toz      : ayni prefab > " + dustName + " > Particle System");
        report.AppendLine("    yogunluk: Emission > Rate over Distance  (metrede kac parcacik)");
        report.AppendLine("    buyukluk: Main > Start Size");
        report.AppendLine("    omur    : Main > Start Lifetime");
        report.AppendLine("  Sayi     : sahnedeki istasyon > Food Serving Customer Manager > Max Customers");
        report.AppendLine("  Mesafe   : ayni yer > Queue Spacing  (arka arkaya)");
        report.AppendLine("             ayni yer > Side Spacing   (yan yana, Customers Per Row > 1 ise)");
        report.AppendLine("             ikisi de olculerek yazildi -- elle degistirirsen bu komut geri alir");
        report.AppendLine("  Balon    : ayni yer > Four Wide Bubble Scale + Bubble Row Lift");

        Debug.Log("[Musteriler]\n" + report);
        EditorUtility.DisplayDialog("Musteri Ayarlari", report.ToString(), "Tamam");
    }

    [MenuItem("Cooked Fast/Musteri/4 Musteri Denemesi", priority = 189)]
    public static void FourCustomerTrial()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'u durdur, sonra tekrar dene.", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.Append(SetupSizes(out CustomerScale.Body body));
        report.AppendLine();
        report.Append(RebuildBubbles(body));
        report.AppendLine();
        report.Append(SetupCounters(body));

        Debug.Log("[4 Musteri Denemesi]\n" + report);
        EditorUtility.DisplayDialog("4 Musteri Denemesi",
            report + "\nCtrl+S ile sahneyi kaydet.", "Tamam");
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

    // Every customer prefab under Customers/, the rabbits and the capsules
    // alike, measured and solved onto one height.
    //
    // The old pass only walked Capsule Random/ and set an absolute .92 on a
    // child called "Body". The rabbits have no child called that, so nothing
    // ever sized them -- which is why they and the capsules were never the same
    // size and why "the customers are too small" had no single number to turn.
    private static string SetupSizes(out CustomerScale.Body widest)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("Govdeler  (hedef boy " +
                          customerHeight.ToString("0.00") + ")");

        widest = default;

        string[] guids = AssetDatabase.FindAssets("t:Prefab",
            new[] { customerFolder });
        int touched = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            if (root == null)
                continue;

            try
            {
                if (root.GetComponent<Customer>() == null)
                {
                    report.AppendLine("  " + root.name + ": Customer yok, atlandi");
                    continue;
                }

                CustomerScale.Body body = CustomerScale.Fit(
                    root, customerHeight, out string line);

                report.AppendLine("  " + line);

                if (!body.valid)
                    continue;

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);

                if (!saved)
                {
                    report.AppendLine("    <-- KAYIT BASARISIZ");
                    continue;
                }

                touched++;

                // The queue has to clear the BIGGEST of them, not the average.
                // One customer with antlers standing shoulder to shoulder is
                // the case the spacing exists for.
                if (body.width > widest.width)
                {
                    widest.width = body.width;
                    widest.height = body.height;
                    widest.valid = true;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        report.AppendLine("  toplam " + touched + " musteri yazildi, en genisi " +
                          widest.width.ToString("0.00"));

        return report.ToString();
    }

    // The bubble hangs off the prefab ROOT at a height measured from the head
    // at the moment it was built -- so resizing the body leaves every card at
    // the old head's altitude, sunk into the ears or floating clear above them.
    // Rebuilding is the only thing that re-reads the head, and it is cheap, so
    // it happens every time rather than being something to remember.
    private static string RebuildBubbles(CustomerScale.Body body)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("Siparis balonlari");

        if (!body.valid)
        {
            report.AppendLine("  govde olculemedi, balonlar yeniden kurulmadi");
            return report.ToString();
        }

        OrderBubbleSetup.Setup();

        report.AppendLine("  yeniden kuruldu -- kart " +
                          OrderBubbleSetup.CardWidth.ToString("0.00") +
                          " en, " + body.height.ToString("0.00") +
                          " boyundaki govdenin tepesine gore yerlestirildi");

        return report.ToString();
    }

    // ---- the scene: count and spacing --------------------------------------

    private static string SetupCounters(CustomerScale.Body body)
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

        Camera camera = CustomerScale.GameCamera();

        if (camera == null)
            report.AppendLine("  kamera yok -- ekrana sigma kontrolu yapilamadi");

        foreach (FoodServingCustomerManager manager in managers)
        {
            // No RecordObject: ApplyModifiedProperties registers its own undo,
            // and both together make one change take two Ctrl+Z to get back
            SerializedObject so = new SerializedObject(manager);

            SerializedProperty max = so.FindProperty("maxCustomers");
            SerializedProperty queue = so.FindProperty("queueSpacing");
            SerializedProperty side = so.FindProperty("sideSpacing");
            SerializedProperty perRow = so.FindProperty("customersPerRow");
            SerializedProperty bubbleScale = so.FindProperty("fourWideBubbleScale");
            SerializedProperty rowLift = so.FindProperty("bubbleRowLift");

            int wasMax = max.intValue;
            int columns = Mathf.Max(1, perRow.intValue);

            max.intValue = wantedCustomers;

            report.AppendLine("  " + manager.name);
            report.AppendLine("    sayi: " + wasMax + " -> " + wantedCustomers +
                              "  (yan yana " + columns + ")");

            // Bubbles are only shrunk by the runtime once four of them stand in
            // one row; below that they are always full size and this field is
            // never read.
            bool shrinkable = columns >= 4;
            float chosenScale = shrinkable ? wantedBubbleScale : 1f;

            // Wide enough for whichever of the two is wider, plus its own gap.
            sideGap = Mathf.Max(body.width + bodyClearance,
                OrderBubbleSetup.CardWidth * chosenScale + bubbleClearance);
            queueGap = Mathf.Max(body.width + bodyClearance, shortestQueueGap);

            // Written before the fit check, because SlotPosition reads the
            // component and the component has to be carrying the new numbers
            // before it can be asked where anybody stands.
            report.AppendLine("    " + Stretch(queue, queueGap, "Queue Spacing"));
            report.AppendLine("    " + Stretch(side, sideGap, "Side Spacing"));
            so.ApplyModifiedProperties();

            report.Append(FitToScreen(manager, camera, columns, body,
                shrinkable, ref chosenScale, so, side));

            if (bubbleScale != null)
            {
                bubbleScale.floatValue = Mathf.Clamp(chosenScale, .5f, 1f);
                report.AppendLine("    balon olcegi: " +
                                  bubbleScale.floatValue.ToString("0.00") +
                                  (shrinkable
                                      ? ""
                                      : "  (yan yana " + columns + " -- oyun bu " +
                                        "alani okumuyor, balonlar zaten tam boy)"));
            }

            // Back rows lift their cards clear of the row in front.
            //
            // Was the panel's height alone, which was wrong: the badge -- the
            // emoji and the timer ring around it -- hangs ABOVE the panel, and
            // it grew. OrderBubbleSetup measures the finished card including
            // that overhang, so this asks it rather than repeating a fraction
            // that has to be kept in step by hand.
            if (rowLift != null)
            {
                float lift = OrderBubbleSetup.CardHeight * chosenScale + .18f;

                rowLift.floatValue = Mathf.Max(lift, 1.1f);
                report.AppendLine("    arka sira balon yuksekligi: " +
                                  rowLift.floatValue.ToString("0.00"));
            }

            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }

        return report.ToString();
    }

    // Does the widest row still fit in the frame, and if not, what gives.
    //
    // Bubbles are shrunk first: the bodies are the thing that was asked to be
    // bigger, and a card is still legible at three quarters size while an
    // animal at three quarters size is the complaint we started from. Only when
    // the bodies alone overflow does this give up, and it then says which
    // height would have fitted rather than quietly picking one.
    private static string FitToScreen(FoodServingCustomerManager manager,
        Camera camera, int columns, CustomerScale.Body body, bool shrinkable,
        ref float chosenScale, SerializedObject so, SerializedProperty side)
    {
        if (camera == null || columns <= 0)
            return string.Empty;

        Vector3 sideDirection = columns > 1
            ? manager.SlotPosition(1) - manager.SlotPosition(0)
            : Vector3.Cross(manager.QueueDirection, Vector3.up);

        sideDirection.y = 0f;

        if (sideDirection.sqrMagnitude < .0001f)
            return "    yan yon okunamadi -- ekran kontrolu atlandi" +
                   System.Environment.NewLine;

        Vector3 centre = Vector3.zero;

        for (int i = 0; i < columns; i++)
            centre += manager.SlotPosition(i);

        centre /= columns;

        float available = CustomerScale.AvailableWidth(camera, centre,
            sideDirection, screenMargin);
        float widest = Mathf.Max(body.width,
            OrderBubbleSetup.CardWidth * chosenScale);
        float gap = sideGap - widest;
        float needed = columns * widest + (columns - 1) * gap;

        StringBuilder report = new StringBuilder();

        report.AppendLine("    ekran: " + needed.ToString("0.00") +
                          " birim gerekiyor, " + available.ToString("0.00") +
                          " birim var");

        if (needed <= available)
        {
            report.AppendLine("    -> sigiyor, " +
                              (available - needed).ToString("0.00") +
                              " birim pay kaldi");

            return report.ToString();
        }

        // The widest anybody may be if the row is to fit at this column count.
        float allowed = (available - (columns - 1) * gap) / columns;

        if (shrinkable && body.width <= allowed)
        {
            chosenScale = Mathf.Clamp(allowed / OrderBubbleSetup.CardWidth, .5f,
                wantedBubbleScale);

            sideGap = Mathf.Max(body.width + bodyClearance,
                OrderBubbleSetup.CardWidth * chosenScale + bubbleClearance);

            report.AppendLine("    -> sigmadi, balonlar " +
                              chosenScale.ToString("0.00") + " olcegine cekildi");
            report.AppendLine("    " + Stretch(side, sideGap, "Side Spacing"));
            so.ApplyModifiedProperties();

            return report.ToString();
        }

        // Nothing left to give: the animals themselves are too wide.
        float fits = customerHeight * allowed / Mathf.Max(body.width, .0001f);

        report.AppendLine("    -> SIGMIYOR. Govdelerin kendisi tasiyor.");
        report.AppendLine("       customerHeight " + customerHeight.ToString("0.00") +
                          " yerine " + fits.ToString("0.00") + " olsaydi sigardi;");
        report.AppendLine("       ya da yan yana sayisi " + columns + " yerine " +
                          (columns - 1) + " olmali.");

        return report.ToString();
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
