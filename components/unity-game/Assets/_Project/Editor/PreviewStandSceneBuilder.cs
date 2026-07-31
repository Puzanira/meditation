using System;
using System.Collections.Generic;
using System.IO;
using AiGameStudio.ArcadeControls;
using Meditation.Scenes;
using Meditation.Stand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Meditation.EditorTools
{
    /// <summary>
    /// Builds the five stand scenes from code and registers them in Build Settings.
    ///
    /// The scenes are deliberately tiny — camera, event system, one stand object — because every pixel
    /// of the stand is built procedurally at runtime (see <c>StageView</c>). That keeps the composition
    /// under review in one readable place instead of a scene YAML, and makes the whole stand
    /// reproducible: delete the scenes, run this menu item, and you are back where you were.
    /// </summary>
    public static class PreviewStandSceneBuilder
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string UiActionsAssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [MenuItem("Meditation/Rebuild Preview Stand Scenes")]
        public static void Rebuild()
        {
            EnsureFolder();

            var paths = new List<string>
            {
                BuildScene(PreviewScenes.Menu, typeof(PreviewMenuController)),
                BuildScene(PreviewScenes.CrankCollect, typeof(Scene1CrankCollect)),
                BuildScene(PreviewScenes.ShakeAway, typeof(Scene2ShakeAway)),
                BuildScene(PreviewScenes.TwoHands, typeof(Scene3TwoHands)),
                BuildScene(PreviewScenes.FullLevel, typeof(Scene4FullLevel))
            };

            var buildScenes = new EditorBuildSettingsScene[paths.Count];
            for (int i = 0; i < paths.Count; i++)
                buildScenes[i] = new EditorBuildSettingsScene(paths[i], true);
            EditorBuildSettings.scenes = buildScenes;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Leave the editor on the entry scene, ready to hit Play.
            EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Single);
            Debug.Log("[Meditation] Preview stand scenes rebuilt: " + string.Join(", ", paths));
        }

        private static string BuildScene(string sceneName, Type controllerType)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.09f, 0.08f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            // The UI is a screen-space overlay canvas, but the panel's sliders still need an event
            // system. This project runs the new Input System only, hence InputSystemUIInputModule.
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            var module = eventSystemGo.AddComponent<InputSystemUIInputModule>();

            // Point the module at the project-wide actions asset; its UI map already carries the
            // Point/Click/Scroll actions the panel's sliders need.
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UiActionsAssetPath);
            if (actions != null) module.actionsAsset = actions;
            else Debug.LogWarning("[Meditation] UI actions asset not found at " + UiActionsAssetPath +
                                  " — the tuning panel will not react to the mouse.");

            var standGo = new GameObject("Stand");
            standGo.AddComponent<ArcadeInputRunner>();
            standGo.AddComponent(controllerType);
            standGo.AddComponent<MenuButtonExit>();

            string path = ScenesFolder + "/" + sceneName + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void EnsureFolder()
        {
            if (!Directory.Exists("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!Directory.Exists(ScenesFolder)) AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
        }
    }
}
