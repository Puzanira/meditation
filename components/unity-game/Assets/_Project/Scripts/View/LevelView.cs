using System.Collections.Generic;
using Meditation.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// Screen S3 for a real level: the art drop's background plate, its details on the positions the
    /// designer authored, its vessel with its progress bar, its thoughts, and the HUD (N slots and the
    /// crank indicator — the sun-dial went out with the timer, founder 2026-08-07).
    ///
    /// It is the art twin of <see cref="StageView"/>, not a replacement: the stand keeps its greybox
    /// so the mechanics can still be judged without a picture in the way. Both implement
    /// <see cref="ICollectionView"/>, so the collection loop underneath them is literally the same code.
    ///
    /// Z-order (SCREENS «Зоны», with the walkthrough's refinement): background → details → thread →
    /// thoughts → vessel → peak veil → HUD → hints → outcome screen. The vessel keeps its place above
    /// the thoughts unless the [toggle] «мысли закрывают сосуд и деталь» says otherwise (MECHANICS §4);
    /// the outcome layer is above even the HUD, because a drawn finished screen covers the frame whole.
    /// </summary>
    public sealed class LevelView : ICollectionView
    {
        /// <summary>Mock 18 keeps the location behind the victory tableau at half strength.</summary>
        public const float VictoryBackgroundAlpha = 0.5f;

        /// <summary>HUD slot side and step (SCREENS «HUD: слоты деталей»); the origin is per level.</summary>
        public const float SlotSize = LevelOneData.SlotSize;
        public const float SlotStep = LevelOneData.SlotStep;

        private readonly Transform _parent;
        private readonly LevelDefinition _level;

        private readonly List<ArtThoughtView> _thoughtPool = new List<ArtThoughtView>();

        // The burst («последний пипс — мысль лопается») and the bookkeeping that spots one. A popped
        // thought is gone from the model the instant it pops, so the only way the view learns about it
        // is by remembering which thoughts it drew last frame — see SyncThoughts.
        private readonly List<ArtThoughtView> _popPool = new List<ArtThoughtView>();
        private readonly List<bool> _popSlotBusy = new List<bool>();
        private readonly List<ThoughtPop> _pops = new List<ThoughtPop>();
        private readonly List<Thought> _drawnLastFrame = new List<Thought>();

        private readonly List<Image> _detailImages = new List<Image>();
        private readonly List<Image> _detailRings = new List<Image>();
        private readonly List<Image> _detailOutlines = new List<Image>();
        private readonly List<Image> _slots = new List<Image>();
        private readonly List<Image> _slotFills = new List<Image>();
        private readonly List<Image> _vesselContents = new List<Image>();
        private readonly List<Vector2> _vesselContentSizes = new List<Vector2>();
        private readonly List<Material> _sweepMaterials = new List<Material>();
        private readonly List<Material> _outlineMaterials = new List<Material>();

        /// <summary>
        /// The thoughts as of the last <see cref="SyncThoughts"/> — kept only so a progress ring can
        /// ask whether the detail it circles is buried (see <see cref="FullyUnderAThought"/>). Held by
        /// reference, like everything else the field hands out; the view never edits it.
        /// </summary>
        private IReadOnlyList<Thought> _liveThoughts;

        private Image _background;
        private Image _vessel;
        private Image _vesselFillTrack;
        private Image _vesselFillLevel;
        private CanvasGroup _thoughtsGroup;
        private CanvasGroup _vesselGroup;
        private Image _outcome;
        private Image _outcomePlate;
        private Image _crankHalo;
        private RectTransform _vesselWindow;
        private Material _gazeMaterial;

        private float _vesselBounce;
        private float _pulse;
        private bool _coverApplied;
        private bool _defeatOrder;
        private int _draggedIndex = -1;
        private bool _desaturated;
        private bool _victoryStaged;
        private bool _introPulse;

        public LevelView(Transform parent, LevelDefinition level)
        {
            _parent = parent;
            _level = level;
            Build();
        }

        public LevelDefinition Level => _level;

        public RectTransform Root { get; private set; }
        public RectTransform SceneLayer { get; private set; }
        public RectTransform DetailsLayer { get; private set; }
        public RectTransform ThreadLayer { get; private set; }
        public RectTransform ThoughtsLayer { get; private set; }
        public RectTransform VesselLayer { get; private set; }
        public RectTransform PeakLayer { get; private set; }
        public RectTransform HudLayer { get; private set; }
        public RectTransform MessageLayer { get; private set; }

        /// <summary>Above everything, including the HUD: a finished screen covers the frame whole.</summary>
        public RectTransform OutcomeLayer { get; private set; }

        public Image Vessel => _vessel;

        /// <summary>
        /// What the tableau moves and scales: the rounded window for a vessel cut out of the plate,
        /// the vessel image itself otherwise. Everything that positions «the vessel» goes through here.
        /// </summary>
        public RectTransform VesselRect => _vesselWindow != null ? _vesselWindow : _vessel.rectTransform;
        public Image Background => _background;

        public Image CrankDial { get; private set; }
        public Image CrankArc { get; private set; }
        public RectTransform CrankNeedle { get; private set; }
        public Image Thread { get; private set; }

        /// <summary>
        /// The neon aim (<c>Meditation/GazeNeon</c>). One image where there used to be three: the pale
        /// disc, the dashed ring sprite and their outline were all invisible on a photographic plate,
        /// and the founder asked for the circle to be «крупнее и заметнее». Kept under its old name
        /// because everything that asks «виден ли взгляд» asks about this rect.
        /// </summary>
        public Image Gaze { get; private set; }

        /// <summary>Same object as <see cref="Gaze"/> — the ring IS the circle now, not a second layer.</summary>
        public Image GazeRing => Gaze;

        public Image GazeArc { get; private set; }
        public Image PeakVignette { get; private set; }

        /// <summary>The peak's darkened edges — the half of it that is visible on a photograph.</summary>
        public Image PeakEdges { get; private set; }

        /// <summary>The beat's own button — НАВОДИ, КРУТИ РУЧКУ — with its arrow.</summary>
        public ButtonHint Hint { get; private set; }

        /// <summary>
        /// The second button of a beat. Beat 2 shows two at once (SCREENS §Обучение п.2): «КРУТИ
        /// РУЧКУ» at the dynamo indicator and «ТАЩИ» beside the detail on its thread — one names the
        /// hand, the other names what the hand is doing to.
        /// </summary>
        public ButtonHint SecondHint { get; private set; }

        /// <summary>
        /// The drawn outcome screen on top of everything — <c>screens/level-complete</c> after a win,
        /// <c>screens/game-over</c> after a loss (drop 2026-08-07, S4/S5). Its alpha is the retry's own
        /// progress bar: every turn of the handle wipes a share of it away.
        /// </summary>
        public Image OutcomeScreen => _outcome;

        /// <summary>
        /// The dark plate under the defeat screen's own copy — see <see cref="DefeatTextPlate"/>.
        /// </summary>
        public Image OutcomeTextPlate => _outcomePlate;

        public IReadOnlyList<Image> Slots => _slots;
        public IReadOnlyList<Image> DetailImages => _detailImages;
        public IReadOnlyList<ArtThoughtView> ThoughtViews => _thoughtPool;

        /// <summary>
        /// Thoughts that are only finishing their 200 ms burst — out of the model, still on the screen
        /// (<see cref="ThoughtPop"/>). Empty at every other moment.
        /// </summary>
        public IReadOnlyList<ThoughtPop> Pops => _pops;

        /// <summary>The views the bursts are drawn through; reused, so most of them are off.</summary>
        public IReadOnlyList<ArtThoughtView> PopViews => _popPool;

        /// <summary>The vessel's fill indicator — present on level 3, where the bag is baked in.</summary>
        public Image VesselFillLevel => _vesselFillLevel;

        /// <summary>The bar's track — its own place on the screen, which the suite measures against.</summary>
        public Image VesselFillTrack => _vesselFillTrack;

        /// <summary>The haul inside the vessel, in collection order — the render gate measures these.</summary>
        public IReadOnlyList<Image> VesselContents => _vesselContents;

        // ---- build ------------------------------------------------------------------------------

        private void Build()
        {
            Root = Ui.Layer(_parent, "Level" + _level.Number);

            SceneLayer = Ui.Layer(Root, "SceneLayer");
            DetailsLayer = Ui.Layer(Root, "DetailsLayer");
            ThreadLayer = Ui.Layer(Root, "ThreadLayer");
            ThoughtsLayer = Ui.Layer(Root, "ThoughtsLayer");
            VesselLayer = Ui.Layer(Root, "VesselLayer");
            PeakLayer = Ui.Layer(Root, "PeakLayer");
            HudLayer = Ui.Layer(Root, "HudLayer");
            MessageLayer = Ui.Layer(Root, "MessageLayer");
            OutcomeLayer = Ui.Layer(Root, "OutcomeLayer");

            BuildBackground();
            BuildDetails();

            Thread = Ui.Dashed(ThreadLayer, "Thread", LevelOneData.Thread, 5f);
            Thread.gameObject.SetActive(false);

            BuildGaze();

            _thoughtsGroup = ThoughtsLayer.gameObject.AddComponent<CanvasGroup>();

            // Two layers, because the mock's peak is two things (SCREENS «Мысли»): the flat veil
            // #3a3050 @0.12 of frame 16, and the darkened edges named in the same line. On a night
            // street the veil alone moved the road by 4 values out of 255 — the peak was a number with
            // no picture. See PeakVeilAlpha / UiSprites.Vignette.
            PeakVignette = Ui.BoxCentred(PeakLayer, "PeakVignette", 960f, 540f, 1920f, 1080f,
                new Color(LevelOneData.Dim.r, LevelOneData.Dim.g, LevelOneData.Dim.b, 0f));

            PeakEdges = Ui.NewImage(PeakLayer, "PeakEdges");
            PeakEdges.sprite = UiSprites.Vignette;
            PeakEdges.color = new Color(1f, 1f, 1f, 0f);
            Ui.Place(PeakEdges.rectTransform, 960f, 540f, 1920f, 1080f);

            _vesselGroup = VesselLayer.gameObject.AddComponent<CanvasGroup>();

            BuildVessel();
            BuildHud();
            BuildHint();
            BuildOutcome();

            ApplyCoverToggle(true);
        }

        private void BuildBackground()
        {
            _background = Ui.NewImage(SceneLayer, "Background");
            _background.sprite = ArtLibrary.Get(_level.BackgroundSprite);
            // A missing plate must not be a black screen that looks like a load failure of the level.
            _background.color = _background.sprite != null ? Color.white : LevelOneData.Sky;
            Ui.Place(_background.rectTransform, 960f, 540f, 1920f, 1080f);
        }

        private void BuildDetails()
        {
            for (int i = 0; i < _level.Details.Length; i++)
            {
                ArtDetail spec = _level.Details[i];

                Image image = Ui.NewImage(DetailsLayer, "Detail_" + spec.Name);
                image.sprite = ArtLibrary.Get(spec.Sprite);
                image.preserveAspect = true;
                image.color = image.sprite != null ? Color.white : Color.magenta;
                Ui.Place(image.rectTransform, spec.Home.x, spec.Home.y, spec.Size.x, spec.Size.y);
                GiveItsOwnSweepMaterial(image);
                _detailImages.Add(image);
                GiveItsOwnNeonOutline(image, spec);

                // The ring goes round the INK, not round the rectangle: the plane is drawn with 600 px
                // of contrail behind it, so a ring centred on its box circles empty sky.
                Vector2 anchor = LevelCatalog.AnchorOf(spec);
                Image ring = Ui.Ring(DetailsLayer, "Ring_" + spec.Name, anchor.x, anchor.y,
                    RingRadius(spec.Size), LevelOneData.VesselStroke);
                ring.fillAmount = 0f;
                ring.gameObject.SetActive(false);
                _detailRings.Add(ring);
            }
        }

        /// <summary>
        /// The progress ring hugs the detail it is drawn around. The greybox could use one radius for
        /// everything because every placeholder was 50 px; the art drop's details run from a 46 px
        /// flowerpot to an 802 px vine, and a fixed 55 px ring would sit inside most of them.
        /// </summary>
        public static float RingRadius(Vector2 size) =>
            Mathf.Clamp(Mathf.Max(size.x, size.y) * 0.62f, 36f, 96f);

        /// <summary>The ring drawn around detail <paramref name="index"/> of this level, design px.</summary>
        public float RingRadiusOf(int index) =>
            index >= 0 && index < _level.DetailCount
                ? RingRadius(_level.Details[index].Size)
                : 0f;

        /// <summary>
        /// The neon aim: one quad, one shader, a radius that comes off the panel every frame.
        ///
        /// The quad is drawn BIGGER than the circle (<see cref="GazeQuadMargin"/>) because the glow is
        /// the half of neon that reads — clipped at the ring's own radius it goes back to being a
        /// hard-edged outline, which is the thing that was invisible in the first place.
        /// </summary>
        private void BuildGaze()
        {
            Gaze = Ui.NewImage(DetailsLayer, "Gaze");
            Gaze.sprite = UiSprites.Circle;   // any opaque quad: the shader decides what is drawn
            Gaze.color = Color.white;
            Gaze.raycastTarget = false;

            // Ui.Place, not a bare sizeDelta: a fresh RectTransform anchors to its parent's CENTRE,
            // and every design coordinate in this project is measured from the top-left. Placed once
            // here so ApplyGazeSize below can move the SIZE alone (this cost the neon aim its first
            // gate run — the circle was drawn a screen and a half off the frame and the picture test
            // read «прицела нет»).
            Ui.Place(Gaze.rectTransform, 960f, 540f, 1f, 1f);

            var shader = Resources.Load<Shader>(LevelCatalog.ArtRoot + "shaders/gaze-neon");
            if (shader != null)
            {
                _gazeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _gazeMaterial.SetColor(NeonColourId, NeonAim);
                Gaze.material = _gazeMaterial;
            }
            else
            {
                // No shader must not mean no aim: fall back to the flat disc the stand draws.
                Gaze.color = new Color(0.478f, 0.655f, 0.851f, 0.4f);
            }

            GazeArc = Ui.Ring(DetailsLayer, "GazeArc", 960f, 540f, GazeSelector.Radius + 14f,
                NeonAim);
            GazeArc.fillAmount = 0f;

            ApplyGazeSize();

            Gaze.gameObject.SetActive(false);
            GazeArc.gameObject.SetActive(false);
        }

        /// <summary>How much wider than the circle the neon quad is drawn, so the bloom is not clipped.</summary>
        public const float GazeQuadMargin = 1.45f;

        /// <summary>
        /// The neon's colour — the same turquoise the drop's own hint buttons are painted in (#8fd7d6,
        /// measured across навoди / крути ручку / тащи). The aim belongs to the same set of things that
        /// say «сюда», so it is the same ink.
        /// </summary>
        public static readonly Color NeonAim = new Color(0.561f, 0.843f, 0.839f, 1f);

        /// <summary>
        /// Push the panel's radius / glow / ring width into the material and the rect. Called every
        /// frame the aim is drawn, because these are sliders and «правки действуют сразу».
        /// </summary>
        private void ApplyGazeSize()
        {
            float radius = GazeSelector.Radius;
            float quad = radius * 2f * GazeQuadMargin;
            Gaze.rectTransform.sizeDelta = new Vector2(quad, quad);

            if (_gazeMaterial != null)
            {
                // In fractions of the quad, which is what the shader works in.
                _gazeMaterial.SetFloat(GazeRingId, 0.5f / GazeQuadMargin);
                _gazeMaterial.SetFloat(GazeRingWidthId,
                    Mathf.Clamp(Tuning.TuningConfig.GazeNeonRingPx * 0.5f / Mathf.Max(1f, quad), 0.002f, 0.25f));
                _gazeMaterial.SetFloat(GazeGlowId, Mathf.Max(0f, Tuning.TuningConfig.GazeNeonGlow));
            }

            GazeArc.rectTransform.sizeDelta = new Vector2((radius + 14f) * 2f, (radius + 14f) * 2f);
        }

        private static readonly int NeonColourId = Shader.PropertyToID("_Neon");
        private static readonly int GazeRingId = Shader.PropertyToID("_RingU");
        private static readonly int GazeRingWidthId = Shader.PropertyToID("_RingWidthU");
        private static readonly int GazeGlowId = Shader.PropertyToID("_Glow");

        private void BuildVessel()
        {
            Vector2 centre = _level.VesselCentre;
            Vector2 size = _level.VesselSize;

            // A baked vessel is a rectangle cut out of the plate. Sitting on its own pixels that never
            // shows, but the victory tableau lifts it to the centre at ×2 — where the cut's hard edges
            // and the corner of the passenger's coat inside it are the first thing you see. So the cut
            // travels in a rounded window, and it is the WINDOW that the tableau moves and scales.
            if (_level.VesselIsBaked)
            {
                _vesselWindow = Ui.RoundedMask(VesselLayer, "VesselWindow",
                    centre.x, centre.y, size.x, size.y, 18);
                _vessel = Ui.NewImage(_vesselWindow, "Vessel");
                _vessel.sprite = ArtLibrary.VesselOf(_level);
                _vessel.color = _vessel.sprite != null ? Color.white : LevelOneData.VesselFill;
                RectTransform inside = _vessel.rectTransform;
                inside.anchorMin = Vector2.zero;
                inside.anchorMax = Vector2.one;
                inside.offsetMin = Vector2.zero;
                inside.offsetMax = Vector2.zero;
            }
            else
            {
                _vessel = Ui.NewImage(VesselLayer, "Vessel");
                _vessel.sprite = ArtLibrary.VesselOf(_level);
                _vessel.color = _vessel.sprite != null ? Color.white : LevelOneData.VesselFill;
                Ui.Place(_vessel.rectTransform, centre.x, centre.y, size.x, size.y);
            }

            // The progress bar under the vessel, on EVERY level (founder, 2026-08-07).
            //
            // It started as level 3's own workaround: that vessel is painted into the plate, so
            // «наполнение» could not be shown by putting things inside it and SCREENS asked for an
            // overlay indicator. The bar that came out of it turned out to be the thing the other four
            // levels were missing too — the haul inside a bucket or a briefcase is readable only if you
            // already know how many details this level has, and the slot row at the top of the frame is
            // the other side of the screen from where the player is looking. Same widget, same numbers,
            // same colour on all five: the metro's indicator is left exactly as it was, and the reason
            // it can be is that there was never anything metro-specific about it beyond where it stood.
            // …and it lives on the HUD LAYER, not on the vessel's (design gate, 2026-08-08). SCREENS
            // files it under the HUD — «мысли HUD не закрывают» — and it was the one HUD widget that
            // did not obey that, because it was built here, beside the thing it stands under. With the
            // cover toggle on (the chaos peak, the office's foreground strip) the thought layer draws
            // above the vessel, so the bar disappeared under the blobs at exactly the moment it is the
            // only thing telling the player how much of the level is left. Where it STANDS is still the
            // vessel's business (LevelCatalog.VesselBarRectOf); what may cover it is the HUD's.
            Rect track = LevelCatalog.VesselBarRectOf(_level);

            _vesselFillTrack = Ui.Rounded(HudLayer, "VesselFillTrack",
                track.center.x, track.center.y, track.width, track.height,
                new Color(0f, 0f, 0f, 0.22f), new Color(1f, 1f, 1f, 0.32f), 2f, 7);

            _vesselFillLevel = Ui.NewImage(_vesselFillTrack.transform, "VesselFillLevel");
            _vesselFillLevel.color = MetroFill;
            RectTransform fill = _vesselFillLevel.rectTransform;
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(3f, -3f);
            fill.sizeDelta = new Vector2(0f, -6f);
        }

        /// <summary>The warm orange the metro's indicator was painted in, now the bar's colour on all five.</summary>
        private static readonly Color MetroFill = new Color(0.89f, 0.45f, 0.28f, 0.92f);

        /// <summary>Padding between a slot's border and the picture inside it, design px.</summary>
        public const float SlotPadding = 7f;

        /// <summary>
        /// A slot is two layers, and it is two layers in BOTH states.
        ///
        /// Underneath, the detail's silhouette in flat ink (<see cref="SlotSilhouette"/>) — that is what
        /// makes a pale cloud or a hairline vine into something readable from a metre. On top, the
        /// detail's own picture, shown only once it has been collected, so the row still says «нашёл»
        /// in colour. The silhouette is dilated a little past the picture, so a collected slot keeps a
        /// dark outline instead of being a cream moon on a white plate.
        /// </summary>
        private static Image BuildSlotPicture(Image slot, string name, Sprite sprite, float inner,
            float silhouetteRadius = 0f)
        {
            SlotSilhouette.Build(slot.transform, name + "Sil", sprite, SlotPadding, inner,
                silhouetteRadius);

            Image picture = Ui.NewImage(slot.transform, name);
            picture.sprite = sprite;
            picture.preserveAspect = true;
            picture.color = SlotPictureHidden;
            picture.raycastTarget = false;

            RectTransform rt = picture.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(SlotPadding, SlotPadding);
            rt.offsetMax = new Vector2(-SlotPadding, -SlotPadding);
            return picture;
        }

        /// <summary>An uncollected slot shows the silhouette only — the picture waits behind alpha 0.</summary>
        private static readonly Color SlotPictureHidden = new Color(1f, 1f, 1f, 0f);

        private void BuildHud()
        {
            for (int i = 0; i < _level.DetailCount; i++)
            {
                Rect box = LevelCatalog.SlotRectOf(_level, i);
                Image slot = Ui.Rounded(HudLayer, "Slot" + (i + 1),
                    box.center.x, box.center.y, box.width, box.height,
                    new Color(1f, 1f, 1f, 0.82f), new Color(0.35f, 0.35f, 0.35f, 0.9f), 3f, 10);
                _slots.Add(slot);

                _slotFills.Add(BuildSlotPicture(slot, "SlotArt" + (i + 1),
                    ArtLibrary.IconOf(_level.Details[i]), box.width - SlotPadding * 2f,
                    _level.Details[i].SilhouetteRadius));
            }

            // Positions come from the level, not from LevelOneData: the stand keeps the SCREENS base,
            // a real level moves the widget off whatever the art drop painted there (founder 2026-07-31).
            //
            // The sun-dial and its caption stood here until 2026-08-07. They are gone with the timer:
            // there is no clock to draw, and a dial that reports nothing is worse than an empty corner
            // because it still claims a corner of every plate (see LevelRules).
            Vector2 crank = _level.CrankIndicatorCentre;

            _crankHalo = Ui.Circle(HudLayer, "CrankHalo", crank.x, crank.y,
                LevelOneData.CrankHaloRadius, LevelOneData.CrankHaloOk, Color.clear);
            _crankHalo.gameObject.SetActive(false);

            CrankDial = Ui.Circle(HudLayer, "CrankDial", crank.x, crank.y,
                LevelOneData.CrankIndicatorRadius,
                Color.white, new Color(0.4f, 0.4f, 0.4f), 4f);

            CrankArc = Ui.Ring(HudLayer, "CrankArc", crank.x, crank.y,
                LevelOneData.CrankIndicatorRadius + 14f,
                LevelOneData.VesselStroke);
            CrankArc.fillAmount = 0.35f;

            var needle = Ui.BoxCentred(CrankDial.transform, "CrankNeedle", 0f, 0f, 8f,
                LevelOneData.CrankIndicatorRadius, new Color(0.33f, 0.33f, 0.33f));
            CrankNeedle = needle.rectTransform;
            CrankNeedle.anchorMin = new Vector2(0.5f, 0.5f);
            CrankNeedle.anchorMax = new Vector2(0.5f, 0.5f);
            CrankNeedle.pivot = new Vector2(0.5f, 0f);
            CrankNeedle.anchoredPosition = Vector2.zero;
        }

        private void BuildHint()
        {
            Hint = new ButtonHint(MessageLayer);
            SecondHint = new ButtonHint(MessageLayer, "SecondHint");
        }

        /// <summary>
        /// The finished outcome screens of the drop (S4 «level-complete», S5 «game-over»), full frame,
        /// above everything the level draws — including the HUD, because a finished screen covers the
        /// frame whole.
        ///
        /// Built empty and hidden: which one is shown is the level's decision, and the ALPHA is what
        /// the retry acts on — «каждый оборот динамо стирает ~10 % штриховки с экрана» (SCREENS S5) is
        /// this image dissolving back into the level the player just lost.
        /// </summary>
        /// <summary>
        /// Where the defeat screen's copy lives, in design px: the three lines «Мысли захватили тебя…»
        /// plus the underline that stands for the retry, with a margin around them.
        ///
        /// Measured off the render itself (`арт/экраны/game-over-screen.png`, 3840×2160 → ×0.5): the lit
        /// pixels of the text run x 480…1448, y 440…660. The plate is that box grown by ~60 px, so no
        /// glyph and no part of the glow sits on its edge.
        /// </summary>
        public static readonly Rect DefeatTextPlate = new Rect(404f, 385f, 1120f, 330f);

        /// <summary>
        /// How dark the plate is at full strength. The text is neon on near-black in the render, so the
        /// plate only has to give it back the near-black — anything lighter and the marker hatching of
        /// the level underneath comes back through the letters.
        /// </summary>
        private const float DefeatPlateOpacity = 0.85f;

        /// <summary>
        /// Below this much of the screen left, the plate fades out with it.
        ///
        /// The plate cannot simply follow the screen's own alpha: at the retry's midpoint that would
        /// leave it at half strength, which is where the hatching wins (the frame the design skeptic
        /// returned on 2026-08-08 measured 49/255 of contrast between the copy and what was behind it).
        /// It also cannot stay: the last turn of the handle clears the screen, and a black rectangle
        /// outliving the picture it was holding up is a bug of its own. So it holds full strength for
        /// the part of the wipe where there is still text to read, and leaves over the last quarter.
        /// </summary>
        private const float DefeatPlateFadeTail = 0.25f;

        private void BuildOutcome()
        {
            // Built BEFORE the screen itself, so it draws under it: the plate is scaffolding for the
            // render's own text, not something laid over the designer's picture.
            _outcomePlate = Ui.Rounded(OutcomeLayer, "OutcomeTextPlate",
                DefeatTextPlate.center.x, DefeatTextPlate.center.y,
                DefeatTextPlate.width, DefeatTextPlate.height,
                new Color(0f, 0f, 0f, DefeatPlateOpacity), Color.clear, 0f, 28);
            _outcomePlate.raycastTarget = false;
            _outcomePlate.gameObject.SetActive(false);

            _outcome = Ui.NewImage(OutcomeLayer, "OutcomeScreen");
            _outcome.color = new Color(1f, 1f, 1f, 0f);
            _outcome.raycastTarget = false;
            Ui.Place(_outcome.rectTransform, 960f, 540f, 1920f, 1080f);
            _outcome.gameObject.SetActive(false);
        }

        /// <summary>Put a drawn screen over the level, fully opaque.</summary>
        /// <param name="withTextPlate">
        /// Only the defeat screen asks for it, and only because it is the one screen that DISSOLVES:
        /// while it does, the level's thoughts show through its own copy. The victory screen is up at
        /// full opacity for its two seconds and needs nothing under it.
        /// </param>
        public void ShowOutcomeScreen(string artKey, bool withTextPlate = false)
        {
            _outcome.sprite = ArtLibrary.Get(artKey);
            // A missing render must not be an invisible «screen»: black is what the flow means here,
            // and it is loud enough that the design gate cannot miss it.
            _outcome.color = _outcome.sprite != null ? Color.white : Color.black;
            _outcome.gameObject.SetActive(true);

            if (_outcomePlate != null) _outcomePlate.gameObject.SetActive(withTextPlate);
            SyncPlateToScreen();
        }

        /// <summary>How much of the outcome screen is still up, 1 = whole, 0 = wiped away.</summary>
        public float OutcomeAlpha
        {
            get { return _outcome != null && _outcome.gameObject.activeSelf ? _outcome.color.a : 0f; }
            set
            {
                if (_outcome == null) return;
                Color c = _outcome.color;
                c.a = Mathf.Clamp01(value);
                _outcome.color = c;
                SyncPlateToScreen();
            }
        }

        private void SyncPlateToScreen()
        {
            if (_outcomePlate == null || !_outcomePlate.gameObject.activeSelf) return;
            float left = _outcome != null ? _outcome.color.a : 0f;
            _outcomePlate.color = new Color(0f, 0f, 0f,
                DefeatPlateOpacity * Mathf.Clamp01(left / DefeatPlateFadeTail));
        }

        public void HideOutcomeScreen()
        {
            if (_outcome == null) return;
            _outcome.gameObject.SetActive(false);
            _outcome.sprite = null;
            if (_outcomePlate != null) _outcomePlate.gameObject.SetActive(false);
        }

        // ---- state -------------------------------------------------------------------------------

        public void SetCrank(float totalDegrees, bool spinning, bool alarm = false)
        {
            CrankNeedle.localRotation = Quaternion.Euler(0f, 0f, -totalDegrees);
            CrankNeedle.GetComponent<Image>().color = spinning
                ? LevelOneData.VesselStroke
                : new Color(0.33f, 0.33f, 0.33f);

            if (alarm) CrankDial.color = LevelOneData.Alarm;
            else CrankDial.color = spinning ? LevelOneData.VesselStroke : new Color(0.4f, 0.4f, 0.4f);

            _crankHalo.gameObject.SetActive(alarm || spinning);
            _crankHalo.color = alarm ? LevelOneData.CrankHaloAlarm : LevelOneData.CrankHaloOk;
        }

        public void SetHudVisible(bool visible) => HudLayer.gameObject.SetActive(visible);

        /// <summary>
        /// Defeat: «цвета гаснут» (mock 17). Tinting the thoughts alone moved the frame's saturation
        /// by 0.08 — invisible. The plate is the picture, so the plate is what has to go out: the
        /// background, the details still lying around and the vessel all take the same cold tint, and
        /// the thoughts take the stronger one that pulls the turquoise into the mock's grey band.
        /// </summary>
        public void SetDesaturated(bool desaturated)
        {
            _desaturated = desaturated;

            Color tint = desaturated ? DefeatSceneTint : Color.white;
            if (_background != null && _background.sprite != null)
            {
                Color plate = tint;
                plate.a = _background.color.a;
                _background.color = plate;
            }

            for (int i = 0; i < _detailImages.Count; i++)
                if (_detailImages[i].sprite != null) _detailImages[i].color = tint;

            if (_vessel != null && _vessel.sprite != null) _vessel.color = tint;
            for (int i = 0; i < _vesselContents.Count; i++)
                if (_vesselContents[i] != null && _vesselContents[i].sprite != null)
                    _vesselContents[i].color = tint;
        }

        /// <summary>
        /// The defeat tint of the LOCATION. Multiplication cannot pull a photograph to grey, but it can
        /// take the warmth out of it and drop it into dusk, which is what «цвета гаснут» looks like on
        /// a plate — and it is measurable: the frame's mean saturation falls instead of twitching.
        /// </summary>
        public static readonly Color DefeatSceneTint = new Color(0.58f, 0.63f, 0.68f);

        public void SetThoughtsAlpha(float alpha)
        {
            if (_thoughtsGroup != null) _thoughtsGroup.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>Intro (2 с): every detail pulses at once — «обзор» (SCREENS S3 state 1).</summary>
        public void SetIntroPulse(bool on) => _introPulse = on;

        public void SetPeak(bool peak)
        {
            Color c = LevelOneData.Dim;
            c.a = peak ? LevelOneData.DimAlpha : 0f;
            PeakVignette.color = c;

            Color edges = PeakEdges.color;
            edges.a = peak ? PeakEdgeAlpha : 0f;
            PeakEdges.color = edges;
        }

        /// <summary>
        /// Strength of the edge darkening at the peak. The veil keeps the mock's own numbers; this is
        /// the second half of the same sentence in SCREENS («+ затемнение краёв экрана»), and it is
        /// what a plate that is already dark in the middle can actually show.
        /// </summary>
        public const float PeakEdgeAlpha = 0.75f;

        /// <summary>MECHANICS §4 [toggle]: the vessel layer swaps sides with the thought layer.</summary>
        public void ApplyCoverToggle(bool force = false)
        {
            bool cover = _defeatOrder || Tuning.TuningConfig.ThoughtsCoverVessel;
            if (!force && cover == _coverApplied) return;
            _coverApplied = cover;

            ApplyLayerOrder(cover);
            // On the defeat screen the vessel is not being played with — it is only what the wallpaper
            // closes over — so it keeps its own colour instead of the toggle's half-fade.
            SetVesselAlpha(cover && !_defeatOrder ? 0.5f : 1f);
        }

        /// <summary>
        /// Defeat: the thoughts go ON TOP of the vessel whatever the toggle says.
        ///
        /// SCREENS S5 says the screen is closed over ЦЕЛИКОМ, and the wallpaper cannot honour that
        /// while the briefcase and the detail on its thread sit above it — the loss frame came out
        /// with the vessel and the dropped detail still bright in the middle of it. The reason the
        /// vessel normally stays visible through the blobs is that the player is aiming at it; on the
        /// defeat screen there is nothing left to aim at, only the crank that wipes the screen clear.
        /// </summary>
        public void StageDefeat()
        {
            _defeatOrder = true;
            ApplyCoverToggle(true);
        }

        /// <summary>Back to the toggle's own order — the level restarts after the screen is wiped.</summary>
        public void ClearDefeatStaging()
        {
            if (!_defeatOrder) return;
            _defeatOrder = false;
            ApplyCoverToggle(true);
        }

        /// <summary>
        /// Lay the whole stack out from the toggle, instead of nudging one layer past another.
        ///
        /// Nudging is what broke it: moving the vessel from below the thoughts to «thoughtsIndex + 1»
        /// forgot that pulling it out of the list shifts everything above it down by one, so switching
        /// the toggle on and back off left the vessel ABOVE the peak veil — the veil of the chaos peak
        /// stopped falling on the vessel and on the detail being dragged. An order this short is cheaper
        /// to state whole than to patch, and stating it whole is also what makes it testable.
        /// </summary>
        private void ApplyLayerOrder(bool cover)
        {
            RectTransform[] order = cover
                ? new[] { SceneLayer, DetailsLayer, ThreadLayer, VesselLayer, ThoughtsLayer, PeakLayer, HudLayer, MessageLayer, OutcomeLayer }
                : new[] { SceneLayer, DetailsLayer, ThreadLayer, ThoughtsLayer, VesselLayer, PeakLayer, HudLayer, MessageLayer, OutcomeLayer };

            int first = int.MaxValue;
            for (int i = 0; i < order.Length; i++)
                first = Mathf.Min(first, order[i].GetSiblingIndex());

            for (int i = 0; i < order.Length; i++)
                order[i].SetSiblingIndex(first + i);
        }

        /// <summary>
        /// Fade the whole vessel layer, the dragged detail with it.
        ///
        /// Through a CanvasGroup, not by walking the graphics and overwriting their alphas: the level-3
        /// fill indicator is DESIGNED translucent (a 0.6 border over a rising 0.55 bar), and setting
        /// every child to 1.0 turned it into an opaque white card sitting on the passenger's bag.
        /// </summary>
        private void SetVesselAlpha(float alpha)
        {
            if (_vesselGroup != null) _vesselGroup.alpha = alpha;
        }

        /// <summary>How opaque the vessel layer is drawn — the cover toggle's own readout.</summary>
        public float VesselAlpha
        {
            get { return _vesselGroup != null ? _vesselGroup.alpha : 1f; }
        }

        // ---- per-frame ---------------------------------------------------------------------------

        public void TickPulse(float deltaTime, IReadOnlyList<bool> collected, int noticedIndex)
        {
            _pulse += deltaTime;

            // SCREENS: масштаб 1.0 → 1.12, цикл 1.6 с. During the intro every detail pulses together;
            // afterwards the one being dragged holds still so the ring around it stays readable.
            float k = 1f + 0.12f * 0.5f * (1f + Mathf.Sin(_pulse * Mathf.PI * 2f / 1.6f));
            for (int i = 0; i < _detailImages.Count; i++)
            {
                bool isCollected = collected != null && i < collected.Count && collected[i];
                bool pulsing = !isCollected && (_introPulse || i != noticedIndex);
                _detailImages[i].rectTransform.localScale = pulsing ? new Vector3(k, k, 1f) : Vector3.one;
            }

            if (_vesselBounce > 0f)
            {
                _vesselBounce = Mathf.Max(0f, _vesselBounce - deltaTime);
                float b = 1f + 0.1f * Mathf.Sin(Mathf.Clamp01(_vesselBounce / 0.3f) * Mathf.PI);
                VesselRect.localScale = new Vector3(b, b, 1f);
            }
            else if (!_victoryStaged)
            {
                VesselRect.localScale = Vector3.one;
            }
        }

        public void SetDetailProgress(int index, float progress01, bool spinning, bool active, bool slipped)
        {
            if (index < 0 || index >= _detailImages.Count) return;

            ArtDetail spec = _level.Details[index];
            Vector2 position = Vector2.Lerp(spec.Home, _level.VesselCentre, Mathf.Clamp01(progress01));
            Ui.MoveTo(_detailImages[index].rectTransform, position);

            Image ring = _detailRings[index];
            ring.gameObject.SetActive(active && RingHasSomethingToShow(progress01, slipped) &&
                                     !FullyUnderAThought(HintPlacement.Centred(position, spec.Size)));
            SetDragged(active ? index : -1);
            if (!active) return;

            // …and it keeps circling the ink as the detail travels: the sprite moves, its centroid
            // moves with it, so the offset between the two is constant.
            Ui.MoveTo(ring.rectTransform, position + LevelCatalog.AnchorOffsetOf(spec));

            // A slip is a CLOSED red ring (mock 14) — a red arc would read as "red progress 60 %".
            ring.fillAmount = slipped ? 1f : Mathf.Clamp01(progress01);
            ring.color = spinning && !slipped ? LevelOneData.VesselStroke : LevelOneData.Alarm;
        }

        /// <summary>
        /// Fill below which the ring's arc is a hair rather than an arc — half a degree of 360.
        /// </summary>
        public const float RingMinimumFill = 1f / 720f;

        /// <summary>
        /// Has this ring anything to say? A ring at fill 0 is not «прогресс 0 %», it is a 2×14 px tick
        /// of the alarm colour standing in the sky.
        ///
        /// That is exactly what frames 27 and 29 shipped (design gate, 2026-08-07): the gull had been
        /// noticed, the teaching cat had landed on it and the dynamo had wound down, so the ring was
        /// drawn at fill 0 in <see cref="LevelOneData.Alarm"/> — a red hair on the plate, over a detail
        /// that had lost nothing because it had collected nothing. A slip is the one zero-progress state
        /// that still has something to show, and it draws a CLOSED ring rather than an arc.
        /// </summary>
        public static bool RingHasSomethingToShow(float progress01, bool slipped) =>
            slipped || progress01 > RingMinimumFill;

        /// <summary>
        /// Is the detail buried under a thought? Then its ring is drawing on top of the thing that hid
        /// it — «кольцо не рисуется, пока деталь полностью закрыта мыслью» (founder, 2026-08-07).
        ///
        /// FULLY covered, not merely touched: the field's own <see cref="ThoughtField.IsCovered"/> asks
        /// about a point because it is deciding whether the gaze may still reach a detail, and borrowing
        /// that here would take the ring away mid-haul the moment a blob drifted across the centroid —
        /// which is the one moment the crank's only feedback has to be on screen.
        /// </summary>
        private bool FullyUnderAThought(Rect detail)
        {
            if (_liveThoughts == null) return false;
            for (int i = 0; i < _liveThoughts.Count; i++)
            {
                Thought thought = _liveThoughts[i];
                if (thought == null) continue;

                Rect over = thought.Rect;
                if (over.xMin <= detail.xMin && over.yMin <= detail.yMin &&
                    over.xMax >= detail.xMax && over.yMax >= detail.yMax) return true;
            }
            return false;
        }

        private void SetDragged(int index)
        {
            if (index == _draggedIndex) return;

            if (_draggedIndex >= 0 && _draggedIndex < _detailImages.Count)
            {
                _detailImages[_draggedIndex].transform.SetParent(DetailsLayer, false);
                _detailRings[_draggedIndex].transform.SetParent(DetailsLayer, false);
            }

            _draggedIndex = index;
            if (_draggedIndex < 0 || _draggedIndex >= _detailImages.Count) return;

            _detailImages[_draggedIndex].transform.SetParent(VesselLayer, false);
            _detailRings[_draggedIndex].transform.SetParent(VesselLayer, false);
            SetVesselAlpha(_coverApplied && !_defeatOrder ? 0.5f : 1f);
        }

        public void CollectDetail(int index)
        {
            if (index < 0 || index >= _detailImages.Count) return;

            ArtDetail spec = _level.Details[index];
            SetDragged(-1);
            _detailImages[index].gameObject.SetActive(false);
            _detailRings[index].gameObject.SetActive(false);
            _vesselBounce = 0.3f;

            if (index < _slotFills.Count) _slotFills[index].color = Color.white;

            // The haul shows the same icon the slot does: a cell is a small square, and a detail that
            // needs a fragment to be readable in a 66 px slot needs it here too — the vine arrived in
            // the briefcase as a thread otherwise.
            Image copy = Ui.NewImage(_vessel.transform, "InVessel_" + spec.Name);
            copy.sprite = ArtLibrary.IconOf(spec);
            copy.preserveAspect = true;
            copy.color = copy.sprite != null ? Color.white : Color.magenta;
            _vesselContents.Add(copy);
            _vesselContentSizes.Add(LevelCatalog.IconSizeOf(spec));

            LayoutVesselContents();
            UpdateVesselFill();
            SetVesselAlpha(_coverApplied ? 0.5f : 1f);
        }

        /// <summary>
        /// Keep the haul INSIDE the vessel, whatever its number — a grid in the vessel's own inner box
        /// (<see cref="VesselHaul"/>), anchored to the vessel itself so the victory tableau's ×2 lift
        /// carries it along. The row this replaces ran wider than the briefcase and pasted a detail
        /// bigger than it was in the scene over the lock.
        /// </summary>
        private void LayoutVesselContents()
        {
            var rects = new List<RectTransform>(_vesselContents.Count);
            for (int i = 0; i < _vesselContents.Count; i++) rects.Add(_vesselContents[i].rectTransform);
            VesselHaul.Layout(rects, _level.VesselSize, LevelCatalog.SolidOf(_level), _vesselContentSizes);
        }

        /// <summary>Level 3's overlay indicator: the bar under the baked bag fills with the haul.</summary>
        private void UpdateVesselFill()
        {
            if (_vesselFillLevel == null) return;

            float share = _level.DetailCount <= 0
                ? 0f
                : Mathf.Clamp01(_vesselContents.Count / (float)_level.DetailCount);
            RectTransform track = _vesselFillTrack.rectTransform;
            _vesselFillLevel.rectTransform.sizeDelta =
                new Vector2(Mathf.Max(0f, (track.rect.width - 6f) * share), -6f);
        }

        public void ResetCollected()
        {
            SetDragged(-1);
            ClearVictoryStaging();

            for (int i = 0; i < _vesselContents.Count; i++)
                if (_vesselContents[i] != null) Object.Destroy(_vesselContents[i].gameObject);
            _vesselContents.Clear();
            _vesselContentSizes.Clear();
            UpdateVesselFill();

            for (int i = 0; i < _slotFills.Count; i++)
                _slotFills[i].color = SlotPictureHidden;

            for (int i = 0; i < _detailImages.Count; i++)
            {
                _detailImages[i].gameObject.SetActive(true);
                Ui.MoveTo(_detailImages[i].rectTransform, _level.Details[i].Home);
                _detailRings[i].gameObject.SetActive(false);
            }
        }

        public void SetThread(Vector2 from, Vector2 to, bool visible)
        {
            Thread.gameObject.SetActive(visible);
            if (visible) Ui.StretchLine(Thread.rectTransform, from, to);
        }

        public void SetGaze(Vector2 position, float dwell01, bool visible)
        {
            Gaze.gameObject.SetActive(visible);
            GazeArc.gameObject.SetActive(visible && dwell01 > 0f);
            if (!visible) return;

            // Radius, glow and ring width are sliders, so they are re-read here rather than baked at
            // build time — the founder moves them while the level she is judging is on screen.
            ApplyGazeSize();

            Ui.MoveTo(Gaze.rectTransform, position);
            Ui.MoveTo(GazeArc.rectTransform, position);
            GazeArc.fillAmount = Mathf.Clamp01(dwell01);
        }

        public void SyncThoughts(IReadOnlyList<Thought> thoughts, float deltaTime)
        {
            _liveThoughts = thoughts;
            ApplyCoverToggle();
            StartPopsForWhateverJustBurst(thoughts);

            while (_thoughtPool.Count < thoughts.Count)
                _thoughtPool.Add(new ArtThoughtView(ThoughtsLayer));

            for (int i = 0; i < _thoughtPool.Count; i++)
            {
                bool used = i < thoughts.Count;
                _thoughtPool[i].SetActive(used);
                if (used) _thoughtPool[i].Bind(thoughts[i], deltaTime, _desaturated);
            }

            TickPops(deltaTime);

            PeakVignette.rectTransform.SetAsLastSibling();
            PeakEdges.rectTransform.SetAsLastSibling();
        }

        // ---- «последний пипс — мысль лопается» ---------------------------------------------------

        /// <summary>
        /// Whatever left the field since the last frame BECAUSE it ran out of pips starts its burst
        /// (<see cref="ThoughtPop"/>).
        ///
        /// The test is «gone from the list AND spent», not «gone from the list»: thoughts also leave
        /// through <see cref="ThoughtField.Clear"/> at the victory dissolve and through
        /// <see cref="ThoughtField.RemoveOldest"/> when the handle wipes the defeat screen, and neither
        /// of those is a thought the player burst — they must not pop. Wallpaper blobs are excluded for
        /// the same reason the pips skip them: on the defeat screen they are not a fight, they are the
        /// picture the retry rubs off.
        /// </summary>
        private void StartPopsForWhateverJustBurst(IReadOnlyList<Thought> thoughts)
        {
            for (int i = 0; i < _drawnLastFrame.Count; i++)
            {
                Thought gone = _drawnLastFrame[i];
                if (gone.Wallpaper || !gone.IsPopped || Holds(thoughts, gone)) continue;
                BeginPop(gone);
            }

            _drawnLastFrame.Clear();
            for (int i = 0; i < thoughts.Count; i++) _drawnLastFrame.Add(thoughts[i]);
        }

        private static bool Holds(IReadOnlyList<Thought> thoughts, Thought one)
        {
            for (int i = 0; i < thoughts.Count; i++)
                if (ReferenceEquals(thoughts[i], one)) return true;
            return false;
        }

        private void BeginPop(Thought thought)
        {
            int slot = TakePopSlot();
            _popPool[slot].SetActive(true);
            _popPool[slot].Bind(thought, 0f, _desaturated);
            _popPool[slot].SetScale(ThoughtPop.StartScale);
            _pops.Add(new ThoughtPop(thought, slot));
        }

        /// <summary>
        /// A burst view, reused. Popping is the commonest event in the game — a fresh
        /// <see cref="ArtThoughtView"/> is two images, a material and a row of twenty-four pip discs,
        /// and building that set every time a thought dies would litter the peak.
        /// </summary>
        private int TakePopSlot()
        {
            for (int i = 0; i < _popSlotBusy.Count; i++)
            {
                if (_popSlotBusy[i]) continue;
                _popSlotBusy[i] = true;
                return i;
            }

            _popPool.Add(new ArtThoughtView(ThoughtsLayer));
            _popSlotBusy.Add(true);
            return _popPool.Count - 1;
        }

        /// <summary>
        /// Advance the bursts. They are re-bound rather than frozen so the flinch of the killing hit
        /// («мысль вздрагивает, ±6 px, 80 мс») plays out on top of the shrink instead of being frozen
        /// into a permanent 6 px offset; the thought itself no longer moves, it is out of the field.
        /// </summary>
        private void TickPops(float deltaTime)
        {
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                ThoughtPop pop = _pops[i];
                pop.Advance(deltaTime);
                ArtThoughtView view = _popPool[pop.Slot];

                if (pop.Finished)
                {
                    // 200 ms are up: the thought is off the screen, and its view goes back in the box
                    // at its own size, ready for the next one.
                    view.SetActive(false);
                    view.SetScale(1f);
                    _popSlotBusy[pop.Slot] = false;
                    _pops.RemoveAt(i);
                    continue;
                }

                view.Bind(pop.Thought, deltaTime, _desaturated);
                view.SetScale(pop.Scale);
            }
        }

        /// <summary>Put a drawn button of the tutorial beside <paramref name="target"/> and aim at it.</summary>
        public void ShowHint(string buttonKey, HintTone tone, Vector2 centre, Vector2 target,
            float standoff = 50f)
        {
            Hint.Show(ArtLibrary.Get(buttonKey), tone, centre, target, standoff);
        }

        /// <summary>The second button of the beat (ТАЩИ), beside the detail that is moving.</summary>
        public void ShowSecondHint(string buttonKey, HintTone tone, Vector2 centre, Vector2 target,
            float standoff = 50f)
        {
            SecondHint.Show(ArtLibrary.Get(buttonKey), tone, centre, target, standoff);
        }

        /// <summary>Walk the second button to a new spot and re-aim it — «рядом с едущей деталью».</summary>
        public void MoveSecondHint(Vector2 centre, Vector2 target)
        {
            SecondHint.MoveTo(centre);
            SecondHint.PointAt(target);
        }

        public void HideSecondHint() => SecondHint.Hide();

        /// <summary>The beat the drop has no button for: an arrow and nothing else.</summary>
        public void ShowArrowHint(HintTone tone, Vector2 from, Vector2 target)
        {
            Hint.ShowArrowOnly(tone, from, target);
        }

        public void HideHint() => Hint.Hide();

        /// <summary>
        /// The reward beat of S4 (kept by the founder's own note on the drop): the vessel slides to the
        /// centre with the whole haul inside it — «единственный момент, где игрок видит добычу» — and
        /// then the drawn <c>level-complete</c> screen comes over it.
        /// </summary>
        public void StageVictory()
        {
            if (_victoryStaged) return;
            _victoryStaged = true;
            _vesselBounce = 0f;

            // …normalised to mock 18's own box (480×300) instead of a flat ×2: the bucket at ×2 is
            // 480×778 and stood on top of «Собрано: …» and of the row of slots below it.
            float scale = LevelCatalog.VictoryScaleOf(_level);
            Ui.MoveTo(VesselRect, new Vector2(960f, 540f));
            VesselRect.localScale = new Vector3(scale, scale, 1f);

            // The location stays behind the tableau, at half strength: the vessel with the haul is the
            // picture, the place is only what it was collected from.
            SetBackgroundAlpha(VictoryBackgroundAlpha);

            // The fill indicator belongs to the bag where it stands, not to the tableau.
            if (_vesselFillTrack != null) _vesselFillTrack.gameObject.SetActive(false);
        }

        private void ClearVictoryStaging()
        {
            if (!_victoryStaged) return;
            _victoryStaged = false;
            Ui.MoveTo(VesselRect, _level.VesselCentre);
            VesselRect.localScale = Vector3.one;
            SetThoughtsAlpha(1f);
            SetBackgroundAlpha(1f);
            if (_vesselFillTrack != null) _vesselFillTrack.gameObject.SetActive(true);
        }

        private void SetBackgroundAlpha(float alpha)
        {
            Color c = _background.color;
            c.a = alpha;
            _background.color = c;
        }

        // ---- луч-подсветка деталей (SCREENS «Детали в сцене») --------------------------------------

        /// <summary>
        /// Slant of the band inside a sprite's own UV — «мягкая НАКЛОННАЯ полоса света».
        ///
        /// In UV rather than in degrees on purpose: a fixed angle on a 484 px contrail and on a 40 px
        /// paperclip is two different pictures, while a quarter of the sprite's own width is the same
        /// lean on both.
        /// </summary>
        private const float SweepSlant = 0.25f;

        /// <summary>
        /// Give a detail its own material instance of <c>Meditation/DetailSweep</c>.
        ///
        /// Per instance, like <see cref="ArtThoughtView"/>'s backing and for the same reason: a UGUI
        /// <c>Image</c> carries no per-instance property block, and the band's position has to be
        /// expressed in EACH sprite's own UV (the details run from a 40 px paperclip to a 484 px
        /// contrail — one shared number would be half of the first and a twentieth of the second).
        /// The instances are destroyed with the level.
        /// </summary>
        private void GiveItsOwnSweepMaterial(Image image)
        {
            // Resources, not Shader.Find: the shader ships inside the game's own Resources folder
            // (like the thought shaders), and Shader.Find only sees what a build decided to include.
            var shader = Resources.Load<Shader>(LevelCatalog.ArtRoot + "shaders/detail-sweep");
            if (shader == null) return;   // no shader = no sweep, never a magenta detail

            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat(SweepStrengthId, 0f);
            material.SetFloat(SweepSlantId, SweepSlant);
            image.material = material;
            _sweepMaterials.Add(material);
        }

        // ---- неон-обводка деталей [toggle] ---------------------------------------------------------

        /// <summary>
        /// Hang a neon rim on a detail (<c>Meditation/DetailOutline</c>), as a CHILD of its Image.
        ///
        /// A child, not a sibling, and that is the whole design: the rim then inherits the detail's
        /// position as it travels the thread, its pulse scale, its reparenting into the vessel layer
        /// while it is being dragged, and its disappearance when it is collected. A sibling would have
        /// to be walked through all four by hand, and the ring above it is the standing proof of how
        /// that goes — <see cref="SetDetailProgress"/> exists mostly to keep one extra object in step
        /// with one sprite. In UGUI a child draws ABOVE its parent, which is exactly why the shader
        /// subtracts the sprite's own alpha: what is left is the rim outside the ink.
        /// </summary>
        private void GiveItsOwnNeonOutline(Image detail, ArtDetail spec)
        {
            var shader = Resources.Load<Shader>(LevelCatalog.ArtRoot + "shaders/detail-outline");
            if (shader == null)
            {
                _detailOutlines.Add(null);
                _outlineMaterials.Add(null);
                return;
            }

            Image rim = Ui.NewImage(detail.transform, "Neon_" + spec.Name);
            rim.sprite = detail.sprite;
            // NOT preserveAspect: the quad is deliberately not the sprite's own rectangle any more (it
            // is padded, see ApplyNeonOutline), and letterboxing it would slide the drawing inside the
            // quad out from under the mapping the shader undoes.
            rim.preserveAspect = false;
            rim.raycastTarget = false;
            RectTransform rt = rim.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor(NeonColourId, NeonAim);
            material.SetVector(OutlineUvRectId, UvRectOf(detail.sprite));
            rim.material = material;
            rim.gameObject.SetActive(false);   // [toggle] ships OFF

            _detailOutlines.Add(rim);
            _outlineMaterials.Add(material);
        }

        /// <summary>
        /// Show/hide the rims and push the panel's numbers into them. Called every frame the level
        /// ticks, because both the toggle and the two sliders act at once.
        ///
        /// The dilation goes in as UV of each sprite's own rectangle — same reason as the light band
        /// and the thought halo: one texel radius would be a sixth of the office paperclip and a
        /// eightieth of the plane's contrail.
        /// </summary>
        /// <param name="allowed">
        /// False on the outcome screens: the win dissolves into a tableau and the loss into a drawn
        /// screen, and neither of them is a moment when the game is still pointing at details.
        /// </param>
        public void ApplyNeonOutline(bool allowed = true)
        {
            bool on = allowed && Tuning.TuningConfig.DetailNeonOutline;

            for (int i = 0; i < _detailOutlines.Count; i++)
            {
                Image rim = _detailOutlines[i];
                if (rim == null) continue;

                if (rim.gameObject.activeSelf != on) rim.gameObject.SetActive(on);
                if (!on) continue;

                Material material = _outlineMaterials[i];
                if (material == null) continue;

                ArtDetail spec = _level.Details[i];
                Vector2 box = spec.Size;
                float px = RimRadiusPx(spec, Mathf.Max(0f, Tuning.TuningConfig.DetailOutlinePx));

                // The quad is grown by the radius (plus a pixel of air, so the outermost tap is not the
                // very last row of the quad) and the shader maps the drawing back inside it.
                float pad = px + RimPadAirPx;
                RectTransform rt = rim.rectTransform;
                rt.offsetMin = new Vector2(-pad, -pad);
                rt.offsetMax = new Vector2(pad, pad);

                material.SetVector(OutlineSpreadId, new Vector4(
                    px / Mathf.Max(1f, box.x), px / Mathf.Max(1f, box.y), 0f, 0f));
                material.SetVector(OutlinePadId, new Vector4(
                    pad / Mathf.Max(1f, box.x + 2f * pad), pad / Mathf.Max(1f, box.y + 2f * pad), 0f, 0f));
                material.SetFloat(OutlineStrengthId, Mathf.Clamp01(Tuning.TuningConfig.DetailOutlineStrength));
            }
        }

        /// <summary>
        /// How wide the rim may be drawn around THIS detail, design px — the panel's number, capped by
        /// the detail's own drawing.
        ///
        /// The rim is a dilation, and a dilation is only an outline while it is narrower than the thing
        /// it goes around: a rim of radius r adds 2r to the width of every stroke, so at the shipped
        /// 6 px the office paperclip — a 10.7 px wire once it is drawn at 40×56 — grew into a solid
        /// turquoise blob, and the librarian's glasses lost both lenses and their bridge (design gate,
        /// 2026-08-08). The cap is therefore a quarter of the stroke's own thickness, which is the same
        /// sentence as «the rim may add at most half of the stroke's width to it».
        ///
        /// The thickness comes from <see cref="ArtLibrary.StrokeThicknessOf"/> in the PNG's pixels and is
        /// brought down to the size the detail is DRAWN at — the paperclip's canvas is 79×113 and its
        /// place on the plate is 40×56, and a cap taken in canvas pixels would be twice as generous as
        /// the picture allows. An unmeasured sprite keeps the panel's number, so this can only ever take
        /// the rim in, never make the slider a lie in the other direction.
        /// </summary>
        public static float RimRadiusPx(ArtDetail spec, float wanted)
        {
            if (wanted <= MinRimPx) return wanted;

            float canvasThickness = ArtLibrary.StrokeThicknessOf(spec.Sprite);
            if (float.IsInfinity(canvasThickness) || canvasThickness > 1e6f) return wanted;

            Sprite sprite = ArtLibrary.Get(spec.Sprite);
            if (sprite == null) return wanted;

            Vector2 canvas = sprite.rect.size;
            if (canvas.x < 1f || canvas.y < 1f) return wanted;

            float scale = Mathf.Min(spec.Size.x / canvas.x, spec.Size.y / canvas.y);
            return Mathf.Clamp(canvasThickness * scale * RimShareOfStroke, MinRimPx, wanted);
        }

        /// <summary>A rim adds twice its radius to a stroke; a quarter of the stroke is half its width.</summary>
        public const float RimShareOfStroke = 0.25f;

        /// <summary>Below this a rim is not a rim any more, so the cap stops taking it in.</summary>
        public const float MinRimPx = 1.5f;

        /// <summary>Air past the outermost tap, so the rim's last row is not the quad's last row.</summary>
        private const float RimPadAirPx = 2f;

        /// <summary>
        /// The patch of texture an <c>Image</c> stretches across its quad, as (min, size) in UV.
        ///
        /// Needed because the outline shader has to be able to answer «outside the drawing» with zero,
        /// and «outside» is a statement about the SPRITE, not about the texture: a sprite that is a
        /// region of a bigger texture would otherwise have its neighbours dilated into its rim.
        /// </summary>
        private static Vector4 UvRectOf(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return new Vector4(0f, 0f, 1f, 1f);

            Rect rect = sprite.textureRect;
            float w = Mathf.Max(1, sprite.texture.width);
            float h = Mathf.Max(1, sprite.texture.height);
            return new Vector4(rect.x / w, rect.y / h, rect.width / w, rect.height / h);
        }

        /// <summary>The rims, in catalogue order — null where the shader is missing. The suite reads these.</summary>
        public IReadOnlyList<Image> DetailOutlines => _detailOutlines;

        private static readonly int OutlineSpreadId = Shader.PropertyToID("_SpreadUV");
        private static readonly int OutlinePadId = Shader.PropertyToID("_PadUV");
        private static readonly int OutlineUvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int OutlineStrengthId = Shader.PropertyToID("_OutlineStrength");

        private static readonly int SweepCentreId = Shader.PropertyToID("_SweepU");
        private static readonly int SweepWidthId = Shader.PropertyToID("_SweepWidthU");
        private static readonly int SweepStrengthId = Shader.PropertyToID("_SweepStrength");
        private static readonly int SweepSlantId = Shader.PropertyToID("_SweepSlant");

        /// <summary>
        /// Put the light band on the details, in each one's own UV.
        ///
        /// <paramref name="skipIndex"/> is the detail the player has already noticed — the toggle
        /// «луч: только по незамеченным» — and a collected detail is not drawn at all, so it needs no
        /// special case. The band's design-px centre comes from <see cref="Mechanics.DetailSweep"/>.
        /// </summary>
        public void ApplySweep(bool active, float centreX, float widthPx, float strength, int skipIndex)
        {
            for (int i = 0; i < _detailImages.Count; i++)
            {
                Material material = _detailImages[i].material;
                if (material == null || !_sweepMaterials.Contains(material)) continue;

                if (!active || i == skipIndex)
                {
                    material.SetFloat(SweepStrengthId, 0f);
                    continue;
                }

                Rect box = LevelCatalog.RectOf(_level.Details[i]);
                float width = Mathf.Max(1f, box.width);
                material.SetFloat(SweepCentreId, (centreX - box.xMin) / width);
                material.SetFloat(SweepWidthId, Mathf.Max(0.02f, widthPx * 0.5f / width));
                material.SetFloat(SweepStrengthId, Mathf.Clamp01(strength));
            }
        }

        /// <summary>Tear the whole level's UI down (the flow rebuilds it for the next level).</summary>
        public void Dispose()
        {
            for (int i = 0; i < _thoughtPool.Count; i++) _thoughtPool[i].Dispose();
            _thoughtPool.Clear();

            for (int i = 0; i < _popPool.Count; i++) _popPool[i].Dispose();
            _popPool.Clear();
            _popSlotBusy.Clear();
            _pops.Clear();
            _drawnLastFrame.Clear();

            for (int i = 0; i < _sweepMaterials.Count; i++)
                if (_sweepMaterials[i] != null) Object.DestroyImmediate(_sweepMaterials[i]);
            _sweepMaterials.Clear();

            for (int i = 0; i < _outlineMaterials.Count; i++)
                if (_outlineMaterials[i] != null) Object.DestroyImmediate(_outlineMaterials[i]);
            _outlineMaterials.Clear();
            _detailOutlines.Clear();

            if (_gazeMaterial != null) Object.DestroyImmediate(_gazeMaterial);
            _gazeMaterial = null;

            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null;
        }
    }
}
