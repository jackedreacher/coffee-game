using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// "musteri bulunamadi" is logged before any serving rule is even consulted: the
// tap never resolved to a customer in the first place. That has one common
// cause -- TapToServe's list of customer managers being empty or out of date,
// which leaves only direct hits on a customer's own collider working, and in an
// isometric view the ray usually lands on the counter in front of them instead
public static class ServeSetup
{
    [MenuItem("Cooked Fast/Servis: Musteri Tikini Kontrol Et (bagla)", priority = 160)]
    public static void Check()
    {
        TapToServe tap = UnityEngine.Object.FindFirstObjectByType<TapToServe>(FindObjectsInactive.Include);

        if (tap == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Sahnede TapToServe yok. Player uzerinde olmali.", "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("TapToServe: " + Path(tap.transform));
        report.AppendLine();

        SerializedObject so = new SerializedObject(tap);
        SerializedProperty list = so.FindProperty("customerManagers");

        report.AppendLine("Bagli musteri yoneticileri: " + list.arraySize);

        for (int i = 0; i < list.arraySize; i++)
        {
            Object value = list.GetArrayElementAtIndex(i).objectReferenceValue;

            report.AppendLine("  " + (value == null ? "BOS" : value.name));
        }

        FoodServingCustomerManager[] found =
            UnityEngine.Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        report.AppendLine();
        report.AppendLine("Sahnede bulunan: " + found.Length);

        foreach (FoodServingCustomerManager manager in found)
            report.AppendLine("  " + Path(manager.transform) + Trigger(manager));

        // Never down to nothing. Rewriting the list is right when the scene has
        // counters in it, but an empty result means the search failed, not that
        // the counters are gone -- and clearing the list would break serving
        // outright while looking like a fix
        if (found.Length <= 0)
        {
            report.AppendLine();
            report.AppendLine("Sahnede hic FoodServingCustomerManager yok.");
            report.AppendLine("Mevcut liste OLDUGU GIBI birakildi -- silmek isleri bozardi.");

            Debug.LogWarning("[Servis kontrolu]\n" + report);
            EditorUtility.DisplayDialog("Servis Kontrolu", report.ToString(), "Tamam");
            return;
        }

        Undo.RecordObject(tap, "Musteri yoneticilerini bagla");

        list.arraySize = found.Length;

        for (int i = 0; i < found.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];

        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(tap.gameObject.scene);

        report.AppendLine();
        report.AppendLine("-> hepsi baglandi (" + found.Length + ")");
        report.AppendLine();

        SerializedObject after = new SerializedObject(tap);

        float radius = after.FindProperty("tapRadius").floatValue;

        report.AppendLine("Tap Radius (dunya birimi): " + radius.ToString("0.00"));
        report.AppendLine("  Isinin dustugu noktadan bu kadar uzaktaki musteri sayilir.");
        report.AppendLine();
        report.AppendLine("Tap Screen Radius (piksel): " +
                          after.FindProperty("tapScreenRadius").floatValue.ToString("0") +
                          "   <-- ASIL BU KARAR VERIYOR");
        report.AppendLine("  Parmagin ekranda musterinin bu kadar yakinindaysa sayilir.");
        report.AppendLine("  Tik gecmiyorsa once BUNU buyut: 90 -> 140 -> 200.");
        report.AppendLine("  Yanlis musteriye gidiyorsa kucult.");
        report.AppendLine();
        report.AppendLine("Tap Aim Height: " +
                          after.FindProperty("tapAimHeight").floatValue.ToString("0.00") +
                          "  (0 = ayaklari, 0.7 = govdesi)");
        report.AppendLine();
        report.AppendLine("Servis mesafesi (Serve Range): " +
                          after.FindProperty("serveRange").floatValue.ToString("0.00"));

        int withoutTrigger = 0;

        foreach (FoodServingCustomerManager manager in found)
        {
            if (!HasTrigger(manager))
                withoutTrigger++;
        }

        if (withoutTrigger > 0)
        {
            report.AppendLine();
            report.AppendLine("UYARI: " + withoutTrigger + " yoneticide trigger collider yok.");
            report.AppendLine("  Tik musteriyi bulsa bile yemek el degistirmez --");
            report.AppendLine("  oyuncunun o tezgahin trigger'inin ICINDE durmasi gerekiyor.");
        }

        Debug.Log("[Servis kontrolu]\n" + report);
        EditorUtility.DisplayDialog("Servis Kontrolu", report.ToString(), "Tamam");
    }

    private static string Trigger(FoodServingCustomerManager manager)
    {
        return HasTrigger(manager) ? "" : "   <-- trigger collider YOK";
    }

    private static bool HasTrigger(FoodServingCustomerManager manager)
    {
        foreach (Collider candidate in manager.GetComponents<Collider>())
        {
            if (candidate.isTrigger)
                return true;
        }

        return false;
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
}
