#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Reflection;

namespace Tabsil.UI
{
    public static class UIElements
    {
        private const string uiPath = "GameObject/Tabsil/UI/";

        [MenuItem(uiPath + "Portrait Canvas", false, 0)]
        public static GameObject CreatePortraitCanvas(MenuCommand menuCommand)
        {
            // Create the Canvas GameObject
            GameObject canvasGO = new GameObject("Portrait Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Add required components
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Configure the CanvasScaler
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // Match height

            // Ensure Canvas is placed correctly in hierarchy
            GameObjectUtility.SetParentAndAlign(canvasGO, menuCommand.context as GameObject);

            // Create an EventSystem if it doesn't exist
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            // Register undo and select the new Canvas
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Portrait Canvas");
            Selection.activeObject = canvasGO;

            StartRenaming(canvasGO);

            return canvasGO;
        }

        [MenuItem(uiPath + "Safe Zone", false, 0)]
        private static void CreateSafeZone(MenuCommand menuCommand)
        {
            // Try to find parent RectTransform (where user right-clicked)
            GameObject parentObject = menuCommand.context as GameObject;

            // If none found create a new configured canvas
            if (parentObject == null)
            {
                GameObject canvasGO = CreatePortraitCanvas(menuCommand);
                GameObject blankPanel = CreateBlankPanel();
                blankPanel.transform.SetParent(canvasGO.transform);

                RectTransform rt = blankPanel.GetComponent<RectTransform>();

                // Full stretch by default
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                parentObject = blankPanel;// canvasGO.GetComponent<RectTransform>().gameObject;
            }

            GameObject safeZoneGO = new GameObject("SafeZone", typeof(RectTransform));
            RectTransform safeRect = safeZoneGO.GetComponent<RectTransform>();
            safeRect.SetParent(parentObject.transform, false);

            // Full stretch by default
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;

            safeZoneGO.AddComponent<UISafeZone>();

            Undo.RegisterCreatedObjectUndo(safeZoneGO, "Create Safe Zone");
            Selection.activeObject = safeZoneGO;

            StartRenaming(safeZoneGO);
        }

        private static GameObject CreateBlankPanel()
        {
            GameObject panel = new GameObject("[PANEL]");

            RectTransform rt = panel.AddComponent<RectTransform>();            


            return panel;
        }

        private static void StartRenaming(GameObject go)
        {
            // Must happen one frame after creating the GO
            EditorApplication.delayCall += () =>
            {
                if (Selection.activeObject == go)
                    EditorWindow.focusedWindow?.SendEvent(EditorGUIUtility.CommandEvent("Rename"));
            };
        }
    }
}


#endif