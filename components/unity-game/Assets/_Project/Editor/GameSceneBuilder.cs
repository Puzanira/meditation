using System;
using System.Collections.Generic;
using System.IO;
using AiGameStudio.ArcadeControls;
using Meditation.Game;
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
    /// Builds the game scene and the preview-stand scenes from code, and registers all six in Build
    /// Settings with <c>Game.unity</c> first — the entry scene the cabinet's launcher opens
    /// (ARCADE_INTEGRATION_CONTRACT §5/§6).
    ///
    /// Everything the player sees is built procedurally at runtime, so the scenes themselves stay
    /// four objects each: camera, event system, the controller, the exit button. That keeps the whole
    /// composition under review in readable code instead of a scene YAML, and makes the build
    /// reproducible from one menu item.
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string UiActionsAssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [MenuItem("Meditation/Rebuild All Scenes")]
        public static void Rebuild()
        {
            EnsureFolder();

            var paths = new List<string>
            {
                // Entry scene first: the launcher loads scene 0.
                BuildScene(PreviewScenes.Game, typeof(GameFlow), PreviewScenes.Game),

                BuildScene(PreviewScenes.Menu, typeof(PreviewMenuController), null),
                BuildScene(PreviewScenes.CrankCollect, typeof(Scene1CrankCollect), null),
                BuildScene(PreviewScenes.ShakeAway, typeof(Scene2ShakeAway), null),
                BuildScene(PreviewScenes.TwoHands, typeof(Scene3TwoHands), null),
                BuildScene(PreviewScenes.FullLevel, typeof(Scene4FullLevel), null)
            };

            var buildScenes = new EditorBuildSettingsScene[paths.Count];
            for (int i = 0; i < paths.Count; i++)
                buildScenes[i] = new EditorBuildSettingsScene(paths[i], true);
            EditorBuildSettings.scenes = buildScenes;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Single);
            Debug.Log("[Meditation] Сцены пересобраны: " + string.Join(", ", paths));
        }

        private static string BuildScene(string sceneName, Type controllerType, string exitScene)
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

            // The UI is a screen-space overlay canvas, but the tuning panel's sliders still need an
            // event system. This project runs the new Input System only.
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            var module = eventSystemGo.AddComponent<InputSystemUIInputModule>();

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UiActionsAssetPath);
            if (actions != null) module.actionsAsset = actions;
            else Debug.LogWarning("[Meditation] UI actions asset not found at " + UiActionsAssetPath +
                                  " — the tuning panel will not react to the mouse.");

            var hostGo = new GameObject(controllerType == typeof(GameFlow) ? "Game" : "Stand");
            hostGo.AddComponent<ArcadeInputRunner>();
            hostGo.AddComponent(controllerType);
            var exit = hostGo.AddComponent<MenuButtonExit>();
            if (!string.IsNullOrEmpty(exitScene)) exit.ExitScene = exitScene;

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
