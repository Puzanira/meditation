using UnityEngine;

namespace Meditation.View
{
    /// <summary>
    /// Procedural placeholder sprites. The whole stand is greybox art (WORLD.md decision), so shapes
    /// are generated in code instead of importing assets: a 9-sliced rounded rectangle, a circle and a
    /// ring for progress arcs, plus a dash pattern for the collection thread.
    /// </summary>
    public static class UiSprites
    {
        private static readonly System.Collections.Generic.Dictionary<int, Sprite> RoundedByRadius =
            new System.Collections.Generic.Dictionary<int, Sprite>();

        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _dash;
        private static Sprite _dashedRing;

        /// <summary>Rounded rectangle with the default corner radius.</summary>
        public static Sprite Rounded => RoundedOf(14);

        /// <summary>
        /// Rounded rectangle, 9-sliced so the corner radius stays constant at any size — and cached
        /// per radius, because the spec's shapes differ: details rx≈8, the vessel 14, thoughts 60–80.
        /// One sprite for all of them would turn a 50 px detail into a circle.
        /// </summary>
        public static Sprite RoundedOf(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 120);
            if (RoundedByRadius.TryGetValue(radius, out Sprite cached) && cached != null) return cached;

            Sprite sprite = BuildRounded(radius * 2 + 8, radius);
            RoundedByRadius[radius] = sprite;
            return sprite;
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = BuildCircle(128);
                return _circle;
            }
        }

        /// <summary>Annulus used with Image.fillMethod = Radial360 for progress / timer arcs.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null) _ring = BuildRing(128, 0.17f);
                return _ring;
            }
        }

        /// <summary>Horizontal dash pattern, tiled along the collection thread.</summary>
        public static Sprite Dash
        {
            get
            {
                if (_dash == null) _dash = BuildDash(26, 12, 6);
                return _dash;
            }
        }

        /// <summary>
        /// Dashed circular outline — the gaze contour of the mock (#gaze: stroke 5, dasharray 12/10).
        /// A plain ring sprite cannot express the gaps, so the dashes are baked into the texture.
        /// </summary>
        public static Sprite DashedRing
        {
            get
            {
                if (_dashedRing == null) _dashedRing = BuildDashedRing(256, 5f, 12f, 10f, 90f);
                return _dashedRing;
            }
        }

        private static Sprite BuildRounded(int size, int radius)
        {
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, InsideRoundedRect(x, y, size, size, radius) ? Color.white : Color.clear);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite BuildCircle(int size)
        {
            var tex = NewTexture(size, size);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                float a = Mathf.Clamp01(r - d);          // 1 px of anti-aliasing at the edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildRing(int size, float thicknessFraction)
        {
            var tex = NewTexture(size, size);
            float outer = size * 0.5f;
            float inner = outer * (1f - Mathf.Clamp(thicknessFraction, 0.02f, 0.9f));
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - outer) * (x + 0.5f - outer) + (y + 0.5f - outer) * (y + 0.5f - outer));
                float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <param name="designRadius">Radius the dash lengths are specified at, so 12/10 px stay 12/10 px.</param>
        private static Sprite BuildDashedRing(int size, float strokeWidth, float dash, float gap,
            float designRadius)
        {
            var tex = NewTexture(size, size);
            float centre = size * 0.5f;
            float outer = centre - 1f;
            float scale = outer / designRadius;                  // texture px per design px
            float halfStroke = strokeWidth * scale * 0.5f;
            float period = (dash + gap) * scale;
            float dashLength = dash * scale;
            float ringRadius = outer - halfStroke;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - centre;
                float dy = y + 0.5f - centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float onStroke = Mathf.Clamp01(halfStroke - Mathf.Abs(distance - ringRadius));
                if (onStroke <= 0f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float arc = (Mathf.Atan2(dy, dx) + Mathf.PI) * ringRadius;   // arc length along the ring
                bool inDash = Mathf.Repeat(arc, period) < dashLength;
                tex.SetPixel(x, y, inDash ? new Color(1f, 1f, 1f, onStroke) : Color.clear);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildDash(int dash, int gap, int thickness)
        {
            int w = dash + gap;
            var tex = NewTexture(w, thickness);
            tex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < thickness; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, x < dash ? Color.white : Color.clear);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, thickness), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool InsideRoundedRect(int x, int y, int w, int h, int radius)
        {
            float px = x + 0.5f;
            float py = y + 0.5f;
            float cx = Mathf.Clamp(px, radius, w - radius);
            float cy = Mathf.Clamp(py, radius, h - radius);
            float dx = px - cx;
            float dy = py - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }
}
