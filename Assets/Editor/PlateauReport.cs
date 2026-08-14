using System.Text;
using UnityEngine;
using UnityEditor;

// Prints everything that decides whether the tray is on screen, so a missing
// plateau can be read off one console line instead of guessed at
public static class PlateauReport
{
    [MenuItem("Cooked Fast/Report Plateau State")]
    public static void Report()
    {
        StringBuilder text = new StringBuilder();

        PlayerController[] controllers = Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (controllers.Length <= 0)
        {
            Debug.LogError("[PlateauReport] Sahnede PlayerController yok");
            return;
        }

        Transform player = controllers[0].transform;

        text.AppendLine("PLAYER: " + Path(player));
        text.AppendLine("  world " + player.position.ToString("0.000") +
                        "  lossyScale " + player.lossyScale.ToString("0.0000"));
        text.AppendLine();

        Plateau[] plateaus = player.GetComponentsInChildren<Plateau>(true);

        text.AppendLine("PLATEAU sayisi: " + plateaus.Length);

        foreach (Plateau plateau in plateaus)
            Describe(text, plateau);

        text.AppendLine();
        text.AppendLine("HOLD FOOD ABILITY");

        if (player.TryGetComponent(out HoldFoodAbility hold))
        {
            SerializedProperty property = new SerializedObject(hold).FindProperty("plateau");
            Object linked = property != null ? property.objectReferenceValue : null;

            text.AppendLine("  plateau alani: " + (linked == null ? "BOS" : linked.name));
        }
        else
        {
            text.AppendLine("  bilesen yok");
        }

        text.AppendLine();

        // The live body, not the retired one still parked next to it switched
        // off. Dumping the wrong one is what sent the tray into a hidden model
        Animator animator = null;

        foreach (Animator candidate in player.GetComponentsInChildren<Animator>(true))
        {
            text.AppendLine("MODEL: " + candidate.name +
                            "  aktif " + candidate.gameObject.activeInHierarchy);

            if (animator == null && candidate.gameObject.activeInHierarchy)
                animator = candidate;
        }

        text.AppendLine();
        text.AppendLine("KEMIKLER (aktif model, derinlik / isim / lossyScale)");

        if (animator == null)
        {
            text.AppendLine("  Aktif Animator yok");
        }
        else
        {
            foreach (Transform bone in animator.transform.GetComponentsInChildren<Transform>(true))
            {
                if (bone.GetComponentInParent<Plateau>() != null)
                    continue;

                text.AppendLine("  " + Depth(bone, animator.transform) + "  " + bone.name +
                                "  " + bone.lossyScale.ToString("0.0000"));
            }
        }

        Debug.Log("[PlateauReport]\n" + text);
    }

    // Customers only ever exist as prefabs until the game spawns them, so the
    // only way to check them is to open each one
    [MenuItem("Cooked Fast/Report Customer Plateaus")]
    public static void ReportCustomers()
    {
        StringBuilder text = new StringBuilder();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PlateauAttach.customersFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                text.AppendLine(System.IO.Path.GetFileNameWithoutExtension(path));

                Plateau plateau = root.GetComponentInChildren<Plateau>(true);

                if (plateau == null)
                {
                    text.AppendLine("  Plateau yok");
                    continue;
                }

                Transform visual = PlateauAttach.FindVisual(root.transform);
                Transform hand = PlateauAttach.FindHand(visual, plateau.transform);

                text.AppendLine("  gorunen model: " + visual.name);
                text.AppendLine("  plateau ebeveyni: " +
                                (plateau.transform.parent == null ? "YOK" : plateau.transform.parent.name));
                text.AppendLine("  secilen kemik: " + (hand == null ? "BULUNAMADI" : hand.name));
                text.AppendLine("  local pos " + plateau.transform.localPosition.ToString("0.0000") +
                                "  scale " + plateau.transform.localScale.ToString("0.0000"));

                Renderer renderer = plateau.GetComponentInChildren<Renderer>(true);

                text.AppendLine("  tabak boyutu: " +
                                (renderer == null ? "renderer yok" : renderer.bounds.size.ToString("0.0000")));

                if (hand == null)
                {
                    text.AppendLine("  KEMIKLER:");

                    foreach (Transform bone in visual.GetComponentsInChildren<Transform>(true))
                    {
                        if (bone.GetComponentInParent<Plateau>() != null)
                            continue;

                        text.AppendLine("    " + Depth(bone, visual) + "  " + bone.name);
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log("[PlateauReport] Musteriler\n" + text);
    }

    private static void Describe(StringBuilder text, Plateau plateau)
    {
        Transform transform = plateau.transform;

        text.AppendLine();
        text.AppendLine("  " + Path(transform));
        text.AppendLine("    aktif: " + transform.gameObject.activeInHierarchy +
                        " (kendi: " + transform.gameObject.activeSelf + ")");
        text.AppendLine("    ebeveyn: " + (transform.parent == null ? "YOK" : transform.parent.name));
        text.AppendLine("    local pos " + transform.localPosition.ToString("0.0000") +
                        "  scale " + transform.localScale.ToString("0.0000") + Mirrored(transform.localScale));
        text.AppendLine("    world pos " + transform.position.ToString("0.000") +
                        "  lossyScale " + transform.lossyScale.ToString("0.000000") +
                        Mirrored(transform.lossyScale));

        // Every node between the bone and the food contributes, so the one
        // carrying the minus sign has to be named rather than hunted for
        foreach (Transform step in transform.GetComponentsInChildren<Transform>(true))
        {
            if (step == transform)
                continue;

            if (Mirrored(step.localScale).Length > 0)
                text.AppendLine("    " + step.name + " scale " +
                                step.localScale.ToString("0.0000") + Mirrored(step.localScale));
        }

        Renderer[] renderers = transform.GetComponentsInChildren<Renderer>(true);

        text.AppendLine("    renderer sayisi: " + renderers.Length);

        foreach (Renderer renderer in renderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            text.AppendLine("      " + renderer.name +
                            "  enabled " + renderer.enabled +
                            "  aktif " + renderer.gameObject.activeInHierarchy +
                            "  mesh " + (filter == null || filter.sharedMesh == null ? "YOK" : filter.sharedMesh.name) +
                            "  mat " + (renderer.sharedMaterial == null ? "YOK" : renderer.sharedMaterial.name));
            text.AppendLine("        bounds merkez " + renderer.bounds.center.ToString("0.000") +
                            "  boyut " + renderer.bounds.size.ToString("0.0000"));
        }

        DescribeFood(text, transform);
    }

    // Run this in play mode while carrying something. Whether the food exists,
    // is switched on, has its renderer enabled and what size it ended up is the
    // whole question when nothing shows on the tray
    private static void DescribeFood(StringBuilder text, Transform plateau)
    {
        FoodPosition[] slots = plateau.GetComponentsInChildren<FoodPosition>(true);

        text.AppendLine("    food position sayisi: " + slots.Length);

        foreach (FoodPosition slot in slots)
        {
            text.AppendLine("      " + slot.name +
                            "  bos " + slot.IsEmpty +
                            "  localPos " + slot.transform.localPosition.ToString("0.0000") +
                            "  localScale " + slot.transform.localScale.ToString("0.0000"));

            if (slot.transform.childCount <= 0)
            {
                text.AppendLine("        icerik yok");
                continue;
            }

            foreach (Transform child in slot.transform)
            {
                Renderer renderer = child.GetComponentInChildren<Renderer>(true);

                text.AppendLine("        " + child.name +
                                "  aktif " + child.gameObject.activeInHierarchy +
                                "  localPos " + child.localPosition.ToString("0.0000") +
                                "  localScale " + child.localScale.ToString("0.0000"));
                text.AppendLine("          world " + child.position.ToString("0.000") +
                                "  lossyScale " + child.lossyScale.ToString("0.0000"));
                text.AppendLine("          renderer " +
                                (renderer == null
                                    ? "YOK"
                                    : renderer.enabled + "  boyut " + renderer.bounds.size.ToString("0.0000")));
            }
        }
    }

    // A negative scale mirrors the mesh. It reads as "upside down" and no amount
    // of rotating fixes it, which is worth saying out loud next to the number
    private static string Mirrored(Vector3 scale)
    {
        bool negative = scale.x < 0f || scale.y < 0f || scale.z < 0f;

        return negative ? "  <-- NEGATIF: mesh aynalanir, rotasyonla duzelmez" : "";
    }

    private static string Path(Transform transform)
    {
        string path = transform.name;

        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private static int Depth(Transform transform, Transform root)
    {
        int depth = 0;

        while (transform != root && transform.parent != null)
        {
            transform = transform.parent;
            depth++;
        }

        return depth;
    }
}
