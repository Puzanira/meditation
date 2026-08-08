using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>One collectable detail of level 1 (SCREENS.md, "Детали в сцене").</summary>
    public struct DetailSpec
    {
        public string Name;
        /// <summary>Home position, design px, centre.</summary>
        public Vector2 Home;
        public float Size;
        public Color Fill;
        public Color Stroke;
    }

    /// <summary>
    /// Level 1 "Деловой квартал" — the composition every scenette is staged on, transcribed from
    /// SCREENS.md and docs/design/gameplay-walkthrough.html. Numbers here are the spec's, not invented.
    /// </summary>
    public static class LevelOneData
    {
        public const string LevelTitle = "Деловой квартал";

        /// <summary>Vessel (портфель): centre (960, 930), 240×150.</summary>
        public static readonly Vector2 VesselCentre = new Vector2(960f, 930f);
        public static readonly Vector2 VesselSize = new Vector2(240f, 150f);

        // The sun / timer dial at (1800, 100) r 60 and its «0:42» caption lived here until 2026-08-07.
        // Both went out with the timer (founder; see LevelRules), and the top-right corner of the frame
        // is free on every level as a result.

        /// <summary>Crank indicator: centre (140, 950), r = 70 — SCREENS base, per-level in a level.</summary>
        public static readonly Vector2 CrankIndicatorCentre = new Vector2(140f, 950f);
        public const float CrankIndicatorRadius = 70f;

        /// <summary>
        /// What the crank indicator really covers: the dial is r = 70, but the «хватает» arc sits at
        /// r + 14 and the halo that lights up on a good spin at 86. Clearance has to be judged against
        /// the widest thing drawn, not against the dial.
        /// </summary>
        public const float CrankHaloRadius = 86f;

        /// <summary>HUD detail slots: from (60, 40), 5 × 72 px, step 88 (base; per-level in a level).</summary>
        public static readonly Vector2 SlotsOrigin = new Vector2(60f, 40f);
        public const float SlotSize = 72f;
        public const float SlotStep = 88f;

        /// <summary>Scene zone 0,0 1920×810; foreground strip 0,810 1920×270.</summary>
        public const float SceneHeight = 810f;

        /// <summary>Five skyscrapers: x, y, width, height (design px, top-left origin).</summary>
        public static readonly Rect[] Buildings =
        {
            new Rect(100f, 250f, 230f, 560f),
            new Rect(420f, 180f, 260f, 630f),
            new Rect(760f, 330f, 200f, 480f),
            new Rect(1180f, 220f, 250f, 590f),
            new Rect(1560f, 320f, 210f, 490f)
        };

        public static readonly DetailSpec[] Details =
        {
            new DetailSpec { Name = "одуванчик",     Home = new Vector2(180f, 760f),  Size = 50f, Fill = Hex("e8d44d"), Stroke = Hex("8f7d0a") },
            new DetailSpec { Name = "облако",        Home = new Vector2(560f, 330f),  Size = 60f, Fill = Hex("bcd3e8"), Stroke = Hex("4a7096") },
            new DetailSpec { Name = "чашка",         Home = new Vector2(1000f, 700f), Size = 50f, Fill = Hex("c9a689"), Stroke = Hex("7a5230") },
            new DetailSpec { Name = "воробей",       Home = new Vector2(1330f, 300f), Size = 50f, Fill = Hex("a8a29a"), Stroke = Hex("5f5a52") },
            new DetailSpec { Name = "тень вывески",  Home = new Vector2(1650f, 740f), Size = 50f, Fill = Hex("8d8d99"), Stroke = Hex("4c4c58") }
        };

        /// <summary>Thought placeholders of level 1 (WORLD.md), in the walkthrough's order.</summary>
        public static readonly string[] ThoughtLabels =
        {
            "гора посуды", "счёт ЖКХ", "клубок ?", "кошелёк", "кровать",
            "мишка", "фото бывшего", "мамин голос", "пирожное"
        };

        // Palette straight out of the walkthrough SVG.
        public static readonly Color Sky = Hex("e9e6de");
        public static readonly Color Building = Hex("d6d2c8");
        public static readonly Color Ground = Hex("dcd8cc");
        public static readonly Color VesselFill = Hex("7fc9a5");
        public static readonly Color VesselStroke = Hex("2b7a5c");
        public static readonly Color Thread = Hex("2b7a5c");
        public static readonly Color Gaze = Hex("4a7096");
        public static readonly Color Alarm = Hex("c0392b");
        /// <summary>Peak-of-chaos veil (walkthrough frame 16: #3a3050 @0.12).</summary>
        public static readonly Color Dim = Hex("3a3050");

        /// <summary>The veil's opacity as the mock authors it (frame 16: <c>opacity="0.12"</c>).</summary>
        public const float DimMockAlpha = 0.12f;

        /// <summary>
        /// The alpha the veil is actually given. The mock is an SVG and composites in sRGB; the stand's
        /// canvas renders in LINEAR colour space (ProjectSettings: Linear), where the very same 0.12
        /// only takes ~5 % off the picture instead of the mock's ~9 % — which is exactly what the design
        /// gate measured on the peak frame. This is the linear-space alpha that lands on frame 16's
        /// pixels over the stand's sky, derived from the mock's 0.12 rather than re-tuned by eye.
        /// </summary>
        public static readonly float DimAlpha = LinearAlphaMatchingSrgb(Dim, Sky, DimMockAlpha);
        /// <summary>Halo behind the crank dial: green in the zone (#dyn-on), pink on a slip (frame 14).</summary>
        public static readonly Color CrankHaloOk = Hex("cfe8db");
        public static readonly Color CrankHaloAlarm = Hex("f2d9d7");

        private static readonly Color[] ThoughtFills =
        {
            Hex("f2c7b8"), Hex("f0d290"), Hex("cfc3ee"), Hex("efb9cd"), Hex("b8d8c7")
        };

        private static readonly Color[] ThoughtStrokes =
        {
            Hex("c08b77"), Hex("b3922e"), Hex("8f7fc4"), Hex("bc7996"), Hex("6da089")
        };

        public static Color ThoughtFill(string label) => ThoughtFills[LabelIndex(label) % ThoughtFills.Length];

        public static Color ThoughtStroke(string label) => ThoughtStrokes[LabelIndex(label) % ThoughtStrokes.Length];

        private static int LabelIndex(string label)
        {
            for (int i = 0; i < ThoughtLabels.Length; i++)
                if (ThoughtLabels[i] == label) return i;
            return 0;
        }

        public static Color Hex(string rrggbb)
        {
            ColorUtility.TryParseHtmlString("#" + rrggbb, out Color c);
            c.a = 1f;
            return c;
        }

        // ---- defeat palette (walkthrough frame 17) --------------------------------------------

        /// <summary>
        /// Grey mixed into the blobs on the defeat screen. Frame 17 draws the wallpaper in the ordinary
        /// pastels with their colour drained — #d8c2b8, #d8cba0, #c9bfdd, #d3b7c4, #bccabf — which
        /// measure 0.07–0.26 HSV saturation against 0.13–0.53 for the live ones. 42 % grey reproduces
        /// that band: «цвета гаснут», but the pastel is still recognisable.
        /// </summary>
        public const float DefeatGreyMix = 0.42f;

        /// <summary>Top of frame 17's saturation band (#d8cba0). Nothing on the defeat screen sits above it.</summary>
        public const float DefeatMaxSaturation = 0.26f;

        /// <summary>Bottom of frame 17's saturation band (#bccabf) — the floor the fills must not fall under.</summary>
        public const float DefeatMinSaturation = 0.07f;

        /// <summary>Drain a colour into frame 17's band: «цвета гаснут», the pastel stays readable.</summary>
        public static Color Faded(Color colour)
        {
            float grey = Luminance(colour);
            Color faded = Color.Lerp(colour, new Color(grey, grey, grey, colour.a), DefeatGreyMix);

            // Mixing grey keeps the luminance, so one pass drains every fill into the band — but the
            // strokes start far more saturated (#b3922e is 0.74) and would end up the only coloured
            // thing on a screen whose whole point is that the colour has gone. Pull those to the ceiling.
            float max = Mathf.Max(faded.r, Mathf.Max(faded.g, faded.b));
            float min = Mathf.Min(faded.r, Mathf.Min(faded.g, faded.b));
            float spread = max - min;
            if (max <= 0.0001f || spread / max <= DefeatMaxSaturation) return faded;

            // Keep `share` of the colour: S = share·spread / (share·max + (1−share)·grey) = ceiling.
            float denominator = spread - DefeatMaxSaturation * (max - grey);
            if (denominator <= 0.0001f) return faded;
            float share = Mathf.Clamp01(DefeatMaxSaturation * grey / denominator);
            return Color.Lerp(new Color(grey, grey, grey, faded.a), faded, share);
        }

        /// <summary>HSV saturation, the quantity the design gate measures off the frames.</summary>
        public static float SaturationOf(Color colour)
        {
            float max = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
            float min = Mathf.Min(colour.r, Mathf.Min(colour.g, colour.b));
            return max <= 0.0001f ? 0f : (max - min) / max;
        }

        public static float Luminance(Color colour) =>
            colour.r * 0.299f + colour.g * 0.587f + colour.b * 0.114f;

        /// <summary>
        /// WCAG relative luminance — the quantity a CONTRAST is computed from, unlike
        /// <see cref="Luminance"/>, which is the perceptual weighting the mock's palette was judged by.
        ///
        /// It exists because the 2026-08-05 art made contrast the measurable question: the thoughts are
        /// black marker on transparent, and «читается или сливается» is a ratio, not a difference.
        /// </summary>
        public static float RelativeLuminance(Color colour) =>
            0.2126f * ToLinear(colour.r) + 0.7152f * ToLinear(colour.g) + 0.0722f * ToLinear(colour.b);

        private static float ToLinear(float channel) =>
            channel <= 0.04045f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        /// <summary>WCAG contrast ratio of two colours, 1:1 … 21:1. Order does not matter.</summary>
        public static float ContrastRatio(Color a, Color b)
        {
            float first = RelativeLuminance(a) + 0.05f;
            float second = RelativeLuminance(b) + 0.05f;
            return first > second ? first / second : second / first;
        }

        /// <summary>
        /// The floor a thought has to clear against whatever is behind it — the walkthrough's own note
        /// on the marker drop («если мысли сливаются… решать подложкой/обводкой»), read at the usual
        /// 3:1 for a large shape. Measured before the backing landed: 1.6–3.5:1 median per level.
        /// </summary>
        public const float MinThoughtContrast = 3f;

        /// <summary>Text against its plate on the outcome screens, at a museum metre.</summary>
        public const float MinTextContrast = 4.5f;

        // ---- sRGB mock ↔ linear canvas ---------------------------------------------------------

        /// <summary>
        /// The alpha a translucent overlay needs in a LINEAR-blending canvas to land on the pixel the
        /// mock's sRGB compositing produces over <paramref name="background"/>. Averaged over the three
        /// channels — for the peak veil they agree to ±0.013, so a single alpha reproduces the frame.
        /// </summary>
        public static float LinearAlphaMatchingSrgb(Color overlay, Color background, float srgbAlpha)
        {
            float sum = 0f;
            for (int channel = 0; channel < 3; channel++)
            {
                float bg = background[channel];
                float over = overlay[channel];
                float bgLinear = SrgbToLinear(bg);
                float overLinear = SrgbToLinear(over);
                float wanted = SrgbToLinear(bg + (over - bg) * srgbAlpha);
                float span = bgLinear - overLinear;
                sum += Mathf.Abs(span) < 0.00001f
                    ? srgbAlpha
                    : Mathf.Clamp01((bgLinear - wanted) / span);
            }
            return sum / 3f;
        }

        /// <summary>The sRGB transfer function the renderer applies to vertex colours, spelled out.</summary>
        public static float SrgbToLinear(float value)
        {
            if (value <= 0.04045f) return value / 12.92f;
            return Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        public static float LinearToSrgb(float value)
        {
            if (value <= 0.0031308f) return value * 12.92f;
            return 1.055f * Mathf.Pow(value, 1f / 2.4f) - 0.055f;
        }
    }
}
