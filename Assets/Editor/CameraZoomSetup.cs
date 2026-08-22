using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CameraZoomSetup
{
    private const float slightlyFarther = 11f;
    private const string recoveryScene = "Assets/_Recovery/0 (1).unity";

    [MenuItem("Cooked Fast/Kamera/Onceki Goruntuyu Recoveryden Geri Getir", priority = 229)]
    public static void RestoreFromRecovery()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Kamera",
                "Play modunu durdur, sonra tekrar dene.", "Tamam");
            return;
        }

        if (!File.Exists(recoveryScene))
        {
            EditorUtility.DisplayDialog("Kamera",
                "Onceki Recovery sahnesi bulunamadi:\n" + recoveryScene,
                "Tamam");
            return;
        }

        Scene destination = SceneManager.GetActiveScene();
        Scene recovered = default;
        float recoveredSize = -1f;

        try
        {
            recovered = EditorSceneManager.OpenScene(recoveryScene,
                OpenSceneMode.Additive);
            CinemachineCamera[] oldCameras = CamerasIn(recovered);

            for (int i = 0; i < oldCameras.Length; i++)
            {
                if (oldCameras[i].name == "Player Follow Camera")
                {
                    recoveredSize = oldCameras[i].Lens.OrthographicSize;
                    break;
                }
            }
        }
        finally
        {
            if (recovered.IsValid() && recovered.isLoaded)
                EditorSceneManager.CloseScene(recovered, true);

            if (destination.IsValid() && destination.isLoaded)
                SceneManager.SetActiveScene(destination);
        }

        if (recoveredSize <= 0f)
        {
            EditorUtility.DisplayDialog("Kamera",
                "Recovery sahnesinde 'Player Follow Camera' bulunamadi.",
                "Tamam");
            return;
        }

        int changed = Apply(destination, recoveredSize, out float previous);

        if (changed == 0)
        {
            EditorUtility.DisplayDialog("Kamera",
                "Acik sahnede 'Player Follow Camera' bulunamadi.", "Tamam");
            return;
        }

        EditorUtility.DisplayDialog("Kamera",
            "Gercek onceki kamera kadraji geri getirildi.\n\n" +
            "Orthographic Size " + previous.ToString("0.##") + " -> " +
            recoveredSize.ToString("0.##") +
            "\n\nCtrl+S ile sahneyi kaydet.", "Tamam");
    }

    [MenuItem("Cooked Fast/Kamera/Orijinal Uzaklik (11)", priority = 230)]
    public static void SlightlyFarther()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Kamera",
                "Play modunu durdur, sonra tekrar dene.", "Tamam");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        int changed = Apply(activeScene, slightlyFarther, out float previous);

        if (changed == 0)
        {
            EditorUtility.DisplayDialog("Kamera",
                "Acik sahnede 'Player Follow Camera' bulunamadi.", "Tamam");
            return;
        }

        EditorUtility.DisplayDialog("Kamera",
            "Orthographic Size " + previous.ToString("0.##") +
            " -> " + slightlyFarther.ToString("0.##") +
            " yapildi.\n\nCtrl+S ile sahneyi kaydet.", "Tamam");
    }

    private static int Apply(Scene scene, float size, out float previous)
    {
        previous = 0f;
        int changed = 0;
        CinemachineCamera[] cameras = CamerasIn(scene);

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];

            if (camera.name != "Player Follow Camera")
                continue;

            Undo.RecordObject(camera, "Restore camera framing");
            LensSettings lens = camera.Lens;
            previous = lens.OrthographicSize;
            lens.OrthographicSize = size;
            camera.Lens = lens;
            EditorUtility.SetDirty(camera);
            Selection.activeGameObject = camera.gameObject;
            changed++;
        }

        // Cinemachine owns this camera in Play. Matching the output camera as
        // well makes the restored framing visible immediately in Edit Mode.
        Camera[] outputs = ComponentsIn<Camera>(scene);

        for (int i = 0; i < outputs.Length; i++)
        {
            Camera output = outputs[i];

            if (!output.CompareTag("MainCamera"))
                continue;

            Undo.RecordObject(output, "Restore camera framing");
            output.orthographic = true;
            output.orthographicSize = size;
            EditorUtility.SetDirty(output);
        }

        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return changed;
    }

    private static CinemachineCamera[] CamerasIn(Scene scene)
    {
        return ComponentsIn<CinemachineCamera>(scene);
    }

    private static T[] ComponentsIn<T>(Scene scene) where T : Component
    {
        List<T> found = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
            found.AddRange(roots[i].GetComponentsInChildren<T>(true));

        return found.ToArray();
    }
}
