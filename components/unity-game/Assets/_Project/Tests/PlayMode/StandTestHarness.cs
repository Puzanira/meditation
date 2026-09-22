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
        /// The same search, without the assertion — for the cases where ABSENCE is the claim. «Солнца
        /// на экране больше нет» is one of them (founder, 2026-08-07), and it cannot be written with
        /// <see cref="Find"/>, which fails precisely when the thing is gone.
        /// </summary>
        public static RectTransform FindOrNull(DesignStage stage, string name)
        {
            RectTransform[] all = stage.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
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

        // ---- screenshots -----------------------------------------------------------------------------

        /// <summary>Where the design gate looks for its evidence (gitignored).</summary>
        public static string ScreenshotFolder => Path.Combine(Application.dataPath, "..", "Screenshots");

        /// <summary>
        /// Render the live canvas at full design resolution and write it to disk.
        ///
        /// The canvas is re-pointed at an off-screen camera for the shot, because the editor's
        /// screenshot API produces nothing in batch mode and the suite has to be runnable headless.
        /// Shared by the stand's frames and the game's, so both sets are made the same way and can be
        /// judged side by side.
        /// </summary>
        public static void Shoot(string shotName, Color letterbox)
        {
            Directory.CreateDirectory(ScreenshotFolder);

            Texture2D shot = Capture(letterbox);
            string path = Path.Combine(ScreenshotFolder, shotName + ".png");
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Object.DestroyImmediate(shot);

            Assert.IsTrue(File.Exists(path), "No frame was written for " + shotName);
            Assert.Greater(new FileInfo(path).Length, 5000, shotName + " rendered an empty frame.");
        }

        /// <summary>
        /// The same render, cut down to one design-space rectangle and magnified — a close-up.
        ///
        /// The frame is 1920×1080 and some of the things the design gate has to judge are forty pixels
        /// wide: the neon rim on the office paperclip is a two-pixel line, and «покажи, что ободок не
        /// вырождается в заливку» cannot be answered by pointing at a screenshot of a whole level. The
        /// magnification is NEAREST-neighbour on purpose — a smooth upscale would invent the very
        /// smoothness the rim is being judged for.
        /// </summary>
        /// <param name="design">What to cut out, in design px (origin top-left).</param>
        /// <param name="zoom">Whole-number magnification, so one rendered pixel stays one square.</param>
        public static void ShootCloseUp(string shotName, Color letterbox, Rect design, int zoom = 6)
        {
            Directory.CreateDirectory(ScreenshotFolder);

            Texture2D shot = Capture(letterbox);
            try
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(design.xMin), 0, 1919);
                int y = Mathf.Clamp(Mathf.RoundToInt(design.yMin), 0, 1079);
                int w = Mathf.Clamp(Mathf.RoundToInt(design.width), 1, 1920 - x);
                int h = Mathf.Clamp(Mathf.RoundToInt(design.height), 1, 1080 - y);

                Color[] cut = PixelsOf(shot, new RectInt(x, y, w, h));
                var big = new Texture2D(w * zoom, h * zoom, TextureFormat.RGB24, false);
                try
                {
                    var blown = new Color[w * zoom * h * zoom];
                    for (int row = 0; row < h * zoom; row++)
                    for (int column = 0; column < w * zoom; column++)
                        blown[row * w * zoom + column] = cut[(row / zoom) * w + column / zoom];

                    big.SetPixels(blown);
                    big.Apply();

                    string path = Path.Combine(ScreenshotFolder, shotName + ".png");
                    File.WriteAllBytes(path, big.EncodeToPNG());
                    Assert.IsTrue(File.Exists(path), "No frame was written for " + shotName);
                    Assert.Greater(new FileInfo(path).Length, 2000, shotName + " rendered an empty frame.");
                }
                finally
                {
                    Object.DestroyImmediate(big);
                }
            }
            finally
            {
                Object.DestroyImmediate(shot);
            }
        }

        /// <summary>
        /// The same render, handed back as pixels instead of written to disk.
        ///
        /// Some claims are about the PICTURE and cannot be checked any other way: «этот силуэт читается
        /// с метра» is a statement about ink and contrast in the rendered slot, and «десатурация гасит
        /// цвет» is a statement about the saturation of the rendered blob. Measuring those off the same
        /// path that produces the design gate's frames means the test and the evidence agree by
        /// construction. The caller owns the texture.
        /// </summary>
        public static Texture2D Capture(Color letterbox)
        {
            DesignStage stage = Stage();

            var camGo = new GameObject("ShotCamera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = letterbox;
            cam.transform.position = new Vector3(0f, 0f, -100f);

            var target = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = target;

            RenderMode previousMode = stage.Canvas.renderMode;
            stage.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            stage.Canvas.worldCamera = cam;
            stage.Canvas.planeDistance = 50f;
            Canvas.ForceUpdateCanvases();
            stage.Fit();
            Canvas.ForceUpdateCanvases();

            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var shot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            cam.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(camGo);

            stage.Canvas.renderMode = previousMode;
            stage.Fit();

            return shot;
        }

        /// <summary>
        /// Where a widget's rectangle lands in the captured frame: pixels, origin top-left, like every
        /// design coordinate in this project. <see cref="Capture"/> hands back a texture whose rows run
        /// bottom-up, so this is also the place that flip is stated once.
        /// </summary>
        public static RectInt PixelRectOf(DesignStage stage, RectTransform rt)
        {
            Rect design = stage.DesignRectOf(rt);
            int x = Mathf.Clamp(Mathf.RoundToInt(design.xMin), 0, 1920);
            int y = Mathf.Clamp(Mathf.RoundToInt(design.yMin), 0, 1080);
            int w = Mathf.Clamp(Mathf.RoundToInt(design.width), 0, 1920 - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(design.height), 0, 1080 - y);
            return new RectInt(x, y, w, h);
        }

        /// <summary>Pixels of <paramref name="box"/> out of a captured frame, row order irrelevant.</summary>
        public static Color[] PixelsOf(Texture2D frame, RectInt box)
        {
            if (box.width <= 0 || box.height <= 0) return new Color[0];
            // Design Y counts down from the top; a texture's rows count up from the bottom.
            int bottom = frame.height - (box.y + box.height);
            return frame.GetPixels(box.x, bottom, box.width, box.height);
        }

        /// <summary>
        /// Share of <paramref name="box"/> (the whole frame when it is omitted) whose pixels change
        /// when <paramref name="what"/> is switched off — i.e. what that object is actually PAINTING on
        /// the composited picture.
        ///
        /// The measurement behind every «is it really visible» / «is it really covered» claim in the
        /// suite, and it lives here because both sides of it are asked now: the screenshot gate asks
        /// whether a widget paints its own rectangle, and the picture tests ask whether the thought
        /// layer paints inside somebody else's (SCREENS §S3, «HUD они не закрывают»).
        /// </summary>
        /// <param name="noise">How far a channel has to move, 0…255, to count as painted over.</param>
        public static float ShareCoveredBy(GameObject what, Color letterbox, RectInt? box = null,
            float noise = 6f)
        {
            Texture2D with = Capture(letterbox);
            bool was = what.activeSelf;
            what.SetActive(false);
            Canvas.ForceUpdateCanvases();
            Texture2D without = Capture(letterbox);
            what.SetActive(was);
            Canvas.ForceUpdateCanvases();

            try
            {
                RectInt region = box ?? new RectInt(0, 0, with.width, with.height);
                Color[] a = PixelsOf(with, region);
                Color[] b = PixelsOf(without, region);
                if (a.Length == 0 || a.Length != b.Length) return 0f;

                int moved = 0;
                for (int i = 0; i < a.Length; i++)
                    if (Mathf.Abs(a[i].r - b[i].r) * 255f > noise ||
                        Mathf.Abs(a[i].g - b[i].g) * 255f > noise ||
                        Mathf.Abs(a[i].b - b[i].b) * 255f > noise) moved++;

                return moved / (float)a.Length;
            }
            finally
            {
                Object.DestroyImmediate(with);
                Object.DestroyImmediate(without);
            }
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
