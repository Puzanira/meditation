using System.Collections.Generic;
using Meditation.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// Screen S3 for a real level: the art drop's background plate, its details on the positions the
    /// designer authored, its vessel, its turquoise thoughts, and the HUD SCREENS.md fixes (N slots,
    /// the sun-dial timer, the crank indicator).
    ///
    /// It is the art twin of <see cref="StageView"/>, not a replacement: the stand keeps its greybox
    /// so the mechanics can still be judged without a picture in the way. Both implement
    /// <see cref="ICollectionView"/>, so the collection loop underneath them is literally the same code.
    ///
    /// Z-order (SCREENS «Зоны», with the walkthrough's refinement): background → details → thread →
    /// thoughts → vessel → peak veil → HUD → messages. The vessel keeps its place above the thoughts
    /// unless the [toggle] «мысли закрывают сосуд и деталь» says otherwise (MECHANICS §4).
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
        private readonly List<Image> _detailImages = new List<Image>();
        private readonly List<Image> _detailRings = new List<Image>();
        private readonly List<Image> _slots = new List<Image>();
        private readonly List<Image> _slotFills = new List<Image>();
        private readonly List<Image> _vesselContents = new List<Image>();
        private readonly List<Vector2> _vesselContentSizes = new List<Vector2>();
        private readonly List<Image> _victorySlotFills = new List<Image>();

        private Image _background;
        private Image _vessel;
        private Image _vesselFillTrack;
        private Image _vesselFillLevel;
        private CanvasGroup _thoughtsGroup;
        private CanvasGroup _vesselGroup;
        private Image _messagePlate;
        private Image _crankHalo;
        private Image _timerPlate;
        private RectTransform _vesselWindow;

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

        public Image Vessel => _vessel;

        /// <summary>
        /// What the tableau moves and scales: the rounded window for a vessel cut out of the plate,
        /// the vessel image itself otherwise. Everything that positions «the vessel» goes through here.
        /// </summary>
        public RectTransform VesselRect => _vesselWindow != null ? _vesselWindow : _vessel.rectTransform;
        public Image Background => _background;

        /// <summary>The white card the outcome lines stand on (mock 17) — measured by the picture tests.</summary>
        public RectTransform MessagePlateRect => _messagePlate.rectTransform;
        public Image Sun { get; private set; }
        public Image SunDial { get; private set; }
        public Text TimerLabel { get; private set; }
        public Image CrankDial { get; private set; }
        public Image CrankArc { get; private set; }
        public RectTransform CrankNeedle { get; private set; }
        public Image Thread { get; private set; }
        public Image Gaze { get; private set; }
        public Image GazeRing { get; private set; }
        public Image GazeArc { get; private set; }
        public Image PeakVignette { get; private set; }

        /// <summary>The peak's darkened edges — the half of it that is visible on a photograph.</summary>
        public Image PeakEdges { get; private set; }

        /// <summary>The row of filled slots under «Собрано: …» (S4, mock 18).</summary>
        public RectTransform VictorySlots { get; private set; }
        public HintCard Hint { get; private set; }
        public Text BigMessage { get; private set; }
        public Text SmallMessage { get; private set; }

        public IReadOnlyList<Image> Slots => _slots;
        public IReadOnlyList<Image> DetailImages => _detailImages;
        public IReadOnlyList<ArtThoughtView> ThoughtViews => _thoughtPool;

        /// <summary>The vessel's fill indicator — present on level 3, where the bag is baked in.</summary>
        public Image VesselFillLevel => _vesselFillLevel;

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
            BuildMessages();

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
                _detailImages.Add(image);

                Image ring = Ui.Ring(DetailsLayer, "Ring_" + spec.Name, spec.Home.x, spec.Home.y,
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
        private static float RingRadius(Vector2 size) =>
            Mathf.Clamp(Mathf.Max(size.x, size.y) * 0.62f, 36f, 96f);

        private void BuildGaze()
        {
            Gaze = Ui.Circle(DetailsLayer, "Gaze", 960f, 540f, GazeSelector.Radius,
                new Color(0.478f, 0.655f, 0.851f, 0.25f), Color.clear);
            GazeRing = Ui.NewImage(DetailsLayer, "GazeRing");
            GazeRing.sprite = UiSprites.DashedRing;
            GazeRing.color = LevelOneData.Gaze;
            Ui.Place(GazeRing.rectTransform, 960f, 540f, GazeSelector.Radius * 2f, GazeSelector.Radius * 2f);
            GazeArc = Ui.Ring(DetailsLayer, "GazeArc", 960f, 540f, GazeSelector.Radius + 14f,
                LevelOneData.VesselStroke);
            GazeArc.fillAmount = 0f;

            Gaze.gameObject.SetActive(false);
            GazeRing.gameObject.SetActive(false);
            GazeArc.gameObject.SetActive(false);
        }

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

            if (!_level.VesselIsBaked) return;

            // Level 3's vessel is painted into the plate, so «наполнение» cannot be shown by putting
            // things inside it — SCREENS asks for an overlay indicator instead. The first one sat ON
            // the bag: a translucent card over 85 % of it, which reported on a bag it was hiding.
            // So the indicator stepped off the bag — a slim bar on the floor UNDER it, in the metro's
            // own warm orange (the seats), filling left to right with the haul.
            float trackWidth = size.x * 0.72f;
            float trackY = centre.y + size.y * 0.5f + FillBarGap;

            _vesselFillTrack = Ui.Rounded(VesselLayer, "VesselFillTrack",
                centre.x, trackY, trackWidth, FillBarHeight,
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

        /// <summary>Height of the baked vessel's fill bar, design px.</summary>
        public const float FillBarHeight = 16f;

        /// <summary>Gap between the bag's lower edge and its bar — the bar never touches the bag.</summary>
        public const float FillBarGap = 22f;

        /// <summary>The metro's own warm orange (the seats), so the indicator belongs to the level.</summary>
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
            Vector2 sun = _level.SunCentre;
            Vector2 crank = _level.CrankIndicatorCentre;

            Sun = Ui.Circle(HudLayer, "Sun", sun.x, sun.y,
                LevelOneData.SunRadius, new Color(1f, 1f, 1f, 0.35f), LevelOneData.SunStroke, 4f);
            SunDial = Ui.NewImage(HudLayer, "SunDial");
            SunDial.sprite = UiSprites.Circle;
            SunDial.type = Image.Type.Filled;
            SunDial.fillMethod = Image.FillMethod.Radial360;
            SunDial.fillOrigin = (int)Image.Origin360.Top;
            // The SPENT sector grows clockwise from 12, so the REMAINING wedge fills anticlockwise.
            SunDial.fillClockwise = false;
            SunDial.color = LevelOneData.SunFill;
            Ui.Place(SunDial.rectTransform, sun.x, sun.y,
                LevelOneData.SunRadius * 2f - 8f, LevelOneData.SunRadius * 2f - 8f);

            // SCREENS «Зоны»: подпись-коробка 300×40 под таймером. A bare number floating over a
            // photograph is not a HUD element — the plate is what makes it one, on any plate.
            Rect caption = LevelCatalog.TimerLabelRectOf(_level);
            _timerPlate = Ui.Rounded(HudLayer, "TimerPlate", caption.center.x, caption.center.y,
                caption.width, caption.height, new Color(0.09f, 0.10f, 0.12f, 0.55f), Color.clear, 0f, 10);
            TimerLabel = Ui.Label(HudLayer, "TimerLabel", "", caption.center.x, caption.center.y,
                caption.width, caption.height, 30, Color.white);

            SetTimerRunning(false);

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

        private void BuildMessages()
        {
            BuildVictorySlots();
            Hint = new HintCard(MessageLayer);

            _messagePlate = Ui.Rounded(MessageLayer, "MessagePlate", 960f, 565f, 1320f, 330f,
                new Color(1f, 1f, 1f, 0.88f), Color.clear, 0f, 20);
            _messagePlate.gameObject.SetActive(false);

            BigMessage = Ui.Label(MessageLayer, "BigMessage", "", 960f, 480f, 1600f, 90f,
                GameTextSizes.Big, new Color(0.23f, 0.23f, 0.21f));
            SmallMessage = Ui.Label(MessageLayer, "SmallMessage", "", 960f, 620f, 1600f, 60f,
                GameTextSizes.Small, new Color(0.4f, 0.4f, 0.4f));
            BigMessage.gameObject.SetActive(false);
            SmallMessage.gameObject.SetActive(false);
        }

        /// <summary>Y of the victory slot row: under «Собрано: …», which stands at y 800.</summary>
        public const float VictorySlotsY = 925f;

        /// <summary>Slot side of the victory row — the HUD's own slot, a touch larger to be read.</summary>
        public const float VictorySlotSize = 78f;

        /// <summary>
        /// S4 asks for two things, and only the line was there: «текст + ряд заполненных слотов»
        /// (SCREENS S4, mock 18). The row is the proof of what the level was about — every slot filled,
        /// with the silhouette of the detail it holds — so it is built with the screen and shown by
        /// <see cref="StageVictory"/> rather than borrowed from the HUD, which the victory hides.
        /// </summary>
        private void BuildVictorySlots()
        {
            int count = _level.DetailCount;
            VictorySlots = Ui.Layer(MessageLayer, "VictorySlots");

            float step = Mathf.Min(VictorySlotSize + 16f, 1600f / Mathf.Max(1, count));
            float side = Mathf.Min(VictorySlotSize, step - 10f);

            for (int i = 0; i < count; i++)
            {
                float x = 960f + (i - (count - 1) * 0.5f) * step;
                Image slot = Ui.Rounded(VictorySlots, "VictorySlot" + (i + 1), x, VictorySlotsY,
                    side, side, new Color(1f, 1f, 1f, 0.9f), new Color(0.35f, 0.35f, 0.35f, 0.9f), 3f, 10);

                _victorySlotFills.Add(BuildSlotPicture(slot, "VictorySlotArt" + (i + 1),
                    ArtLibrary.IconOf(_level.Details[i]), side - SlotPadding * 2f,
                    _level.Details[i].SilhouetteRadius));
            }

            VictorySlots.gameObject.SetActive(false);
        }

        /// <summary>Sizes the outcome lines are drawn at (text registry, walkthrough frame 25).</summary>
        private static class GameTextSizes
        {
            public const int Big = 60;
            public const int Small = 40;
        }

        private static readonly Color DarkInk = new Color(0.23f, 0.23f, 0.21f);
        private static readonly Color DimInk = new Color(0.4f, 0.4f, 0.4f);

        /// <summary>Ink for a line standing on the location itself, where every plate is dark.</summary>
        private static readonly Color LightInk = new Color(0.97f, 0.96f, 0.92f);

        // ---- state -------------------------------------------------------------------------------

        /// <summary>
        /// Pale sun = the timer is not running (frames 4–8); bright = it is.
        ///
        /// «Pale» used to mean a translucent disc with no dial, no hand and no number — a blur in the
        /// corner rather than a clock that happens to be stopped. Now the whole instrument stays
        /// legible and only loses strength: the ring, the full (untouched) sector and the caption are
        /// all there, so the player reads «время ещё не пошло», not «что-то белое справа сверху».
        /// </summary>
        public void SetTimerRunning(bool running)
        {
            float alpha = running ? 1f : 0.72f;

            Color sun = Sun.color;
            sun.a = alpha;
            Sun.color = sun;

            Image sunFill = Sun.transform.childCount > 0
                ? Sun.transform.GetChild(0).GetComponent<Image>()
                : null;
            if (sunFill != null)
            {
                Color c = sunFill.color;
                c.a = (running ? 0.35f : 0.22f);
                sunFill.color = c;
            }

            Color dial = SunDial.color;
            dial.a = running ? 1f : 0.55f;
            SunDial.color = dial;

            // The caption stays up while the clock is held: the number is what says how long the level
            // WILL be, and its plate is the one SCREENS draws under the sun (300×40).
            TimerLabel.gameObject.SetActive(true);
            TimerLabel.color = running ? Color.white : new Color(1f, 1f, 1f, 0.8f);
            if (_timerPlate != null)
            {
                Color plate = _timerPlate.color;
                plate.a = running ? 0.55f : 0.4f;
                _timerPlate.color = plate;
            }
        }

        public void SetTimer(float remaining01, float secondsLeft)
        {
            SunDial.fillAmount = Mathf.Clamp01(remaining01);
            TimerLabel.text = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft)) + " с";
        }

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
                ? new[] { SceneLayer, DetailsLayer, ThreadLayer, VesselLayer, ThoughtsLayer, PeakLayer, HudLayer, MessageLayer }
                : new[] { SceneLayer, DetailsLayer, ThreadLayer, ThoughtsLayer, VesselLayer, PeakLayer, HudLayer, MessageLayer };

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
            ring.gameObject.SetActive(active);
            SetDragged(active ? index : -1);
            if (!active) return;

            Ui.MoveTo(ring.rectTransform, position);

            // A slip is a CLOSED red ring (mock 14) — a red arc would read as "red progress 60 %".
            ring.fillAmount = slipped ? 1f : Mathf.Clamp01(progress01);
            ring.color = spinning && !slipped ? LevelOneData.VesselStroke : LevelOneData.Alarm;
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

            if (index < _victorySlotFills.Count) _victorySlotFills[index].color = Color.white;

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
            for (int i = 0; i < _victorySlotFills.Count; i++)
                _victorySlotFills[i].color = SlotPictureHidden;

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
            GazeRing.gameObject.SetActive(visible);
            GazeArc.gameObject.SetActive(visible && dwell01 > 0f);
            if (!visible) return;

            Ui.MoveTo(Gaze.rectTransform, position);
            Ui.MoveTo(GazeRing.rectTransform, position);
            Ui.MoveTo(GazeArc.rectTransform, position);
            GazeArc.fillAmount = Mathf.Clamp01(dwell01);
        }

        public void SyncThoughts(IReadOnlyList<Thought> thoughts, float deltaTime)
        {
            ApplyCoverToggle();
            while (_thoughtPool.Count < thoughts.Count)
                _thoughtPool.Add(new ArtThoughtView(ThoughtsLayer));

            for (int i = 0; i < _thoughtPool.Count; i++)
            {
                bool used = i < thoughts.Count;
                _thoughtPool[i].SetActive(used);
                if (used) _thoughtPool[i].Bind(thoughts[i], deltaTime, _desaturated);
            }

            PeakVignette.rectTransform.SetAsLastSibling();
            PeakEdges.rectTransform.SetAsLastSibling();
        }

        public void ShowHint(string text, HintTone tone, Vector2 cardCentre, Vector2 target,
            float standoff = 50f)
        {
            Hint.Show(text, tone, cardCentre, target, standoff);
        }

        public void HideHint() => Hint.Hide();

        /// <summary>Mock 18: the vessel lifts to the centre at ×2 with the whole haul inside it.</summary>
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

            // S4 is «текст + ряд заполненных слотов» — the row is half of the screen.
            if (VictorySlots != null) VictorySlots.gameObject.SetActive(true);
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
            if (VictorySlots != null) VictorySlots.gameObject.SetActive(false);
        }

        private void SetBackgroundAlpha(float alpha)
        {
            Color c = _background.color;
            c.a = alpha;
            _background.color = c;
        }

        /// <summary>
        /// The outcome lines. The colour is the caller's, because the two outcomes sit on opposite
        /// backgrounds: the defeat lines are on a white plate (mock 17) and have to be dark, while
        /// «Собрано: …» stands straight on the location — and every one of the three plates is a dusk
        /// or an interior, where the mock's dark green went invisible.
        /// </summary>
        public void ShowMessage(string big, string small, bool plate = false, float bigY = 480f,
            int bigSize = 60, int smallSize = 40, bool lightInk = false)
        {
            _messagePlate.gameObject.SetActive(plate);
            Ui.MoveTo(BigMessage.rectTransform, new Vector2(960f, bigY));
            Ui.MoveTo(SmallMessage.rectTransform, new Vector2(960f, bigY + 140f));

            BigMessage.fontSize = bigSize;
            SmallMessage.fontSize = smallSize;
            BigMessage.color = lightInk ? LightInk : DarkInk;
            SmallMessage.color = lightInk ? LightInk : DimInk;
            BigMessage.text = big ?? string.Empty;
            SmallMessage.text = small ?? string.Empty;
            BigMessage.gameObject.SetActive(!string.IsNullOrEmpty(big));
            SmallMessage.gameObject.SetActive(!string.IsNullOrEmpty(small));
        }

        /// <summary>Tear the whole level's UI down (the flow rebuilds it for the next level).</summary>
        public void Dispose()
        {
            for (int i = 0; i < _thoughtPool.Count; i++) _thoughtPool[i].Dispose();
            _thoughtPool.Clear();

            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null;
        }
    }
}
