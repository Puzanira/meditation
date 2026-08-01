using System.Collections.Generic;
using Meditation.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// Which parts of the level-1 composition a scenette needs. The HUD is NOT part of this: slots
    /// and the sun-dial are on every screen of the mock, and "таймер стоит" is drawn as a pale sun
    /// (frames 4–8), never as a missing one — so a scenette can only say whether its timer runs.
    /// </summary>
    public struct StageOptions
    {
        public bool ShowDetails;
        public bool ShowThoughts;
        public bool TimerRunning;

        public static StageOptions Full => new StageOptions
        {
            ShowDetails = true, ShowThoughts = true, TimerRunning = true
        };
    }

    /// <summary>
    /// The level-1 screen from SCREENS.md / gameplay-walkthrough.html, in greybox: sky, five towers,
    /// foreground strip, briefcase-vessel, sun-dial timer, HUD slots, crank indicator, details on their
    /// authored positions, the collection thread, the gaze circle and the thought layer.
    ///
    /// Z-order (SCREENS.md's list, with the foreground split in two so the walkthrough's frames are
    /// reproducible): scene + ground strip → details → thread → thoughts → vessel → HUD. The strip is
    /// scenery and sits under the travelling detail and its thread, exactly as frames 4–6 draw them;
    /// the vessel keeps its place above the thoughts, as the spec's z-order requires.
    /// </summary>
    public sealed class StageView : ICollectionView
    {
        /// <summary>Mock 18 draws the two surviving towers at <c>opacity="0.5"</c> behind the tableau.</summary>
        public const float VictoryBackgroundAlpha = 0.5f;

        private readonly DesignStage _stage;
        private readonly StageOptions _options;
        private readonly List<ThoughtView> _thoughtPool = new List<ThoughtView>();
        private readonly List<Image> _detailImages = new List<Image>();
        private readonly List<Image> _detailRings = new List<Image>();
        private readonly List<Image> _slots = new List<Image>();
        private readonly List<Image> _buildings = new List<Image>();
        private readonly List<Image> _vesselContents = new List<Image>();

        private float _vesselBounce;
        private float _pulse;
        private bool _coverApplied;
        private int _draggedIndex = -1;
        private float _sparkle;
        private bool _desaturated;
        private bool _victoryStaged;
        private CanvasGroup _thoughtsGroup;
        private Image _messagePlate;
        private Image _crankHalo;
        private readonly List<Image> _sparks = new List<Image>(3);

        public StageView(DesignStage stage, StageOptions options)
        {
            _stage = stage;
            _options = options;
            Build();
        }

        public RectTransform SceneLayer { get; private set; }
        public RectTransform DetailsLayer { get; private set; }
        public RectTransform ThreadLayer { get; private set; }
        public RectTransform ThoughtsLayer { get; private set; }
        public RectTransform ForegroundLayer { get; private set; }
        public RectTransform PeakLayer { get; private set; }
        public RectTransform HudLayer { get; private set; }
        public RectTransform MessageLayer { get; private set; }

        public Image Vessel { get; private set; }
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
        public HintCard Hint { get; private set; }
        public Text BigMessage { get; private set; }
        public Text SmallMessage { get; private set; }

        public IReadOnlyList<Image> Slots => _slots;
        public IReadOnlyList<Image> DetailImages => _detailImages;
        public IReadOnlyList<ThoughtView> ThoughtViews => _thoughtPool;

        private void Build()
        {
            Transform root = _stage.Frame;

            SceneLayer = Ui.Layer(root, "SceneLayer");
            DetailsLayer = Ui.Layer(root, "DetailsLayer");
            ThreadLayer = Ui.Layer(root, "ThreadLayer");
            ThoughtsLayer = Ui.Layer(root, "ThoughtsLayer");
            ForegroundLayer = Ui.Layer(root, "VesselLayer");
            PeakLayer = Ui.Layer(root, "PeakLayer");
            HudLayer = Ui.Layer(root, "HudLayer");
            MessageLayer = Ui.Layer(root, "MessageLayer");

            // --- scene (0,0 1920×810) + foreground strip (0,810 1920×270) -------------------
            Ui.Box(SceneLayer, "Sky", 0f, 0f, 1920f, LevelOneData.SceneHeight, LevelOneData.Sky);
            for (int i = 0; i < LevelOneData.Buildings.Length; i++)
            {
                Rect b = LevelOneData.Buildings[i];
                _buildings.Add(Ui.Box(SceneLayer, "Building" + (i + 1), b.x, b.y, b.width, b.height,
                    LevelOneData.Building));
            }

            Ui.Box(SceneLayer, "Ground", 0f, LevelOneData.SceneHeight, 1920f,
                1080f - LevelOneData.SceneHeight, LevelOneData.Ground);

            // --- details on their authored positions ----------------------------------------
            for (int i = 0; i < LevelOneData.Details.Length; i++)
            {
                DetailSpec spec = LevelOneData.Details[i];
                Image image = Ui.Rounded(DetailsLayer, "Detail_" + spec.Name,
                    spec.Home.x, spec.Home.y, spec.Size, spec.Size, spec.Fill, spec.Stroke, 3f, 8);
                image.gameObject.SetActive(_options.ShowDetails);
                _detailImages.Add(image);

                Image ring = Ui.Ring(DetailsLayer, "Ring_" + spec.Name, spec.Home.x, spec.Home.y, 55f,
                    LevelOneData.VesselStroke);
                ring.fillAmount = 0f;
                ring.gameObject.SetActive(false);
                _detailRings.Add(ring);
            }

            // --- collection thread ----------------------------------------------------------
            Thread = Ui.Dashed(ThreadLayer, "Thread", LevelOneData.Thread, 5f);
            Thread.gameObject.SetActive(false);

            // --- gaze circle: fill #7aa7d9 @0.25 + dashed contour #4a7096, 5 px, 12/10 (#gaze) --
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

            // --- thought layer (empty pool; grows on demand) --------------------------------
            _thoughtsGroup = ThoughtsLayer.gameObject.AddComponent<CanvasGroup>();

            // Peak dimming lives ABOVE the vessel and below the HUD: SCREENS asks for a light veil
            // over the picture (#3a3050 @0.12), not for a tint that the vessel can hide.
            PeakVignette = Ui.BoxCentred(PeakLayer, "PeakVignette", 960f, 540f, 1920f, 1080f,
                new Color(LevelOneData.Dim.r, LevelOneData.Dim.g, LevelOneData.Dim.b, 0f));

            // --- vessel (портфель, centre 960,930 · 240×150) ---------------------------------
            Vessel = Ui.Rounded(ForegroundLayer, "Vessel",
                LevelOneData.VesselCentre.x, LevelOneData.VesselCentre.y,
                LevelOneData.VesselSize.x, LevelOneData.VesselSize.y,
                LevelOneData.VesselFill, LevelOneData.VesselStroke, 4f, 14);

            // Three sparks over the vessel when a detail lands (walkthrough frame 6).
            for (int i = 0; i < 3; i++)
            {
                Image spark = Ui.Segment(ForegroundLayer, "Spark" + i, LevelOneData.VesselStroke, 7f);
                Vector2 from = new Vector2(900f + i * 60f, 800f - (i == 1 ? 10f : 0f));
                Vector2 to = from + new Vector2((i - 1) * 14f, -28f);
                Ui.StretchLine(spark.rectTransform, from, to);
                spark.gameObject.SetActive(false);
                _sparks.Add(spark);
            }

            // --- HUD ------------------------------------------------------------------------
            for (int i = 0; i < 5; i++)
            {
                float x = LevelOneData.SlotsOrigin.x + i * LevelOneData.SlotStep;
                Image slot = Ui.Rounded(HudLayer, "Slot" + (i + 1),
                    x + LevelOneData.SlotSize * 0.5f,
                    LevelOneData.SlotsOrigin.y + LevelOneData.SlotSize * 0.5f,
                    LevelOneData.SlotSize, LevelOneData.SlotSize,
                    Color.white, new Color(0.6f, 0.6f, 0.6f), 3f, 10);
                _slots.Add(slot);
            }

            Sun = Ui.Circle(HudLayer, "Sun", LevelOneData.SunCentre.x, LevelOneData.SunCentre.y,
                LevelOneData.SunRadius, new Color(1f, 1f, 1f, 0.35f), LevelOneData.SunStroke, 4f);
            SunDial = Ui.NewImage(HudLayer, "SunDial");
            SunDial.sprite = UiSprites.Circle;
            SunDial.type = Image.Type.Filled;
            SunDial.fillMethod = Image.FillMethod.Radial360;
            SunDial.fillOrigin = (int)Image.Origin360.Top;

            // The SPENT sector has to grow clockwise from 12 (mock frame 15), so the REMAINING
            // wedge — which is what fillAmount draws — is filled anti-clockwise from the same origin.
            SunDial.fillClockwise = false;
            SunDial.color = LevelOneData.SunFill;
            Ui.Place(SunDial.rectTransform, LevelOneData.SunCentre.x, LevelOneData.SunCentre.y,
                LevelOneData.SunRadius * 2f - 8f, LevelOneData.SunRadius * 2f - 8f);
            TimerLabel = Ui.Label(HudLayer, "TimerLabel", "", LevelOneData.SunCentre.x,
                LevelOneData.SunCentre.y + LevelOneData.SunRadius + 30f, 300f, 40f, 30,
                new Color(0.23f, 0.23f, 0.21f));

            // "Таймер стоит" is a PALE sun (frames 4–8), not a missing one.
            SetTimerRunning(_options.TimerRunning);

            // Halo behind the dial: green while the hand is in the zone, pink the moment a stall
            // starts costing a detail (walkthrough #dyn-on / frame 14).
            _crankHalo = Ui.Circle(HudLayer, "CrankHalo", LevelOneData.CrankIndicatorCentre.x,
                LevelOneData.CrankIndicatorCentre.y, LevelOneData.CrankHaloRadius,
                LevelOneData.CrankHaloOk, Color.clear);
            _crankHalo.gameObject.SetActive(false);

            CrankDial = Ui.Circle(HudLayer, "CrankDial", LevelOneData.CrankIndicatorCentre.x,
                LevelOneData.CrankIndicatorCentre.y, LevelOneData.CrankIndicatorRadius,
                Color.white, new Color(0.4f, 0.4f, 0.4f), 4f);

            // The arc is the "хватает" ZONE (#dyn-on, #2b7a5c) — it stays green; only the dial goes
            // red, and only when a stall is actually costing the player a detail.
            CrankArc = Ui.Ring(HudLayer, "CrankArc", LevelOneData.CrankIndicatorCentre.x,
                LevelOneData.CrankIndicatorCentre.y, LevelOneData.CrankIndicatorRadius + 14f,
                LevelOneData.VesselStroke);
            CrankArc.fillAmount = 0.35f;
            CrankArc.color = LevelOneData.VesselStroke;

            var needle = Ui.BoxCentred(CrankDial.transform, "CrankNeedle", 0f, 0f, 8f,
                LevelOneData.CrankIndicatorRadius, new Color(0.33f, 0.33f, 0.33f));
            CrankNeedle = needle.rectTransform;
            CrankNeedle.anchorMin = new Vector2(0.5f, 0.5f);
            CrankNeedle.anchorMax = new Vector2(0.5f, 0.5f);
            CrankNeedle.pivot = new Vector2(0.5f, 0f);
            CrankNeedle.anchoredPosition = Vector2.zero;

            // --- hint card + outcome overlay --------------------------------------------------
            Hint = new HintCard(MessageLayer);

            // White plate under the defeat lines (mock 17: rect 300,400 1320×330, #fff @0.88) —
            // without it the text sits on whatever blobs happen to be underneath.
            _messagePlate = Ui.Rounded(MessageLayer, "MessagePlate", 960f, 565f, 1320f, 330f,
                new Color(1f, 1f, 1f, 0.88f), Color.clear, 0f, 20);
            _messagePlate.gameObject.SetActive(false);

            BigMessage = Ui.Label(MessageLayer, "BigMessage", "", 960f, 480f, 1600f, 90f, 60,
                new Color(0.23f, 0.23f, 0.21f));
            SmallMessage = Ui.Label(MessageLayer, "SmallMessage", "", 960f, 620f, 1600f, 60f, 40,
                new Color(0.4f, 0.4f, 0.4f));
            BigMessage.gameObject.SetActive(false);
            SmallMessage.gameObject.SetActive(false);

            ApplyCoverToggle(true);
        }

        /// <summary>Pale sun = the timer is not running (frames 4–8); bright = it is.</summary>
        public void SetTimerRunning(bool running)
        {
            float alpha = running ? 1f : 0.35f;
            Color sun = Sun.color;
            sun.a = alpha;
            Sun.color = sun;

            Image sunFill = Sun.transform.childCount > 0
                ? Sun.transform.GetChild(0).GetComponent<Image>()
                : null;
            if (sunFill != null)
            {
                Color c = sunFill.color;
                c.a = 0.35f * alpha;
                sunFill.color = c;
            }

            Color dial = SunDial.color;
            dial.a = alpha;
            SunDial.color = dial;
            TimerLabel.gameObject.SetActive(running);
        }

        /// <summary>
        /// MECHANICS §4 [toggle] "могут ли мысли закрывать сосуд и деталь, которую сейчас тянешь":
        /// the vessel layer (which also carries the detail being dragged) swaps sides with the
        /// thought layer.
        /// </summary>
        public void ApplyCoverToggle(bool force = false)
        {
            bool cover = Tuning.TuningConfig.ThoughtsCoverVessel;
            if (!force && cover == _coverApplied) return;
            _coverApplied = cover;

            ApplyLayerOrder(cover);

            // Covered does not mean erased: the mock keeps the vessel readable at half opacity under
            // the blobs (frame 16), so the player can still see where the details are going.
            SetVesselAlpha(cover ? 0.5f : 1f);
        }

        /// <summary>
        /// The whole stack from the toggle, not one layer nudged past another — the same fix as in
        /// <see cref="LevelView"/>: «thoughtsIndex + 1» ignored that pulling the vessel out of the list
        /// shifts the layers above it down, so on → off left the vessel above the peak veil.
        /// </summary>
        private void ApplyLayerOrder(bool cover)
        {
            RectTransform[] order = cover
                ? new[] { SceneLayer, DetailsLayer, ThreadLayer, ForegroundLayer, ThoughtsLayer, PeakLayer, HudLayer, MessageLayer }
                : new[] { SceneLayer, DetailsLayer, ThreadLayer, ThoughtsLayer, ForegroundLayer, PeakLayer, HudLayer, MessageLayer };

            int first = int.MaxValue;
            for (int i = 0; i < order.Length; i++)
                first = Mathf.Min(first, order[i].GetSiblingIndex());

            for (int i = 0; i < order.Length; i++)
                order[i].SetSiblingIndex(first + i);
        }

        private void SetVesselAlpha(float alpha)
        {
            var graphics = ForegroundLayer.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Color c = graphics[i].color;
                if (c.a <= 0.02f) continue;
                c.a = alpha;
                graphics[i].color = c;
            }
        }

        // ---- per-frame updates ---------------------------------------------------------------

        /// <summary>Idle pulse of the not-yet-noticed details (scale 1.0 → 1.12, 1.6 s cycle).</summary>
        public void TickPulse(float deltaTime, IReadOnlyList<bool> collected, int noticedIndex)
        {
            _pulse += deltaTime;
            float k = 1f + 0.12f * 0.5f * (1f + Mathf.Sin(_pulse * Mathf.PI * 2f / 1.6f));
            for (int i = 0; i < _detailImages.Count; i++)
            {
                bool isCollected = collected != null && i < collected.Count && collected[i];
                bool pulsing = !isCollected && i != noticedIndex;
                _detailImages[i].rectTransform.localScale = pulsing ? new Vector3(k, k, 1f) : Vector3.one;
            }

            if (_vesselBounce > 0f)
            {
                _vesselBounce = Mathf.Max(0f, _vesselBounce - deltaTime);
                float b = 1f + 0.1f * Mathf.Sin(Mathf.Clamp01(_vesselBounce / 0.3f) * Mathf.PI);
                Vessel.rectTransform.localScale = new Vector3(b, b, 1f);
            }
            else if (!_victoryStaged)
            {
                Vessel.rectTransform.localScale = Vector3.one;
            }

        }

        /// <summary>
        /// Chrome that must keep ticking in every state, gameplay or outcome — the collect sparks
        /// would otherwise freeze on screen the moment the level ends (they did, on the victory shot).
        /// </summary>
        public void TickChrome(float deltaTime)
        {
            if (_sparkle <= 0f) return;
            _sparkle = Mathf.Max(0f, _sparkle - deltaTime);
            if (_sparkle > 0f) return;
            for (int i = 0; i < _sparks.Count; i++) _sparks[i].gameObject.SetActive(false);
        }

        /// <summary>Place a detail along its home → vessel path and drive its progress ring.</summary>
        public void SetDetailProgress(int index, float progress01, bool spinning, bool active,
            bool slipped = false)
        {
            if (index < 0 || index >= _detailImages.Count) return;

            DetailSpec spec = LevelOneData.Details[index];
            Vector2 position = Vector2.Lerp(spec.Home, LevelOneData.VesselCentre, Mathf.Clamp01(progress01));
            Ui.MoveTo(_detailImages[index].rectTransform, position);

            Image ring = _detailRings[index];
            ring.gameObject.SetActive(active);
            SetDragged(active ? index : -1);
            if (!active) return;

            Ui.MoveTo(ring.rectTransform, position);

            // A slip is a CLOSED red ring (mock 14). A red arc would read as "red progress 60 %",
            // which is the opposite of what just happened.
            ring.fillAmount = slipped ? 1f : Mathf.Clamp01(progress01);
            ring.color = spinning && !slipped ? LevelOneData.VesselStroke : LevelOneData.Alarm;
        }

        /// <summary>
        /// The detail being dragged rides with the vessel layer, so the "мысли закрывают сосуд и
        /// текущую деталь" [toggle] governs both of them together, exactly as MECHANICS §4 words it.
        /// </summary>
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

            _detailImages[_draggedIndex].transform.SetParent(ForegroundLayer, false);
            _detailRings[_draggedIndex].transform.SetParent(ForegroundLayer, false);
            SetVesselAlpha(_coverApplied ? 0.5f : 1f);
        }

        public void ShowDetail(int index, bool visible)
        {
            if (index < 0 || index >= _detailImages.Count) return;
            _detailImages[index].gameObject.SetActive(visible);
        }

        /// <summary>Detail landed: vessel bounces, the HUD slot fills, a copy shows inside the vessel.</summary>
        public void CollectDetail(int index)
        {
            if (index < 0 || index >= _detailImages.Count) return;

            DetailSpec spec = LevelOneData.Details[index];
            SetDragged(-1);
            _detailImages[index].gameObject.SetActive(false);
            _detailRings[index].gameObject.SetActive(false);
            _vesselBounce = 0.3f;

            if (index < _slots.Count)
            {
                Image innerSlot = _slots[index].transform.childCount > 0
                    ? _slots[index].transform.GetChild(0).GetComponent<Image>()
                    : null;
                if (innerSlot != null) innerSlot.color = spec.Fill;
            }

            // Full-size copy (50 px, mock 6) parented to the vessel so it rides along when the
            // victory screen lifts and doubles it.
            Image copy = Ui.Rounded(Vessel.transform, "InVessel_" + spec.Name, 0f, 0f, 50f, 50f,
                spec.Fill, spec.Stroke, 2f, 8);
            _vesselContents.Add(copy);
            LayoutVesselContents();

            _sparkle = 0.45f;
            for (int i = 0; i < _sparks.Count; i++) _sparks[i].gameObject.SetActive(true);
            SetVesselAlpha(_coverApplied ? 0.5f : 1f);
        }

        /// <summary>Keep the collected details centred inside the vessel, whatever their number.</summary>
        private void LayoutVesselContents()
        {
            int count = _vesselContents.Count;
            for (int i = 0; i < count; i++)
            {
                RectTransform rt = _vesselContents[i].rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(50f, 50f);
                float step = count > 1 ? Mathf.Min(46f, 190f / (count - 1)) : 0f;
                rt.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * step, 0f);
            }
        }

        public void ResetCollected()
        {
            SetDragged(-1);
            ClearVictoryStaging();
            _sparkle = 0f;
            for (int i = 0; i < _sparks.Count; i++) _sparks[i].gameObject.SetActive(false);
            for (int i = 0; i < _vesselContents.Count; i++)
                if (_vesselContents[i] != null) Object.Destroy(_vesselContents[i].gameObject);
            _vesselContents.Clear();

            for (int i = 0; i < _slots.Count; i++)
            {
                Image innerSlot = _slots[i].transform.childCount > 0
                    ? _slots[i].transform.GetChild(0).GetComponent<Image>()
                    : null;
                if (innerSlot != null) innerSlot.color = Color.white;
            }

            for (int i = 0; i < _detailImages.Count; i++)
            {
                _detailImages[i].gameObject.SetActive(_options.ShowDetails);
                Ui.MoveTo(_detailImages[i].rectTransform, LevelOneData.Details[i].Home);
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

        /// <summary>
        /// Crank HUD (SCREENS "индикатор динамо"): the needle turns with the hand, the green arc is
        /// the "хватает" zone and stays green; the dial lights up green while the hand is inside the
        /// zone and only turns red when a stall is actually costing a detail — an idle stand must not
        /// look like an error.
        /// </summary>
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

        public void SetTimer(float remaining01, float secondsLeft)
        {
            SunDial.fillAmount = Mathf.Clamp01(remaining01);
            TimerLabel.text = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft)) + " с";
        }

        /// <summary>Show a teaching card next to its target (walkthrough frames 4, 7, 9).</summary>
        public void ShowHint(string text, HintTone tone, Vector2 cardCentre, Vector2 target,
            float standoff = 50f)
        {
            Hint.Show(text, tone, cardCentre, target, standoff);
        }

        public void HideHint() => Hint.Hide();

        public void SetPeak(bool peak)
        {
            Color c = LevelOneData.Dim;
            // SCREENS/mock 16: лёгкое затемнение #3a3050 @0.12. The canvas blends in linear space, so
            // the literal 0.12 renders at ~5 % — LevelOneData.DimAlpha is that 0.12 restated for it.
            c.a = peak ? LevelOneData.DimAlpha : 0f;
            PeakVignette.color = c;
        }

        /// <summary>Mirror the thought list into views, growing the pool as waves arrive.</summary>
        public void SyncThoughts(IReadOnlyList<Thought> thoughts, float deltaTime)
        {
            ApplyCoverToggle();
            while (_thoughtPool.Count < thoughts.Count)
                _thoughtPool.Add(new ThoughtView(ThoughtsLayer));

            for (int i = 0; i < _thoughtPool.Count; i++)
            {
                bool used = i < thoughts.Count;
                _thoughtPool[i].SetActive(used);
                if (used) _thoughtPool[i].Bind(thoughts[i], deltaTime, _desaturated);
            }

            // The vignette must stay on top of the blobs.
            PeakVignette.rectTransform.SetAsLastSibling();
        }

        /// <summary>HUD is off on the outcome screens — mocks 17–18 have no slots, sun or dial.</summary>
        public void SetHudVisible(bool visible) => HudLayer.gameObject.SetActive(visible);

        /// <summary>Defeat: «цвета гаснут» — the blobs lose their colour, the scene stays readable.</summary>
        public void SetDesaturated(bool desaturated) => _desaturated = desaturated;

        /// <summary>Thought layer opacity, used by the victory dissolve (mock 18: 0.5 s).</summary>
        public void SetThoughtsAlpha(float alpha)
        {
            if (_thoughtsGroup != null) _thoughtsGroup.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>
        /// The victory tableau of mock 18: the vessel lifts to the centre of the screen at ×2 with
        /// all five details inside it. The caller drives the timing (dissolve → silence → tableau).
        /// </summary>
        public void StageVictory()
        {
            if (_victoryStaged) return;
            _victoryStaged = true;
            _vesselBounce = 0f;
            Ui.MoveTo(Vessel.rectTransform, new Vector2(960f, 540f));
            Vessel.rectTransform.localScale = new Vector3(2f, 2f, 1f);

            // Mock 18 keeps the district behind the tableau, but at half strength — the vessel with the
            // haul is the picture, the city is only what it was collected from.
            SetBuildingsAlpha(VictoryBackgroundAlpha);
        }

        private void ClearVictoryStaging()
        {
            if (!_victoryStaged) return;
            _victoryStaged = false;
            Ui.MoveTo(Vessel.rectTransform, LevelOneData.VesselCentre);
            Vessel.rectTransform.localScale = Vector3.one;
            SetThoughtsAlpha(1f);
            SetBuildingsAlpha(1f);
        }

        private void SetBuildingsAlpha(float alpha)
        {
            for (int i = 0; i < _buildings.Count; i++)
            {
                Color c = _buildings[i].color;
                c.a = alpha;
                _buildings[i].color = c;
            }
        }

        public void ShowMessage(string big, string small, bool plate = false, float bigY = 480f)
        {
            _messagePlate.gameObject.SetActive(plate);
            Ui.MoveTo(BigMessage.rectTransform, new Vector2(960f, bigY));
            Ui.MoveTo(SmallMessage.rectTransform, new Vector2(960f, bigY + 140f));

            BigMessage.text = big ?? string.Empty;
            SmallMessage.text = small ?? string.Empty;
            BigMessage.gameObject.SetActive(!string.IsNullOrEmpty(big));
            SmallMessage.gameObject.SetActive(!string.IsNullOrEmpty(small));
        }
    }
}
