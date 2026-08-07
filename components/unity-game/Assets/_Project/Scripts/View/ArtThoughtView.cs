using Meditation.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// A thought as the art drop draws it: one of the level's black marker scribbles (drop
    /// 2026-08-05 — before that, turquoise silhouettes), with the row of pips underneath. No label and
    /// no outlined box — the text registry says the thoughts carry no text at all («без текста —
    /// чёрная маркерная штриховка из дропа 2026-08-05»; the greybox captions are old placeholders
    /// that do not ship).
    ///
    /// The sprite is drawn at its own colour: the whole set is one ink by design, so tinting per
    /// thought would be inventing a distinction the designer removed on purpose.
    ///
    /// What the ink DOES need is something to sit on. Measured on the rendered frames, the hatching
    /// lands at 1.6–3.5:1 against the five plates (median; 40–97 % of it below 3:1, worst on the L4
    /// embankment at 1.60) — «чёрное на тёмно-синем не разглядеть», which is why the designer put her
    /// own sample strips in the walkthrough on a light backing. So every thought is drawn twice: the
    /// hatching, and under it the same sprite as a light halo (<see cref="BackingColour"/>, dilated by
    /// <see cref="BackingHaloPx"/> through <c>Meditation/ThoughtBacking</c>). A halo rather than a card
    /// because SCREENS keeps thoughts «без обведённой рамки» and the screen under them «дырявым».
    /// </summary>
    public sealed class ArtThoughtView
    {
        /// <summary>
        /// The light the black hatching is read against — #F2F0EA, the colour the designer set behind
        /// her own sample strips in <c>gameplay-walkthrough.html</c> («Арт уровней 1–5»). Off-white
        /// rather than white: the same paper the marker would have been drawn on.
        /// </summary>
        public static readonly Color BackingColour = new Color(242f / 255f, 240f / 255f, 234f / 255f);

        /// <summary>
        /// How far the backing spreads past the ink, design px. Three is a hair over the width of a
        /// hatch line at play size, which is what a halo has to be to separate the stroke from the
        /// plate without closing the gaps the hatching is made of.
        /// </summary>
        public const float BackingHaloPx = 3f;

        /// <summary>
        /// Pips under a thought: the same ink as the hatching, on the same backing.
        ///
        /// The old value was the drop's turquoise taken dark enough to read ON the silhouette — but a
        /// scribble has no fill, so the pips ended up on the bare plate, at 1.3–1.9:1. They now read
        /// the way the thought above them does.
        /// </summary>
        public static readonly Color PipColour = new Color(0.06f, 0.06f, 0.07f);

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

        /// <summary>
        /// The backing material — one instance PER THOUGHT, unlike the shared fade material.
        ///
        /// The dilation radius is a property, and it has to be a different number for every thought:
        /// the drop's canvases run from 454 to 1800 px and they are all drawn into the same 180–420 px
        /// class boxes, so a radius fixed in texels would be a 2 px halo on one scribble and a 6 px one
        /// on the next. A UGUI Image cannot carry a per-instance property block, so it carries its own
        /// material instead.
        ///
        /// Null when the shader is missing, reported once — the fallback is a thought with no backing,
        /// which is a red test rather than a crash.
        /// </summary>
        private static Material NewBackingMaterial()
        {
            var shader = Resources.Load<Shader>(Mechanics.LevelCatalog.ArtRoot + "shaders/thought-backing");
            if (shader == null)
            {
                if (!_backingReported)
                {
                    _backingReported = true;
                    Debug.LogError("[Meditation] Нет шейдера подложки: " +
                                   Mechanics.LevelCatalog.ArtRoot + "shaders/thought-backing");
                }
                return null;
            }

            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material.SetColor(BackingProperty, BackingColour);
            return material;
        }

        private static readonly int DesaturateProperty = Shader.PropertyToID("_Desaturate");
        private static readonly int LightenProperty = Shader.PropertyToID("_Lighten");
        private static readonly int BackingProperty = Shader.PropertyToID("_Backing");
        private static readonly int SpreadProperty = Shader.PropertyToID("_SpreadUV");

        private static Material _fadeMaterial;
        private static bool _fadeReported;
        private static bool _backingReported;

        private readonly Image _backing;
        private readonly Material _backingMaterial;
        private readonly Image _root;
        private readonly PipRow _pips;

        private int _lastHitsTaken;
        private bool _drained;
        private float _flinch;
        private Vector2 _flinchOffset;
        private string _boundKey;

        public ArtThoughtView(Transform parent)
        {
            // The backing is a SIBLING created first, not a child: in UGUI a child always draws above
            // its parent, and the whole point of this one is to be underneath.
            _backing = Ui.NewImage(parent, "ThoughtBacking");
            _backing.color = Color.white;
            _backing.preserveAspect = true;
            Ui.Place(_backing.rectTransform, 0f, 0f, 280f, 220f);
            _backingMaterial = NewBackingMaterial();
            _backing.material = _backingMaterial;
            _backing.gameObject.SetActive(_backingMaterial != null);

            _root = Ui.NewImage(parent, "Thought");
            _root.color = Color.white;
            _root.preserveAspect = true;
            Ui.Place(_root.rectTransform, 0f, 0f, 280f, 220f);

            _pips = new PipRow(_root.transform, -14f, BackingColour);
        }

        public RectTransform Rect => _root.rectTransform;

        public GameObject GameObject => _root.gameObject;

        /// <summary>
        /// Drop the per-thought backing material. The view's GameObjects go with the level's root, but
        /// a Material is not a component — five levels of pooled thoughts would leak a hundred of them,
        /// and Unity reports that as a leaked-object warning in the middle of an unrelated test.
        /// </summary>
        public void Dispose()
        {
            if (_backingMaterial != null) Object.Destroy(_backingMaterial);
        }

        public void SetActive(bool active)
        {
            _root.gameObject.SetActive(active);
            _backing.gameObject.SetActive(active && _backingMaterial != null && _root.sprite != null);
        }

        /// <summary>
        /// The size the whole thought is drawn at, 1 = its own. SCREENS «Мысли (визуал состояния)»:
        /// «Последний пипс — мысль лопается (масштаб 1.1 → 0, 200 мс)» — see <see cref="ThoughtPop"/>.
        ///
        /// Ink, halo and pips go together, and only the first two are set here: the row of pips is a
        /// CHILD of the ink and rides it, while the backing is a sibling (it has to draw underneath),
        /// so a halo left alone would hang in the air around a scribble shrinking out of it.
        /// </summary>
        public void SetScale(float scale)
        {
            var s = new Vector3(scale, scale, 1f);
            _root.rectTransform.localScale = s;
            _backing.rectTransform.localScale = s;
        }

        /// <summary>What the thought is currently drawn at (see <see cref="SetScale"/>).</summary>
        public float Scale => _root.rectTransform.localScale.x;

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
            Vector2 box = thought.Wallpaper ? CoverFit(_root.sprite, thought.Size) : thought.Size;
            _root.rectTransform.sizeDelta = box;
            _backing.rectTransform.sizeDelta = box;
            _backing.sprite = _root.sprite;
            _backing.gameObject.SetActive(_backingMaterial != null && _root.sprite != null);
            if (_backingMaterial != null && _root.sprite != null)
            {
                // The halo is asked for in DESIGN px and handed over in UV, because only this side
                // knows how far down the sprite is being drawn (see NewBackingMaterial).
                Vector2 drawn = ContainFit(_root.sprite, box);
                _backingMaterial.SetVector(SpreadProperty, new Vector4(
                    BackingHaloPx / Mathf.Max(1f, drawn.x),
                    BackingHaloPx / Mathf.Max(1f, drawn.y), 0f, 0f));
            }

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
            Ui.MoveTo(_backing.rectTransform, centre);

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

        /// <summary>
        /// The rect the sprite actually fills inside <paramref name="box"/> — what
        /// <c>Image.preserveAspect</c> does. The backing needs it to state its halo in design px: the
        /// rect a thought is given is a CLASS box (S/M/L), and a tall scribble leaves half of it empty.
        /// </summary>
        public static Vector2 ContainFit(Sprite sprite, Vector2 box)
        {
            if (sprite == null) return box;
            Vector2 native = sprite.rect.size;
            if (native.x < 1f || native.y < 1f) return box;

            float factor = Mathf.Min(box.x / native.x, box.y / native.y);
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
