using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// The 1920×1080 design frame the whole game screen is laid out in, letterboxed into whatever the
    /// window actually is. Keeping a real fixed-size frame (instead of stretching a CanvasScaler)
    /// means SCREENS.md coordinates are exact at any window size — and that the tuning panel can take
    /// a slice of the window without ever squashing the composition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesignStage : MonoBehaviour
    {
        public const float DesignWidth = 1920f;
        public const float DesignHeight = 1080f;

        /// <summary>Screen pixels reserved on the right for the tuning panel.</summary>
        public float ReservedRight;

        public Canvas Canvas { get; private set; }

        /// <summary>The letterbox field around the design frame, in stand palette.</summary>
        public Image Backdrop { get; private set; }

        /// <summary>Parent for all game content; its local space IS design space (Y flipped).</summary>
        public RectTransform Frame { get; private set; }

        /// <summary>Canvas-space root for stand chrome (tuning panel) that must not be letterboxed.</summary>
        public RectTransform Overlay { get; private set; }

        public float Scale { get; private set; } = 1f;

        public static DesignStage Create(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(DesignStage));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Constant pixel size: canvas units are screen pixels, and the design frame does the
            // scaling itself. A ScaleWithScreenSize canvas would fight the letterboxing.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var stage = go.GetComponent<DesignStage>();
            stage.Canvas = canvas;

            // The letterbox around the frame is painted in the stand's own palette, not left black:
            // the founder tunes in this view, and a dark frame would poison every colour judgement.
            var backdropGo = new GameObject("StandBackdrop", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            backdropGo.transform.SetParent(go.transform, false);
            var backdrop = backdropGo.GetComponent<Image>();
            backdrop.color = new Color(0.76f, 0.74f, 0.69f);
            backdrop.raycastTarget = false;
            var backdropRt = backdrop.rectTransform;
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            backdropRt.offsetMin = Vector2.zero;
            backdropRt.offsetMax = Vector2.zero;
            stage.Backdrop = backdrop;

            var frameGo = new GameObject("DesignFrame", typeof(RectTransform));
            frameGo.transform.SetParent(go.transform, false);
            var frame = (RectTransform)frameGo.transform;
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0f, 1f);
            frame.sizeDelta = new Vector2(DesignWidth, DesignHeight);
            stage.Frame = frame;

            var overlayGo = new GameObject("Overlay", typeof(RectTransform));
            overlayGo.transform.SetParent(go.transform, false);
            var overlay = (RectTransform)overlayGo.transform;
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            stage.Overlay = overlay;

            stage.Fit();
            return stage;
        }

        private void LateUpdate() => Fit();

        /// <summary>Letterbox the design frame into the window minus the reserved panel strip.</summary>
        public void Fit()
        {
            if (Frame == null) return;

            // Measured off the canvas, not off Screen: the same code then fits an overlay canvas, a
            // camera canvas and an off-screen capture target without a special case for each.
            Vector2 surface = CanvasSize();
            float available = Mathf.Max(100f, surface.x - ReservedRight);
            Scale = Mathf.Min(available / DesignWidth, surface.y / DesignHeight);

            // Frame pivot is its top-left corner; anchor is the canvas centre.
            float centreX = -ReservedRight * 0.5f;
            Frame.localScale = new Vector3(Scale, Scale, 1f);
            Frame.anchoredPosition = new Vector2(
                centreX - DesignWidth * 0.5f * Scale,
                DesignHeight * 0.5f * Scale);
        }

        private Vector2 CanvasSize()
        {
            var rt = transform as RectTransform;
            Vector2 size = rt != null ? rt.rect.size : Vector2.zero;
            if (size.x > 1f && size.y > 1f) return size;
            return new Vector2(Mathf.Max(1f, Screen.width), Mathf.Max(1f, Screen.height));
        }

        /// <summary>
        /// Where a widget really sits, in design pixels (origin top-left). Reads the rendered world
        /// corners, so it catches "the object is somewhere, but not where the spec says" — the check
        /// an activeSelf assert would sail straight past.
        /// </summary>
        public Rect DesignRectOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = Frame.InverseTransformPoint(corners[i]);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, -local.y);
                maxY = Mathf.Max(maxY, -local.y);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
