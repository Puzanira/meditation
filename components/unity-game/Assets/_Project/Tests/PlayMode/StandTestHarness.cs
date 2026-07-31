using System.Collections;
using System.IO;
using AiGameStudio.ArcadeControls;
using Meditation.Tuning;
using Meditation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meditation.Tests
{
    /// <summary>Pumps ArcadeInput from a FakeBackend so PlayMode tests drive both hands from code.</summary>
    public sealed class ArcadePump : MonoBehaviour
    {
        public FakeBackend Backend;

        private void Update()
        {
            if (Backend != null) ArcadeInput.Update(Time.deltaTime);
        }
    }

    /// <summary>Shared PlayMode plumbing: load a stand scene, take over its input, inspect its layout.</summary>
    public static class StandTestHarness
    {
        /// <summary>
        /// Send the tuning store to a scratch file for the duration of a test. Dragging a slider now
        /// writes the config to disk, and a suite run must never overwrite the numbers the founder
        /// tuned in her own <c>UserSettings/tuning.json</c>.
        /// </summary>
        public static void IsolateTuningFile()
        {
            TuningStore.UseFileForTests(Path.Combine(Application.temporaryCachePath,
                "tuning-playmode-tests.json"));
        }

        /// <summary>Give the store back the real file and forget the scratch one.</summary>
        public static void ReleaseTuningFile()
        {
            try
            {
                if (TuningStore.IsRedirected && File.Exists(TuningStore.FilePath))
                    File.Delete(TuningStore.FilePath);
            }
            catch (System.Exception)
            {
                // A leftover file in the temp cache is harmless.
            }

            TuningStore.UseDefaultFile();
        }

        /// <summary>Load a stand scene for real (single mode, exactly how the stand boots).</summary>
        public static IEnumerator LoadScene(string sceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " must be in Build Settings.");
            while (!op.isDone) yield return null;

            yield return null;   // Awake
            yield return null;   // first Update: everything is built and drawn
        }

        /// <summary>
        /// Replace the scene's keyboard backend with a code-driven one. The runner is destroyed so
        /// nothing else pumps ArcadeInput behind the test's back.
        /// </summary>
        public static FakeBackend TakeOverInput()
        {
            var runner = Object.FindAnyObjectByType<ArcadeInputRunner>();
            if (runner != null) Object.DestroyImmediate(runner);

            var fake = new FakeBackend();
            ArcadeInput.Initialize(fake);

            var pump = Object.FindAnyObjectByType<ArcadePump>();
            if (pump == null) pump = new GameObject("ArcadePump").AddComponent<ArcadePump>();
            pump.Backend = fake;
            return fake;
        }

        public static DesignStage Stage()
        {
            var stage = Object.FindAnyObjectByType<DesignStage>();
            Assert.IsNotNull(stage, "The scene must build a DesignStage canvas.");
            return stage;
        }

        /// <summary>Find a widget of the built UI by GameObject name.</summary>
        public static RectTransform Find(DesignStage stage, string name)
        {
            RectTransform[] all = stage.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];

            Assert.Fail("Widget '" + name + "' is missing from the stage.");
            return null;
        }

        /// <summary>
        /// The check the studio learned to insist on: not "is the object enabled" but "is this thing
        /// actually drawn, at a real size, inside the screen".
        /// </summary>
        public static void AssertVisible(RectTransform rt, string what)
        {
            Assert.IsTrue(rt.gameObject.activeInHierarchy, what + " is not active in the hierarchy.");

            var graphic = rt.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
            {
                Assert.IsTrue(graphic.enabled, what + " has a disabled graphic.");
                Assert.Greater(graphic.color.a, 0.05f, what + " is fully transparent.");
            }

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);

            Assert.Greater(maxX - minX, 1f, what + " has no width on screen.");
            Assert.Greater(maxY - minY, 1f, what + " has no height on screen.");
            Assert.Greater(Mathf.Min(maxX, Screen.width) - Mathf.Max(minX, 0f), 1f,
                what + " sits outside the screen horizontally.");
            Assert.Greater(Mathf.Min(maxY, Screen.height) - Mathf.Max(minY, 0f), 1f,
                what + " sits outside the screen vertically.");
        }

        /// <summary>Assert a widget occupies exactly the rectangle SCREENS.md gives it.</summary>
        public static void AssertDesignRect(DesignStage stage, string name, Rect expected, float tolerance = 1.5f)
        {
            RectTransform rt = Find(stage, name);
            AssertVisible(rt, name);

            Rect actual = stage.DesignRectOf(rt);
            Assert.AreEqual(expected.x, actual.x, tolerance, name + ": x off spec.");
            Assert.AreEqual(expected.y, actual.y, tolerance, name + ": y off spec.");
            Assert.AreEqual(expected.width, actual.width, tolerance, name + ": width off spec.");
            Assert.AreEqual(expected.height, actual.height, tolerance, name + ": height off spec.");
        }

        public static Vector2 DesignCentre(DesignStage stage, string name)
        {
            Rect r = stage.DesignRectOf(Find(stage, name));
            return new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
        }

        // ---- viewport size -----------------------------------------------------------------------

        /// <summary>
        /// Put the stand's canvas into a window of exactly this size. The founder does not play at
        /// 1920×1080 — her Game view measured about 1346×636 — and a layout bug only exists at the size
        /// it exists at, so a layout test has to be able to name its resolution. Same trick the
        /// screenshot tests use: an off-screen camera whose render target IS the window.
        /// </summary>
        public static Camera ResizeCanvas(DesignStage stage, int width, int height)
        {
            var camGo = new GameObject("ViewportCamera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -100f);
            // 24-bit depth, like the screenshot camera: the render-graph API warns on a depthless
            // output texture, and a stray warning fails LogAssert.NoUnexpectedReceived.
            cam.targetTexture = new RenderTexture(width, height, 24);

            stage.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            stage.Canvas.worldCamera = cam;
            stage.Canvas.planeDistance = 50f;
            Canvas.ForceUpdateCanvases();
            stage.Fit();
            Canvas.ForceUpdateCanvases();
            return cam;
        }

        public static void RestoreCanvas(DesignStage stage, Camera cam)
        {
            if (stage != null && stage.Canvas != null)
            {
                stage.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                stage.Fit();
            }

            if (cam == null) return;
            RenderTexture target = cam.targetTexture;
            cam.targetTexture = null;
            if (target != null) target.Release();
            Object.DestroyImmediate(cam.gameObject);
        }

        /// <summary>Where a widget really is, in canvas pixels — read off the rendered corners.</summary>
        public static Rect WorldRectOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Drawn rectangles come back in WORLD units, and a canvas rendered through a camera does not
        /// put one world unit on a pixel — so «this block is 200 px tall» cannot be asserted against a
        /// raw world height. The panel is the ruler: it is exactly <see cref="TuningPanel.PanelWidth"/>
        /// canvas pixels wide by construction, so its drawn width gives the scale.
        /// </summary>
        public static float ToPanelPixels(TuningPanel panel, float worldSize)
        {
            float drawnWidth = WorldRectOf((RectTransform)panel.transform).width;
            Assert.Greater(drawnWidth, 0f, "The tuning panel has no width on screen.");
            return worldSize * TuningPanel.PanelWidth / drawnWidth;
        }

        /// <summary>The part of <paramref name="rect"/> that survives clipping; width/height 0 if none.</summary>
        public static Rect ClippedBy(Rect rect, Rect clip)
        {
            float minX = Mathf.Max(rect.xMin, clip.xMin);
            float maxX = Mathf.Min(rect.xMax, clip.xMax);
            float minY = Mathf.Max(rect.yMin, clip.yMin);
            float maxY = Mathf.Min(rect.yMax, clip.yMax);
            if (maxX <= minX || maxY <= minY) return new Rect(minX, minY, 0f, 0f);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>Do two drawn rectangles share pixels? A hair of tolerance, so touching edges pass.</summary>
        public static bool Overlaps(Rect a, Rect b, float tolerance = 0.5f)
        {
            return a.xMin < b.xMax - tolerance && b.xMin < a.xMax - tolerance &&
                   a.yMin < b.yMax - tolerance && b.yMin < a.yMax - tolerance;
        }
    }
}
