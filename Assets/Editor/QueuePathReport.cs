#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Checks the floor under every standing spot, and the walk to it.
//
// The queue slots are arithmetic: a start point plus two offsets, worked out
// once and never questioned. Nothing has ever checked that the floor they land
// on is floor a customer can actually stand on, and there are three separate
// ways for that to be false while the scene looks perfectly correct.
//
// It matters because of how each one LOOKS. A slot off the navigation mesh, or
// close enough to its edge that the agent's own body will not fit, gives a path
// that ends somewhere the customer cannot reach -- and an agent that cannot
// finish its path keeps steering at it. From the outside that is not "the spot
// is wrong", it is a customer weaving left and right on the way over
public static class QueuePathReport
{
    [MenuItem("Cooked Fast/Musteri/Kuyruk Yolunu Denetle", priority = 604)]
    public static void Audit()
    {
        FoodServingCustomerManager[] counters = Object.FindObjectsByType<FoodServingCustomerManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (counters.Length <= 0)
        {
            Show("Sahnede FoodServingCustomerManager yok.");
            return;
        }

        // The mesh's own figure, not the prefab's. What decides whether a spot
        // is standable is the radius the NavMesh was BAKED with -- the agent's
        // own radius only has to be no bigger than it
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);

        float radius = settings.agentRadius;

        StringBuilder report = new StringBuilder();

        report.AppendLine("NavMesh yaricapi: " + radius.ToString("0.00") +
                          "  (zemin bu genislikte bir govdeye gore kesilmis)");
        report.AppendLine();

        bool mismatch = false;

        report.Append(Bodies(radius, ref mismatch));

        int bad = 0;

        foreach (FoodServingCustomerManager counter in counters)
        {
            report.AppendLine(counter.name + (counter.Closed ? "  (KAPALI)" : ""));

            Transform spawn = counter.SpawnPoint;

            if (spawn == null)
            {
                report.AppendLine("  Spawn Point YOK -- yol hesaplanamaz");
                report.AppendLine();
                continue;
            }

            report.Append(Slot(counter, spawn.position, radius, ref bad, out float worst));

            // Only when it is actually costing something. A row that already
            // arrives square does not need advice about where it starts from
            if (worst > 25f)
                report.Append(SpawnAdvice(counter, spawn));

            report.AppendLine();
        }

        if (bad > 0)
        {
            report.AppendLine(bad + " durakta sorun var. Yukaridaki satirlar");
            report.AppendLine("  hangisi oldugunu soyluyor.");
        }
        else if (mismatch)
        {
            report.AppendLine("Duraklarin yeri ve yollari temiz -- yol kus ucusuyla");
            report.AppendLine("  ayni, kirilma yok. Yani yalpalama YOLDAN gelmiyor.");
            report.AppendLine("  Geriye govde genisligi kaliyor, yukarida yazdi.");
        }
        else
        {
            report.AppendLine("Duraklar da govdeler de temiz. Yalpalama ne yoldan");
            report.AppendLine("  ne olcuden geliyor -- sonraki suphe yurume klibi.");
        }

        report.AppendLine();
        report.AppendLine("Ne demek");
        report.AppendLine("  MESHTE YOK    Durak zeminin YANINDA kaliyor. Ajan oraya");
        report.AppendLine("                varamaz, en yakin noktada takilir ve");
        report.AppendLine("                hedefe dogru itiklemeye devam eder.");
        report.AppendLine("                (Dikey fark onemli degil, bakilmiyor.)");
        report.AppendLine("  KENARA YAKIN  Durak zeminde ama govdesi sigmiyor.");
        report.AppendLine("                Ayni sey: varamadigi icin durmaz.");
        report.AppendLine("  YOL EKSIK     Spawn'dan oraya tam yol yok.");
        report.AppendLine("  DOLAMBACLI    Yol kus ucusunun cok ustunde ya da");
        report.AppendLine("                gereginden fazla kirilma iceriyor --");
        report.AppendLine("                govde her kirilmada yon degistirir.");
        report.AppendLine("  GOVDE GENIS   Ajan, zeminin hesapladigindan kalin.");
        report.AppendLine("                Yol duz olsa bile birbirlerini iter.");
        report.AppendLine("  SON DONUS     Vardiginda kac derece donmesi gerekiyor.");
        report.AppendLine("                45'i gecerse duruken donmek zorunda kalir");
        report.AppendLine("                ve kayarak donuyor gibi gorunur.");
        report.AppendLine("                Cozumu kodda degil: Spawn Point'i EN SON");
        report.AppendLine("                duragin arkasina, kuyrukla ayni hatta koy.");
        report.AppendLine("                O zaman yuruyusun yonu durus yonu olur.");
        report.AppendLine();
        report.AppendLine("Duzeltmek: tezgahin Queue Start Point'ini kaydir,");
        report.AppendLine("  ya da Side Spacing'i kucult. Zemin dar geliyorsa");
        report.AppendLine("  Cooked Fast > Etkilesim > Yurunebilir Zemin Yap ile");
        report.AppendLine("  o parcayi da zemine kat ve NavMesh'i tekrar bake et.");
        report.AppendLine("  Govde genisligi icin: Musteri > Yuruyusu Sakinlestir.");

        Show(report.ToString());
    }

    private const string customersFolder = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";

    // The body against the floor it walks on, which are two different numbers
    // and nothing in Unity makes them agree.
    //
    // The mesh is cut back from every wall by the BAKE radius. The agent shoves
    // other agents around using its OWN radius. Bake at 0.20 and then walk a
    // 0.50 wide body over it and every customer is two and a half times wider
    // than the floor was planned for -- so two of them in a queue are inside
    // each other's space long before they look anywhere near touching, and the
    // avoidance solver spends the entire walk prising them apart.
    //
    // That is a sway no amount of path checking will ever find, because the
    // path is perfectly straight the whole time. Which is exactly what the
    // first run of this report said
    private static string Bodies(float baked, ref bool mismatch)
    {
        StringBuilder report = new StringBuilder();

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { customersFolder });

        if (guids.Length <= 0)
        {
            report.AppendLine("Musteri prefabi bulunamadi: " + customersFolder);
            report.AppendLine();

            return report.ToString();
        }

        report.AppendLine("Govdeler");

        // Reported once for the set rather than once per rabbit. They are seven
        // colours of the same prefab and seven identical lines is a wall of text
        // that hides the one line that differs
        float widest = 0f;
        int counted = 0;

        foreach (string guid in guids)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (prefab == null)
                continue;

            NavMeshAgent agent = prefab.GetComponentInChildren<NavMeshAgent>(true);

            if (agent == null)
            {
                report.AppendLine("  " + prefab.name + ": NavMeshAgent yok");
                continue;
            }

            counted++;

            if (agent.radius > widest)
                widest = agent.radius;
        }

        if (counted <= 0)
            return report.ToString();

        report.AppendLine("  " + counted + " musteri prefabi, en genisi " +
                          widest.ToString("0.00") + " yaricap");

        // A fifth of slack, because a body a hair wider than the bake is a
        // rounding difference and not a design mistake
        if (widest > baked * 1.2f)
        {
            mismatch = true;

            report.AppendLine("  GOVDE GENIS: zemin " + baked.ToString("0.00") +
                              "'e gore kesilmis, govde " + widest.ToString("0.00") +
                              " -- " + (widest / baked).ToString("0.0") + " kat.");
            report.AppendLine("  Yol duz olsa bile birbirlerini itiyorlar; en uzaga");
            report.AppendLine("  gidenin yolu en kalabalik oldugu icin en cok o sallanir.");
        }
        else
        {
            report.AppendLine("  zeminle uyumlu");
        }

        report.AppendLine();

        return report.ToString();
    }

    // Every slot of one counter. Written as one block so the numbers for a
    // counter stay together in the report -- reading them interleaved is how a
    // problem at slot three gets blamed on slot one
    private static string Slot(FoodServingCustomerManager counter, Vector3 from,
        float radius, ref int bad, out float worstTurn)
    {
        StringBuilder report = new StringBuilder();

        worstTurn = 0f;

        for (int i = 0; i < counter.Slots; i++)
        {
            Vector3 slot = counter.SlotPosition(i);

            string trouble = "";

            // On the mesh at all. A metre of tolerance, which is far more than
            // the agent would ever be nudged -- anything worse than that is not
            // a rounding problem, it is a spot in the wrong place
            if (!NavMesh.SamplePosition(slot, out NavMeshHit onMesh, 1f, NavMesh.AllAreas))
            {
                trouble += "  MESHTE YOK";
                bad++;
            }
            else
            {
                // Sideways only.
                //
                // The first version of this compared the raw distance and
                // flagged every slot in the scene for being eight centimetres
                // out -- which was the slot sitting at y 0.1 and the mesh lying
                // on the floor beneath it. That difference is not a problem and
                // never was: an agent is placed on the surface under its
                // destination, so only a slot that misses the floor SIDEWAYS is
                // a slot in the wrong place
                Vector3 flat = onMesh.position - slot;

                float sideways = new Vector2(flat.x, flat.z).magnitude;

                if (sideways > .05f)
                {
                    trouble += "  MESHTE YOK (" + sideways.ToString("0.00") + " yana)";
                    bad++;
                }

                // Worth saying out loud only when it is big enough to mean the
                // spot was authored at the wrong height rather than just above
                // the floor like everything else
                if (Mathf.Abs(flat.y) > .5f)
                {
                    trouble += "  ZEMIN " + Mathf.Abs(flat.y).ToString("0.0") +
                               (flat.y < 0f ? " ALTTA" : " USTTE");
                    bad++;
                }

                // Room to stand, which is a different question from being on the
                // mesh. The baked floor already stops a radius short of every
                // wall, so a spot inside that margin is one the agent's own body
                // does not fit into -- it gets pushed out and steers back in
                if (NavMesh.FindClosestEdge(onMesh.position, out NavMeshHit edge, NavMesh.AllAreas)
                    && edge.distance < radius)
                {
                    trouble += "  KENARA YAKIN (" + edge.distance.ToString("0.00") +
                               " < " + radius.ToString("0.00") + ")";
                    bad++;
                }
            }

            NavMeshPath path = new NavMeshPath();

            float straight = Vector3.Distance(from.With(y: 0), slot.With(y: 0));
            float walked = 0f;
            int corners = 0;

            if (!NavMesh.CalculatePath(from, slot, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                trouble += "  YOL EKSIK (" + path.status + ")";
                bad++;
            }
            else
            {
                corners = path.corners.Length;

                for (int c = 1; c < corners; c++)
                    walked += Vector3.Distance(path.corners[c - 1], path.corners[c]);

                // Two corners is a straight line from here to there. Three is
                // one turn, which is a normal way round a counter. Past that,
                // or a walk half again as long as the crow flies, is the agent
                // threading something -- and every corner is a turn the body
                // makes on the way
                if (corners > 4 || (straight > .5f && walked > straight * 1.5f))
                {
                    trouble += "  DOLAMBACLI";
                    bad++;
                }

                // How far round they still have to turn once they get there.
                //
                // The last leg of the path is the direction they arrive
                // travelling; the queue facing is the direction they have to
                // end up pointing. The difference is a turn that has to happen
                // while they are stopping, and past about a quarter of a circle
                // there is no smoothing that makes it read as walking in -- the
                // body is going one way and facing another, which is the thing
                // that looks like sliding.
                //
                // This one is not fixed in code. It is fixed by where the spawn
                // point sits: put it behind the LAST slot, in line with the
                // queue, and the walk ends pointing the right way by itself
                if (corners >= 2)
                {
                    Vector3 arriving = path.corners[corners - 1] - path.corners[corners - 2];
                    Vector3 facing = -counter.QueueDirection;

                    arriving.y = 0f;
                    facing.y = 0f;

                    if (arriving.sqrMagnitude > .0001f && facing.sqrMagnitude > .0001f)
                    {
                        float turn = Vector3.Angle(arriving.normalized, facing.normalized);

                        if (turn > worstTurn)
                            worstTurn = turn;

                        trouble += "  son donus " + turn.ToString("0") + " derece";

                        if (turn > 45f)
                        {
                            trouble += " COK";
                            bad++;
                        }
                    }
                }
            }

            report.AppendLine("  durak " + i + "  " + slot.ToString("0.0") +
                              "   yol " + walked.ToString("0.0") +
                              " / kus ucusu " + straight.ToString("0.0") +
                              ", " + corners + " kirilma" +
                              (trouble.Length <= 0 ? "   temiz" : trouble));
        }

        return report.ToString();
    }

    // Where the spawn point would have to sit for the walk to end pointing the
    // right way on its own.
    //
    // The turn at the end is not a code problem and no amount of smoothing
    // removes it: the direction a customer arrives travelling is decided by
    // where they set off from, and nothing later can undo that. Start them
    // behind the MIDDLE of the row and far enough back, and every slot becomes
    // a shallow lean instead of a swerve.
    //
    // The distance is arithmetic rather than taste. The outermost slot sits
    // half a row off the middle, and the angle it is approached at is that
    // offset over the distance walked -- so the distance that keeps the worst
    // one near 25 degrees is the offset divided by the tangent of 25
    private static string SpawnAdvice(FoodServingCustomerManager counter, Transform spawn)
    {
        Vector3 back = counter.QueueDirection;

        back.y = 0f;

        if (back.sqrMagnitude < .0001f)
            return "";

        back = back.normalized;

        Vector3 middle = Vector3.zero;

        for (int i = 0; i < counter.Slots; i++)
            middle += counter.SlotPosition(i);

        middle /= counter.Slots;

        // How far the outermost slot sits from the middle, measured ACROSS the
        // walk. Only that part costs anything -- the part along the walk is
        // just a longer or shorter approach
        float spread = 0f;

        for (int i = 0; i < counter.Slots; i++)
        {
            Vector3 offset = counter.SlotPosition(i) - middle;

            offset.y = 0f;

            float across = Vector3.ProjectOnPlane(offset, back).magnitude;

            if (across > spread)
                spread = across;
        }

        // tan(25 degrees). Floored, so a single file queue does not get told to
        // start on top of itself
        float distance = Mathf.Max(2f, spread / .466f);

        Vector3 wanted = middle + back * distance;

        StringBuilder report = new StringBuilder();

        report.AppendLine("  SPAWN POINT ONERISI  " + wanted.ToString("0.0"));
        report.AppendLine("    su an " + spawn.position.ToString("0.0") +
                          " -- siranin yaninda ve bir ucunda.");
        report.AppendLine("    Onerilen nokta siranin ORTASININ " +
                          distance.ToString("0.0") + " birim arkasi.");
        report.AppendLine("    Oradan en uzak duraga gidis bile 25 derecelik bir");
        report.AppendLine("    egim olur -- yani donus degil, yonelme.");

        // Said out loud, because a suggestion that cannot be walked to is worse
        // than no suggestion: it looks right in the inspector and produces a
        // customer who never sets off
        if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            report.AppendLine("    DIKKAT: orasi zeminin disinda, yakininda da");
            report.AppendLine("    yurunebilir yer yok. Once zemini genislet.");

            return report.ToString();
        }

        float off = Vector3.Distance(hit.position.With(y: 0), wanted.With(y: 0));

        if (off > .5f)
            report.AppendLine("    DIKKAT: tam orasi zemin degil, en yakin" +
                              " yurunebilir yer " + hit.position.ToString("0.0"));

        return report.ToString();
    }

    private static void Show(string report)
    {
        Debug.Log("[Kuyruk]\n" + report);
        EditorUtility.DisplayDialog("Kuyruk Yolu", report, "Tamam");
    }
}
#endif
