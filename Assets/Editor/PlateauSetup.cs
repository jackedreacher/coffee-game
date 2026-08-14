using UnityEngine;
using UnityEditor;

// Swaps the model the shared Plateau prefab draws. Every tray in the game --
// the player's, every customer's, every station's -- is an instance of that one
// prefab, so this is the single place the look lives
public static class PlateauSetup
{
    public const string plateauPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Plateau.prefab";
    private const string plateModelPath = "Assets/Tiny Coffee Shop/FBX/Environment_Plate.fbx";
    private const string originalModelPath = "Assets/Tiny Coffee Shop/Models/Props/Plateau.fbx";

    [MenuItem("Cooked Fast/Use Plate As Plateau")]
    public static void UsePlate()
    {
        Swap(plateModelPath);
    }

    [MenuItem("Cooked Fast/Restore Original Plateau Model")]
    public static void RestoreOriginal()
    {
        Swap(originalModelPath);
    }

    // Writes the plate's own transform into the shared prefab, so a size and
    // angle settled on against one character apply to every tray in the game.
    // Deliberately dumb: it copies the numbers given to it and works nothing out
    public static string WriteVisual(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(plateauPrefabPath);

        if (root == null)
            return "Plateau prefab acilamadi";

        try
        {
            MeshFilter filter = FindTargetFilter(root);

            if (filter == null)
                return "Plateau prefabinda mesh bulunamadi";

            filter.transform.localPosition = localPosition;
            filter.transform.localRotation = localRotation;
            filter.transform.localScale = localScale;

            PrefabUtility.SaveAsPrefabAsset(root, plateauPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();

        return "Tabak gorseli prefaba yazildi";
    }

    // Reads the same three values back, for a window that wants to start from
    // whatever the prefab already says rather than from an instance override
    public static bool ReadVisual(out Vector3 localPosition, out Quaternion localRotation, out Vector3 localScale)
    {
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;
        localScale = Vector3.one;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plateauPrefabPath);

        if (prefab == null)
            return false;

        MeshFilter filter = FindTargetFilter(prefab);

        if (filter == null)
            return false;

        localPosition = filter.transform.localPosition;
        localRotation = filter.transform.localRotation;
        localScale = filter.transform.localScale;

        return true;
    }

    private static void Swap(string modelPath)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

        if (model == null)
        {
            Fail("Model bulunamadi:\n" + modelPath);
            return;
        }

        MeshFilter sourceFilter = model.GetComponentInChildren<MeshFilter>(true);

        if (sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            Fail("Modelde mesh yok:\n" + modelPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(plateauPrefabPath);

        if (root == null)
        {
            Fail("Plateau prefab acilamadi:\n" + plateauPrefabPath);
            return;
        }

        try
        {
            string report = Rebuild(root, model, sourceFilter);

            if (report == null)
                return;

            PrefabUtility.SaveAsPrefabAsset(root, plateauPrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log("Plateau modeli:\n" + report);
            EditorUtility.DisplayDialog("Plateau Modeli", report, "Tamam");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string Rebuild(GameObject root, GameObject model, MeshFilter sourceFilter)
    {
        MeshFilter targetFilter = FindTargetFilter(root);

        if (targetFilter == null || targetFilter.sharedMesh == null)
        {
            Fail("Plateau prefabinda mesh bulunamadi");
            return null;
        }

        Transform visual = targetFilter.transform;

        // Already installed. Redoing the fit would throw away whatever the
        // adjuster tuned afterwards, and none of the numbers would change
        if (targetFilter.sharedMesh == sourceFilter.sharedMesh)
            return "Bu model zaten takili.\nTransform'a dokunulmadi.";

        // Where the old tray sat, measured in the space both the visual and the
        // food positions live in. Everything below lines the new model up with
        // these numbers so nothing else in the prefab has to move
        Bounds oldBounds = TransformBounds(targetFilter.sharedMesh.bounds, LocalMatrix(visual));

        // The mesh node's own placement inside the FBX, so a model that was
        // exported rotated or off centre still lands square
        Matrix4x4 insideModel = model.transform.worldToLocalMatrix * sourceFilter.transform.localToWorldMatrix;

        Bounds rawNew = TransformBounds(sourceFilter.sharedMesh.bounds, insideModel);

        float oldFootprint = Mathf.Max(oldBounds.size.x, oldBounds.size.z);
        float newFootprint = Mathf.Max(rawNew.size.x, rawNew.size.z);

        // Match the footprint rather than the volume: a plate is much flatter
        // than the old tray, and it is the width that has to read at a glance
        float fit = newFootprint > .0001f ? oldFootprint / newFootprint : 1f;

        Quaternion rotation = insideModel.rotation;
        Vector3 scale = insideModel.lossyScale * fit;

        Bounds placed = TransformBounds(sourceFilter.sharedMesh.bounds,
            Matrix4x4.TRS(Vector3.zero, rotation, scale));

        // Same centre and same underside, so the tray still meets the hand
        // exactly where it used to
        Vector3 position = new Vector3(
            oldBounds.center.x - placed.center.x,
            oldBounds.min.y - placed.min.y,
            oldBounds.center.z - placed.center.z);

        visual.localPosition = position;
        visual.localRotation = rotation;
        visual.localScale = scale;

        // A plate is thinner than the tray, so the surface food rests on drops.
        // Shift the food positions by the same amount or every item floats.
        // Re-running with the same model measures a delta of zero, so this stays
        // idempotent
        float oldSurface = oldBounds.max.y;
        float newSurface = position.y + placed.max.y;

        Transform positions = FindPositions(root, visual);

        if (positions != null && !Mathf.Approximately(oldSurface, newSurface))
            positions.localPosition += Vector3.up * (newSurface - oldSurface);

        string meshName = targetFilter.sharedMesh.name + " -> " + sourceFilter.sharedMesh.name;

        targetFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer targetRenderer = targetFilter.GetComponent<MeshRenderer>();
        MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();

        string materialName = "degismedi";

        if (targetRenderer != null && sourceRenderer != null && sourceRenderer.sharedMaterials.Length > 0)
        {
            targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            materialName = sourceRenderer.sharedMaterial == null ? "yok" : sourceRenderer.sharedMaterial.name;
        }

        return
            "Mesh: " + meshName + "\n" +
            "Material: " + materialName + "\n" +
            "Genislik: " + oldFootprint.ToString("0.000") + " -> " + (newFootprint * fit).ToString("0.000") + "\n" +
            "Olcek: " + scale.ToString("0.000") + "\n" +
            "Yemek yuksekligi: " + (newSurface - oldSurface).ToString("+0.000;-0.000;0.000");
    }

    // The visual is the one mesh the prefab ships with. Named lookup first so a
    // stray mesh added later cannot hijack the swap
    public static MeshFilter FindTargetFilter(GameObject root)
    {
        Transform named = root.transform.Find("Anim/Renderer");

        if (named != null && named.TryGetComponent(out MeshFilter filter))
            return filter;

        return root.GetComponentInChildren<MeshFilter>(true);
    }

    // Parent of the food positions, sitting next to the visual in the same space
    private static Transform FindPositions(GameObject root, Transform visual)
    {
        FoodPosition foodPosition = root.GetComponentInChildren<FoodPosition>(true);

        if (foodPosition == null)
            return null;

        Transform positions = foodPosition.transform.parent;

        return positions != null && positions.parent == visual.parent ? positions : null;
    }

    private static Matrix4x4 LocalMatrix(Transform transform)
    {
        return Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
    }

    // Rotating an axis aligned box needs every corner, not just the two the
    // Bounds struct stores
    private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        Bounds result = new Bounds(matrix.MultiplyPoint3x4(min), Vector3.zero);

        for (int i = 1; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);

            result.Encapsulate(matrix.MultiplyPoint3x4(corner));
        }

        return result;
    }

    private static void Fail(string message)
    {
        Debug.LogError("[PlateauSetup] " + message);
        EditorUtility.DisplayDialog("Hata", message, "Tamam");
    }
}
