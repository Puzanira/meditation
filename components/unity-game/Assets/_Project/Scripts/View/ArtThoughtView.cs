using Meditation.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// A thought as the art drop draws it: one of the level's turquoise silhouettes, with the row of
    /// pips underneath. No label and no outlined box — the drop's own note is that the soft blurred
    /// edge IS the style, and the text registry says the thoughts carry no text at all («без текста —
    /// бирюзовые силуэты из арт-дропа»; the greybox captions are old placeholders that do not ship).
    ///
    /// The sprite is drawn at its own colour: the whole set is one turquoise (#5ADFE6) by design, so
    /// tinting per thought would be inventing a distinction the designer removed on purpose.
    /// </summary>
    public sealed class ArtThoughtView
    {
        /// <summary>Pips under a silhouette: the drop's turquoise taken dark enough to read on it.</summary>
        public static readonly Color PipColour = new Color(0.09f, 0.36f, 0.39f);

        /// <summary>
        /// Defeat, «лёгкая десатурация» (SCREENS S5): how much of the silhouette is mixed with its own
        /// luminance. The drop's whole thought set is one turquoise #5ADFE6, saturation 0.609; this
        /// number is what puts it in the middle of frame 17's band — 0.19 measured on the arithmetic
        /// below, 0.14 measured on the rendered pixel (the canvas blends in LINEAR space, where the
        /// same mix lands a little further down the band). It is also the number the shader is given.
        /// </summary>
        public const float DefeatDesaturate = 0.68f;

        /// <summary>
        /// …and how far what is left is lifted towards white. «Цвета гаснут» on a night street has to
        /// LIGHTEN: the blob stays a blob, it just stops being turquoise.
        /// </summary>
        public const float DefeatLighten = 0.12f;

        /// <summary>
        /// The defeat transform, in C#, exactly as <c>Meditation/ThoughtFade</c> does it per pixel.
        ///
        /// It exists so the claim can be checked without a renderer: the old test measured
        /// <c>LevelOneData.Faded</c>, which is the GREYBOX stand's palette — the art path never went
        /// through it, and its multiply-only tint was quietly making the turquoise MORE saturated
        /// (0.474 → 0.492 on a measured frame) while the suite stayed green.
        /// </summary>
        public static Color Drain(Color source)
        {
            float lum = LevelOneData.Luminance(source);
            Color drained = Color.Lerp(source, new Color(lum, lum, lum, source.a), DefeatDesaturate);
            Color lifted = Color.Lerp(drained, new Color(1f, 1f, 1f, drained.a), DefeatLighten);
            lifted.a = source.a;
            return lifted;
        }

        /// <summary>
        /// The material the silhouettes are drawn through on the defeat screen — one shared instance,
        /// because every thought is drained by the same two numbers.
        ///
        /// Null when the shader is missing, and that is reported once: the fallback (no material) is a
        /// defeat screen that simply is not drained, which is worth a red test rather than a crash.
        /// </summary>
        public static Material FadeMaterial
        {
            get
            {
                if (_fadeMaterial != null) return _fadeMaterial;
                if (_fadeReported) return null;

                var shader = Resources.Load<Shader>(Mechanics.LevelCatalog.ArtRoot + "shaders/thought-fade");
                if (shader == null)
                {
                    _fadeReported = true;
                    Debug.LogError("[Meditation] Нет шейдера десатурации: " +
                                   Mechanics.LevelCatalog.ArtRoot + "shaders/thought-fade");
                    return null;
                }

                _fadeMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                _fadeMaterial.SetFloat(DesaturateProperty, DefeatDesaturate);
                _fadeMaterial.SetFloat(LightenProperty, DefeatLighten);
                return _fadeMaterial;
            }
        }

        private static readonly int DesaturateProperty = Shader.PropertyToID("_Desaturate");
        private static readonly int LightenProperty = Shader.PropertyToID("_Lighten");

        private static Material _fadeMaterial;
        private static bool _fadeReported;

        private readonly Image _root;
        private readonly PipRow _pips;

        private int _lastHitsTaken;
        private bool _drained;
        private float _flinch;
        private Vector2 _flinchOffset;
        private string _boundKey;

        public ArtThoughtView(Transform parent)
        {
            _root = Ui.NewImage(parent, "Thought");
            _root.color = Color.white;
            _root.preserveAspect = true;
            Ui.Place(_root.rectTransform, 0f, 0f, 280f, 220f);

            _pips = new PipRow(_root.transform, -14f);
        }

        public RectTransform Rect => _root.rectTransform;

        public GameObject GameObject => _root.gameObject;

        public void SetActive(bool active) => _root.gameObject.SetActive(active);

        /// <param name="desaturated">Defeat screen: «цвета гаснут» (walkthrough frame 17).</param>
        public void Bind(Thought thought, float deltaTime, bool desaturated = false)
        {
            if (_boundKey != thought.Label)
            {
                _boundKey = thought.Label;
                _root.sprite = ArtLibrary.Get(thought.Label);
            }

            // A silhouette with no sprite behind it would be an invisible obstacle — the player would
            // lose to a blank screen. Fall back to a flat turquoise block so the failure is visible,
            // and ArtLibrary has already logged which key is missing.
            _root.color = _root.sprite != null
                ? Color.white
                : (desaturated ? Drain(new Color(0.35f, 0.87f, 0.9f, 0.85f))
                               : new Color(0.35f, 0.87f, 0.9f, 0.85f));

            // The drain is a per-pixel operation on a coloured sprite, so it rides on the material
            // rather than on the tint — see DefeatDesaturate. Tracked by a flag, because Graphic.material
            // hands back the DEFAULT material when none is set, so «is it already null» cannot be asked.
            if (_drained != desaturated)
            {
                _drained = desaturated;
                _root.material = desaturated ? FadeMaterial : null;
            }

            // A silhouette normally sits INSIDE its class box (contain). The defeat wallpaper is the
            // one exception: there a blob has to close its cell, so it is scaled to COVER it — same
            // aspect, no squashing, just bigger than the hole it fills.
            _root.rectTransform.sizeDelta = thought.Wallpaper
                ? CoverFit(_root.sprite, thought.Size)
                : thought.Size;

            if (thought.HitsTaken > _lastHitsTaken)
            {
                // SCREENS: «мысль вздрагивает (±6 px, 80 мс)».
                _flinch = 0.08f;
                _flinchOffset = new Vector2(Random.Range(-6f, 6f), Random.Range(-6f, 6f));
            }
            _lastHitsTaken = thought.HitsTaken;

            if (_flinch > 0f) _flinch = Mathf.Max(0f, _flinch - deltaTime);
            Vector2 offset = _flinch > 0f ? _flinchOffset : Vector2.zero;
            Vector2 centre = thought.Position + offset;
            Ui.MoveTo(_root.rectTransform, centre);

            BindPips(thought, centre, desaturated);
        }

        /// <summary>
        /// The pips belong to a thought you can see. Off screen they used to hang at the frame's edge
        /// out of nowhere (the row is anchored under a blob that is itself outside the picture), and at
        /// the chaos peak a dozen of those rows drew solid dotted lines across the scene. So: no pips
        /// for a thought that is barely in the frame, and for one that is half in, the row is pulled to
        /// the part of it that shows.
        /// </summary>
        /// <summary>Smallest rect of this sprite's aspect that still covers <paramref name="box"/>.</summary>
        public static Vector2 CoverFit(Sprite sprite, Vector2 box)
        {
            if (sprite == null) return box;
            Vector2 native = sprite.rect.size;
            if (native.x < 1f || native.y < 1f) return box;

            float factor = Mathf.Max(box.x / native.x, box.y / native.y);
            return native * factor;
        }

        private void BindPips(Thought thought, Vector2 centre, bool desaturated)
        {
            if (thought.Wallpaper)
            {
                _pips.SetShown(false);
                return;
            }

            Vector2 size = thought.Size;
            float visible = VisibleShare(centre, size);
            if (visible < MinVisibleShareForPips)
            {
                _pips.SetShown(false);
                return;
            }

            _pips.SetShown(true);

            // Where the row would land, in design px, and where it is allowed to land.
            float wanted = centre.y + size.y * 0.5f - _pips.OffsetY;
            float clamped = Mathf.Clamp(wanted, PipMargin, ThoughtField.ScreenHeight - PipMargin);
            float shiftX = Mathf.Clamp(centre.x, PipSideMargin, ThoughtField.ScreenWidth - PipSideMargin)
                           - centre.x;

            _pips.SetOffset(new Vector2(shiftX, -(clamped - wanted)));
            _pips.Set(thought.Durability, thought.HitsRemaining,
                desaturated ? LevelOneData.Faded(PipColour) : PipColour);
        }

        /// <summary>Below this much of the blob in frame the pips are noise, not a readout.</summary>
        public const float MinVisibleShareForPips = 0.35f;

        private const float PipMargin = 26f;
        private const float PipSideMargin = 180f;

        /// <summary>Share of the blob's rectangle that is inside the frame, 0..1.</summary>
        public static float VisibleShare(Vector2 centre, Vector2 size)
        {
            float w = Mathf.Max(1f, size.x);
            float h = Mathf.Max(1f, size.y);
            float overlapX = Mathf.Max(0f, Mathf.Min(centre.x + w * 0.5f, ThoughtField.ScreenWidth) -
                                           Mathf.Max(centre.x - w * 0.5f, 0f));
            float overlapY = Mathf.Max(0f, Mathf.Min(centre.y + h * 0.5f, ThoughtField.ScreenHeight) -
                                           Mathf.Max(centre.y - h * 0.5f, 0f));
            return overlapX * overlapY / (w * h);
        }
    }
}
