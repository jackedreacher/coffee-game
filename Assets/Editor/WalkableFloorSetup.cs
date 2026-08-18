#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Marks the floor the player is allowed to walk on.
//
// Works off the selection rather than off a guess, because which slab is the
// kitchen and which is the customer side is a question about the level that
// only the person who built it can answer. The console names the object on
// every refused tap, so the loop is: tap, read the name, select it, run this
public static class WalkableFloorSetup
{
    [MenuItem("Cooked Fast/Etkilesim/Yurunebilir Zemin Yap", priority = 219)]
    public static void Mark()
    {
        GameObject[] chosen = Selection.gameObjects;

        if (chosen == null || chosen.Length <= 0)
        {
            Show("Once Hierarchy'den zemin objelerini sec.\n\n" +
                 "Hangisi oldugunu bilmiyorsan: oyunu oynat, gitmek istedigin\n" +
                 "yere tikla, konsolda cikan \"bos zemin: <isim>\" satirindaki\n" +
                 "ismi Hierarchy'de ara.");
            return;
        }

        // Asked once, up front, for the whole selection. A tap is a raycast and
        // a mesh with no collider is not there as far as the ray is concerned:
        // marking such a floor looks done and does nothing at all, which is the
        // worst of the three possible outcomes
        int bare = 0;

        foreach (GameObject target in chosen)
        {
            if (target != null && target.GetComponentInChildren<Collider>(true) == null)
                bare++;
        }

        bool addColliders = bare <= 0 || EditorUtility.DisplayDialog("Yurunebilir Zemin",
            bare + " objede Collider yok. Onlarsiz tiklama isini zeminden geciyor,\n" +
            "yani isaretlemenin bir etkisi olmaz.\n\n" +
            "Mesh Collider ekleyeyim mi?",
            "Ekle", "Sadece isaretle");

        string report = "";
        int added = 0;

        foreach (GameObject target in chosen)
        {
            if (target == null)
                continue;

            // Marking and covering are two separate jobs, and this used to skip
            // straight past the second one whenever the first was already done.
            // Re-running on a floor marked earlier reported "already walkable"
            // and left it with nothing for the ray to hit -- which is the one
            // state that looks finished and works not at all
            WalkableFloor above = target.GetComponentInParent<WalkableFloor>();

            string note;

            if (above == null)
            {
                Undo.AddComponent<WalkableFloor>(target);
                note = "yurunebilir";
                added++;
            }
            else
            {
                note = above.gameObject == target
                    ? "zaten isaretli"
                    : "zaten isaretli (" + above.name + " uzerinden)";
            }

            if (target.GetComponentInChildren<Collider>(true) != null)
            {
                report += "- " + target.name + ": " + note + "\n";
                continue;
            }

            if (!addColliders)
            {
                report += "- " + target.name + ": " + note +
                          " ama COLLIDER YOK -- isin uzerinden gecer\n";
                continue;
            }

            int meshes = Cover(target);

            if (meshes > 0)
            {
                report += "- " + target.name + ": " + note + ", " +
                          meshes + " Mesh Collider eklendi\n";
                added++;
            }
            else
            {
                report += "- " + target.name + ": " + note +
                          " ama MESH DE YOK -- collider eklenecek bir sey bulunamadi\n";
            }
        }

        if (added > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Show(report + "\n" + Summary());
    }

    [MenuItem("Cooked Fast/Etkilesim/Yurunebilir Zemini Kaldir", priority = 220)]
    public static void Unmark()
    {
        GameObject[] chosen = Selection.gameObjects;

        if (chosen == null || chosen.Length <= 0)
        {
            Show("Once Hierarchy'den objeleri sec.");
            return;
        }

        string report = "";

        foreach (GameObject target in chosen)
        {
            if (target == null || !target.TryGetComponent(out WalkableFloor floor))
                continue;

            Undo.DestroyObjectImmediate(floor);
            report += "- " + target.name + ": kaldirildi\n";
        }

        if (string.IsNullOrEmpty(report))
            report = "Secilenlerde WalkableFloor yoktu.\n";

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Show(report + "\n" + Summary());
    }

    // Not called List. This class uses List<string>, and a method sharing a
    // name with a type it also uses is exactly the collision that stopped the
    // whole editor assembly compiling once already
    [MenuItem("Cooked Fast/Etkilesim/Yurunebilir Zeminleri Listele", priority = 221)]
    public static void ListAll()
    {
        Show(Summary());
    }

    // Gives the meshes under this object something for the ray to hit.
    //
    // Mesh Collider rather than a box, and not convex: this is a floor. It never
    // moves and nothing is simulated against it, so the exact shape costs
    // nothing and a box would round off every cut-out and doorway in it.
    //
    // One per MeshFilter, on the same object as the mesh. Putting a single
    // collider on the parent would only ever cover one of the children
    private static int Cover(GameObject target)
    {
        int made = 0;

        foreach (MeshFilter mesh in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh == null || mesh.sharedMesh == null)
                continue;

            if (mesh.TryGetComponent(out Collider _))
                continue;

            MeshCollider collider = Undo.AddComponent<MeshCollider>(mesh.gameObject);
            collider.sharedMesh = mesh.sharedMesh;

            made++;
        }

        return made;
    }

    private static string Summary()
    {
        WalkableFloor[] floors = Object.FindObjectsByType<WalkableFloor>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (floors.Length <= 0)
        {
            return "SAHNEDE HIC YURUNEBILIR ZEMIN YOK.\n" +
                   "Oyuncu sadece istasyonlara tiklayarak hareket edebilir.";
        }

        List<string> lines = new List<string>();

        foreach (WalkableFloor floor in floors)
        {
            if (floor == null)
                continue;

            string fault = "";

            if (floor.GetComponentInChildren<Collider>(true) == null)
                fault = "  -- COLLIDER YOK, calismaz";
            else if (!floor.gameObject.activeInHierarchy)
                fault = "  -- obje kapali";

            lines.Add("  " + floor.name + fault);
        }

        return "YURUNEBILIR ZEMINLER (" + lines.Count + ")\n" + string.Join("\n", lines);
    }

    private static void Show(string report)
    {
        Debug.Log("Yurunebilir Zemin\n" + report);
        EditorUtility.DisplayDialog("Yurunebilir Zemin", report, "Tamam");
    }
}
#endif
