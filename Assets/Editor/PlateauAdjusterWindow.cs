using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Nudges the tray model inside the shared Plateau prefab. The swap tool fits a
// new model by size, but it cannot know which way up the artist exported it, so
// the last bit of orientation is done here by eye
public class PlateauAdjusterWindow : EditorWindow
{
    private Vector3 localPosition;
    private Vector3 localEuler;
    private Vector3 localScale = Vector3.one;

    private bool live = true;
    private string status;

    private const string sampleName = "SAMPLE Plateau";

    [MenuItem("Cooked Fast/Arac/Plateau Adjuster")]
    public static void Open()
    {
        PlateauAdjusterWindow window = GetWindow<PlateauAdjusterWindow>(true, "Plateau Ayari");
        window.minSize = new Vector2(320f, 300f);
        window.Reload();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Tabagin prefab icindeki durusu. Degisiklik butun plateau'lara gider: " +
            "player, musteriler, istasyonlar.", MessageType.None);

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        localPosition = EditorGUILayout.Vector3Field("Konum", localPosition);
        localEuler = EditorGUILayout.Vector3Field("Rotasyon", localEuler);
        localScale = EditorGUILayout.Vector3Field("Olcek", localScale);

        bool edited = EditorGUI.EndChangeCheck();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hizli cevir", EditorStyles.boldLabel);

        edited |= FlipRow("X", Vector3.right);
        edited |= FlipRow("Y", Vector3.up);
        edited |= FlipRow("Z", Vector3.forward);

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rotasyonu sifirla"))
            {
                localEuler = Vector3.zero;
                edited = true;
            }

            if (GUILayout.Button("Yeniden oku"))
                Reload();
        }

        live = EditorGUILayout.ToggleLeft("Canli uygula", live);

        if (!live && GUILayout.Button("Uygula", GUILayout.Height(24f)))
            Apply();

        EditorGUILayout.Space();

        if (GUILayout.Button("Prefab modunda ac (onerilen)", GUILayout.Height(26f)))
            OpenPrefab();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sahneye ornek koy", GUILayout.Height(24f)))
                AddSample();

            if (GUILayout.Button("Ornegi sil", GUILayout.Height(24f)))
                RemoveSample();
        }

        if (GUILayout.Button("Sahnedeki override'lari temizle"))
            ClearSceneOverrides();

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.Info);

        if (edited && live)
            Apply();
    }

    // Both directions, because which one reads as "the right way up" depends on
    // the model and guessing costs a round trip through the Scene view
    private bool FlipRow(string label, Vector3 axis)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(20f));

            if (GUILayout.Button("-90"))
            {
                localEuler = Rotate(axis, -90f);
                return true;
            }

            if (GUILayout.Button("+90"))
            {
                localEuler = Rotate(axis, 90f);
                return true;
            }

            if (GUILayout.Button("180"))
            {
                localEuler = Rotate(axis, 180f);
                return true;
            }
        }

        return false;
    }

    // Multiplying quaternions rather than adding euler angles, so a second flip
    // on a different axis still turns the model the way the button says
    private Vector3 Rotate(Vector3 axis, float degrees)
    {
        Quaternion rotated = Quaternion.Euler(localEuler) * Quaternion.AngleAxis(degrees, axis);

        return rotated.eulerAngles;
    }

    // Prefab Mode shows the plateau alone on an empty grid, which is by far the
    // clearest way to judge which way up it is. Editing the open stage rather
    // than the asset behind its back keeps the two from fighting
    private static GameObject GetOpenStageRoot()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

        return stage != null && stage.assetPath == PlateauSetup.plateauPrefabPath
            ? stage.prefabContentsRoot
            : null;
    }

    private void OpenPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlateauSetup.plateauPrefabPath);

        if (prefab == null)
        {
            status = "Plateau prefab bulunamadi";
            return;
        }

        AssetDatabase.OpenAsset(prefab);
        Reload();

        status = "Prefab modu acildi. Cikarken Ctrl+S";
    }

    private void Reload()
    {
        GameObject staged = GetOpenStageRoot();

        if (staged != null)
        {
            ReadFrom(staged, "Prefab modundan okundu");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlateauSetup.plateauPrefabPath);

        if (root == null)
        {
            status = "Plateau prefab acilamadi";
            return;
        }

        try
        {
            ReadFrom(root, "Okundu");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private void ReadFrom(GameObject root, string label)
    {
        MeshFilter filter = PlateauSetup.FindTargetFilter(root);

        if (filter == null)
        {
            status = "Prefab icinde mesh yok";
            return;
        }

        localPosition = filter.transform.localPosition;
        localEuler = filter.transform.localRotation.eulerAngles;
        localScale = filter.transform.localScale;

        status = label + ": " + (filter.sharedMesh == null ? "mesh yok" : filter.sharedMesh.name);
    }

    private void Apply()
    {
        GameObject staged = GetOpenStageRoot();

        if (staged != null)
        {
            if (!WriteTo(staged))
                return;

            EditorSceneManager.MarkSceneDirty(staged.scene);
            status = "Prefab modunda guncellendi (Ctrl+S)";
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlateauSetup.plateauPrefabPath);

        if (root == null)
        {
            status = "Plateau prefab acilamadi";
            return;
        }

        try
        {
            if (!WriteTo(root))
                return;

            PrefabUtility.SaveAsPrefabAsset(root, PlateauSetup.plateauPrefabPath);
            status = "Kaydedildi";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private bool WriteTo(GameObject root)
    {
        MeshFilter filter = PlateauSetup.FindTargetFilter(root);

        if (filter == null)
        {
            status = "Prefab icinde mesh yok";
            return false;
        }

        filter.transform.localPosition = localPosition;
        filter.transform.localRotation = Quaternion.Euler(localEuler);
        filter.transform.localScale = localScale;

        return true;
    }

    // A plateau lives inside a character's hand at roughly one hundredth scale,
    // which is far too small to judge by eye. This drops a full sized one right
    // where the Scene view is already looking
    private void AddSample()
    {
        GameObject existing = GameObject.Find(sampleName);

        if (existing != null)
        {
            Frame(existing);
            status = "Ornek zaten sahnede";
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlateauSetup.plateauPrefabPath);

        if (prefab == null)
        {
            status = "Plateau prefab bulunamadi";
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = sampleName;

        SceneView view = SceneView.lastActiveSceneView;

        // Right in front of the Scene view camera rather than at its pivot. The
        // pivot can sit anywhere the last orbit left it, including out in the
        // sky, which makes the preview look like a bug in the level
        instance.transform.position = view != null && view.camera != null
            ? view.camera.transform.position + view.camera.transform.forward * 3f
            : Vector3.zero;

        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = SampleScale(instance);

        AddSampleFood(instance);

        Undo.RegisterCreatedObjectUndo(instance, "Add Sample Plateau");
        EditorSceneManager.MarkSceneDirty(instance.scene);

        Frame(instance);
        status = "Ornek kondu. Ayari bitirince 'Ornegi sil'";
    }

    // The prefab is authored at roughly seventy five times the size it is drawn
    // at, because it hangs off a hand bone that shrinks it back down. Spawning
    // it at scale one puts a plate the size of a building in the level
    private Vector3 SampleScale(GameObject instance)
    {
        Plateau reference = FindRealPlateau();

        if (reference != null)
            return reference.transform.lossyScale;

        Renderer renderer = instance.GetComponentInChildren<Renderer>(true);

        if (renderer == null)
            return Vector3.one;

        float footprint = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z);

        // Roughly a hand sized plate, so it reads next to the rest of the level
        return footprint > .0001f ? Vector3.one * (.4f / footprint) : Vector3.one;
    }

    // Any plateau already in the scene knows the size the game actually draws
    private Plateau FindRealPlateau()
    {
        Plateau[] plateaus = Object.FindObjectsByType<Plateau>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Plateau plateau in plateaus)
        {
            if (plateau.name != sampleName)
                return plateau;
        }

        return null;
    }

    // Without something on it there is no way to tell whether the food sits on
    // the surface or floats above it, which is half of what is being tuned here
    private void AddSampleFood(GameObject instance)
    {
        FoodPosition foodPosition = instance.GetComponentInChildren<FoodPosition>(true);

        if (foodPosition == null)
            return;

        GameObject foodPrefab = FindFoodPrefab();

        if (foodPrefab == null)
            return;

        GameObject food = (GameObject)PrefabUtility.InstantiatePrefab(foodPrefab, foodPosition.transform);

        food.transform.localPosition = Vector3.zero;
        food.transform.localRotation = Quaternion.identity;
    }

    private GameObject FindFoodPrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Tiny Coffee Shop/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (candidate != null && candidate.GetComponent<SpawnableFood>() != null)
                return candidate;
        }

        return null;
    }

    private void RemoveSample()
    {
        GameObject existing = GameObject.Find(sampleName);

        if (existing == null)
        {
            status = "Sahnede ornek yok";
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        status = "Ornek silindi";
    }

    private void Frame(GameObject instance)
    {
        Selection.activeGameObject = instance;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    // An instance that carries its own transform override ignores the prefab, so
    // a plateau tuned by hand earlier would stay crooked while every other one
    // straightened out
    private void ClearSceneOverrides()
    {
        Plateau[] plateaus = Object.FindObjectsByType<Plateau>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        List<string> cleared = new List<string>();

        foreach (Plateau plateau in plateaus)
        {
            MeshFilter filter = plateau.GetComponentInChildren<MeshFilter>(true);

            if (filter == null || !PrefabUtility.IsPartOfPrefabInstance(filter))
                continue;

            PrefabUtility.RevertObjectOverride(
                filter.transform, InteractionMode.AutomatedAction);

            cleared.Add(plateau.transform.parent == null
                ? plateau.name
                : plateau.transform.parent.name + "/" + plateau.name);
        }

        status = cleared.Count <= 0
            ? "Sahnede override yok"
            : "Temizlendi: " + string.Join(", ", cleared);
    }
}
