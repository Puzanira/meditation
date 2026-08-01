using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// UGUI primitives that speak SCREENS.md's coordinate system directly: design pixels on a
    /// 1920×1080 screen, origin top-left, Y growing downwards, positions given as element centres.
    /// Every widget in the stand is built through here, so a number in the spec is the same number
    /// in the code (and in the tests, via <see cref="DesignStage.DesignRectOf"/>).
    /// </summary>
    public static class Ui
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>An empty layer that fills the whole design frame. Later siblings draw on top.</summary>
        public static RectTransform Layer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Sharp-cornered rectangle given by its top-left corner (the spec's building/ground form).</summary>
        public static Image Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            return BoxCentred(parent, name, x + w * 0.5f, y + h * 0.5f, w, h, color);
        }

        /// <summary>Sharp-cornered rectangle given by its centre.</summary>
        public static Image BoxCentred(Transform parent, string name, float cx, float cy, float w, float h, Color color)
        {
            var image = NewImage(parent, name);
            image.color = color;
            Place(image.rectTransform, cx, cy, w, h);
            return image;
        }

        /// <summary>
        /// A rounded window: everything parented under it is clipped to a rounded rectangle.
        ///
        /// Level 3's vessel has no sprite of its own — it is cut out of the background plate — and a
        /// straight rectangular cut showed its hard edges and a corner of the passenger's coat the
        /// moment the bag left its place (the finale's row, the victory tableau). The mask is what
        /// turns the cut-out back into an object.
        /// </summary>
        public static RectTransform RoundedMask(Transform parent, string name, float cx, float cy,
            float w, float h, int cornerRadius = 20)
        {
            var image = NewImage(parent, name);
            image.sprite = UiSprites.RoundedOf(cornerRadius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;
            Place(image.rectTransform, cx, cy, w, h);

            var mask = image.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            return image.rectTransform;
        }

        /// <summary>Rounded rectangle with an optional stroke; returns the outer (stroke) image.</summary>
        public static Image Rounded(Transform parent, string name, float cx, float cy, float w, float h,
            Color fill, Color stroke, float strokeWidth = 0f, int cornerRadius = 14)
        {
            var outer = NewImage(parent, name);
            outer.sprite = UiSprites.RoundedOf(cornerRadius);
            outer.type = Image.Type.Sliced;
            outer.pixelsPerUnitMultiplier = 1f;
            outer.color = strokeWidth > 0f ? stroke : fill;
            Place(outer.rectTransform, cx, cy, w, h);

            if (strokeWidth <= 0f) return outer;

            var inner = NewImage(outer.transform, name + "_Fill");
            inner.sprite = UiSprites.RoundedOf(cornerRadius);
            inner.type = Image.Type.Sliced;
            inner.pixelsPerUnitMultiplier = 1f;
            inner.color = fill;
            inner.raycastTarget = false;
            var rt = inner.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(strokeWidth, strokeWidth);
            rt.offsetMax = new Vector2(-strokeWidth, -strokeWidth);
            return outer;
        }

        /// <summary>Circle with an optional stroke; returns the outer (stroke) image.</summary>
        public static Image Circle(Transform parent, string name, float cx, float cy, float radius,
            Color fill, Color stroke, float strokeWidth = 0f)
        {
            var outer = NewImage(parent, name);
            outer.sprite = UiSprites.Circle;
            outer.color = strokeWidth > 0f ? stroke : fill;
            Place(outer.rectTransform, cx, cy, radius * 2f, radius * 2f);

            if (strokeWidth <= 0f) return outer;

            var inner = NewImage(outer.transform, name + "_Fill");
            inner.sprite = UiSprites.Circle;
            inner.color = fill;
            inner.raycastTarget = false;
            var rt = inner.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(strokeWidth, strokeWidth);
            rt.offsetMax = new Vector2(-strokeWidth, -strokeWidth);
            return outer;
        }

        /// <summary>Ring image set up for radial fill (progress rings, the sun-dial timer).</summary>
        public static Image Ring(Transform parent, string name, float cx, float cy, float radius, Color color)
        {
            var image = NewImage(parent, name);
            image.sprite = UiSprites.Ring;
            image.color = color;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 1f;
            Place(image.rectTransform, cx, cy, radius * 2f, radius * 2f);
            return image;
        }

        /// <summary>Dashed connector between two design-space points (the collection thread).</summary>
        public static Image Dashed(Transform parent, string name, Color color, float thickness = 6f)
        {
            var image = NewImage(parent, name);
            image.sprite = UiSprites.Dash;
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(0f, thickness);
            return image;
        }

        /// <summary>Solid connector segment, positioned by <see cref="StretchLine"/>.</summary>
        public static Image Segment(Transform parent, string name, Color color, float thickness)
        {
            var image = NewImage(parent, name);
            image.color = color;
            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(0f, thickness);
            return image;
        }

        /// <summary>Point a <see cref="Dashed"/> line from one design-space point to another.</summary>
        public static void StretchLine(RectTransform line, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            line.anchoredPosition = new Vector2(from.x, -from.y);
            line.sizeDelta = new Vector2(delta.magnitude, line.sizeDelta.y);
            float angle = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg;
            line.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public static Text Label(Transform parent, string name, string text, float cx, float cy,
            float w, float h, int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = Font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            Place(label.rectTransform, cx, cy, w, h);
            return label;
        }

        /// <summary>Position a rect by its centre in design px (origin top-left, Y down).</summary>
        public static void Place(RectTransform rt, float cx, float cy, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(cx, -cy);
        }

        /// <summary>Move an already-placed rect to a new design-space centre.</summary>
        public static void MoveTo(RectTransform rt, Vector2 designCentre)
        {
            rt.anchoredPosition = new Vector2(designCentre.x, -designCentre.y);
        }

        public static Image NewImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
