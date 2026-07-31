using System;
using System.Collections.Generic;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Tuning
{
    /// <summary>
    /// The on-screen tuning panel: every [tune]/[toggle] of the current scenette as a slider or a
    /// switch, applied live (done contract §7), plus a live readout block so the founder can see the
    /// numbers her hands are producing (crank deg/s, hits, coverage).
    ///
    /// It docks into a reserved strip on the right — <see cref="DesignStage"/> letterboxes the game
    /// into what is left, so the composition is never covered or squashed. Collapse it and the frame
    /// takes the whole window again (that is the state to screenshot against the walkthrough).
    ///
    /// Mouse-driven on purpose: this is a development stand, not a cabinet screen. No gameplay input
    /// ever passes through here — the game itself reads only ArcadeInput.
    /// </summary>
    public sealed class TuningPanel : MonoBehaviour
    {
        public const float PanelWidth = 460f;

        /// <summary>Title + subtitle band at the top of the panel.</summary>
        public const float HeaderHeight = 82f;

        /// <summary>Full height of the live-readout block when the panel has room for it.</summary>
        public const float ReadoutZoneHeight = 200f;

        /// <summary>Bottom band owned by the collapse button — no zone may reach into it.</summary>
        public const float ChromeBottomHeight = 64f;

        /// <summary>On a short window the readout gives ground too, instead of eating the whole panel.</summary>
        public const float ReadoutMaxShare = 0.45f;

        private static readonly Color PanelBg = new Color(0.13f, 0.14f, 0.16f, 0.94f);
        private static readonly Color RowText = new Color(0.92f, 0.93f, 0.9f);
        private static readonly Color Accent = new Color(0.16f, 0.48f, 0.37f);
        private static readonly Color Idle = new Color(0.28f, 0.29f, 0.31f);
        private static readonly Color Divider = new Color(0.35f, 0.37f, 0.36f, 0.8f);

        private readonly List<Action> _refreshers = new List<Action>();
        private readonly List<RectTransform> _rowRects = new List<RectTransform>();
        private DesignStage _stage;
        private RectTransform _body;
        private RectTransform _rows;
        private Text _readoutLabel;
        private Text _toggleButtonLabel;
        private Func<string> _readout;

        /// <summary>The clipped window the parameter rows live in — nothing draws outside it.</summary>
        public RectTransform RowsViewport { get; private set; }

        /// <summary>The scrolling content: as tall as the rows need, however short the window is.</summary>
        public RectTransform RowsContent => _rows;

        /// <summary>The live-readout block. Its own zone, never shared with a row.</summary>
        public RectTransform ReadoutZone { get; private set; }

        public ScrollRect Scroll { get; private set; }

        /// <summary>Every widget of every parameter row, for the no-overlap layout test.</summary>
        public IReadOnlyList<RectTransform> RowWidgets => _rowRects;

        /// <summary>
        /// How the panel's height is divided, given the window it ends up in. Pure arithmetic so the
        /// invariant «the four bands tile the panel and never overlap» can be checked without a canvas.
        /// The readout keeps its full height while there is room, then shrinks with the window rather
        /// than letting the rows run into it — which is exactly what used to happen at 1346×636, where
        /// scenette 4's list (the longest one: уровень + взгляд + волны + давление) ran straight over
        /// the readings block pinned to the bottom.
        /// </summary>
        public static void ResolveZones(float panelHeight, out float header, out float rows,
            out float readout, out float chrome)
        {
            float height = Mathf.Max(0f, panelHeight);
            chrome = Mathf.Min(ChromeBottomHeight, height);

            float usable = height - chrome;
            readout = Mathf.Min(ReadoutZoneHeight, usable * ReadoutMaxShare);
            header = Mathf.Min(HeaderHeight, Mathf.Max(0f, usable - readout));
            rows = Mathf.Max(0f, usable - header - readout);
        }

        public static TuningPanel Create(DesignStage stage, string title, IList<TuningParam> parameters,
            Func<string> readout)
        {
            var go = new GameObject("TuningPanel", typeof(RectTransform), typeof(TuningPanel));
            go.transform.SetParent(stage.Overlay, false);

            var panel = go.GetComponent<TuningPanel>();
            panel._stage = stage;
            panel._readout = readout;
            panel.Build(title, parameters);
            return panel;
        }

        private void Build(string title, IList<TuningParam> parameters)
        {
            var self = (RectTransform)transform;
            self.anchorMin = new Vector2(1f, 0f);
            self.anchorMax = new Vector2(1f, 1f);
            self.pivot = new Vector2(1f, 1f);
            self.offsetMin = new Vector2(-PanelWidth, 0f);
            self.offsetMax = Vector2.zero;

            _body = Ui.Layer(transform, "Body");
            var bg = Ui.NewImage(_body, "Background");
            bg.color = PanelBg;
            bg.raycastTarget = true;
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // --- header band (title + subtitle), fixed at the top ---------------------------------
            AddText(_body, "Title", title, 20f, 16f, PanelWidth - 40f, 30f, 23, RowText, FontStyle.Bold);
            AddText(_body, "Subtitle", "[tune] / [toggle] — меняются на лету", 20f, 50f,
                PanelWidth - 40f, 24f, 16, new Color(0.6f, 0.63f, 0.6f));

            // --- rows band: a clipped, scrolling window --------------------------------------------
            // The rows used to be laid out straight into the panel body while the readout hung off the
            // bottom edge, so on any window shorter than 1080 the two simply drew through each other —
            // in every scenette, not only the longest one. Now the list owns a viewport with a hard
            // bottom edge and scrolls inside it; nothing it contains can reach the readings block.
            var scrollGo = new GameObject("RowsScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_body, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;

            RowsViewport = Ui.Layer(scrollRt, "RowsViewport");
            RowsViewport.gameObject.AddComponent<RectMask2D>();
            var catcher = Ui.NewImage(RowsViewport, "WheelCatcher");
            catcher.color = new Color(0f, 0f, 0f, 0f);   // invisible, but the wheel needs something to hit
            catcher.raycastTarget = true;
            var catcherRt = catcher.rectTransform;
            catcherRt.anchorMin = Vector2.zero;
            catcherRt.anchorMax = Vector2.one;
            catcherRt.offsetMin = Vector2.zero;
            catcherRt.offsetMax = Vector2.zero;

            _rows = Ui.Layer(RowsViewport, "RowsContent");
            _rows.anchorMin = new Vector2(0f, 1f);
            _rows.anchorMax = new Vector2(1f, 1f);
            _rows.pivot = new Vector2(0.5f, 1f);
            _rows.offsetMin = new Vector2(0f, 0f);
            _rows.offsetMax = new Vector2(0f, 0f);
            _rows.anchoredPosition = Vector2.zero;

            float y = 8f;
            for (int i = 0; i < parameters.Count; i++)
                y = AddRow(parameters[i], y);

            y += 6f;
            _rowRects.Add(AddButton(_rows, "Reset", "сброс к дефолтам", 20f, y, 200f, 30f, () =>
            {
                // Defaults AND no saved file: "сброс" has to undo the session, not persist it.
                TuningStore.Clear();
                Refresh();
            }).targetGraphic.rectTransform);
            y += 38f;

            _rows.sizeDelta = new Vector2(0f, y);

            Scroll = scrollGo.GetComponent<ScrollRect>();
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Clamped;
            Scroll.scrollSensitivity = 32f;
            Scroll.viewport = RowsViewport;
            Scroll.content = _rows;

            // --- readout band: its own zone above the collapse button ------------------------------
            ReadoutZone = Ui.Layer(_body, "ReadoutZone");
            ReadoutZone.anchorMin = new Vector2(0f, 0f);
            ReadoutZone.anchorMax = new Vector2(1f, 0f);
            ReadoutZone.pivot = new Vector2(0.5f, 0f);
            ReadoutZone.offsetMin = new Vector2(0f, 0f);
            ReadoutZone.offsetMax = new Vector2(0f, 0f);

            var rule = Ui.NewImage(ReadoutZone, "ReadoutDivider");
            rule.color = Divider;
            var ruleRt = rule.rectTransform;
            ruleRt.anchorMin = new Vector2(0f, 1f);
            ruleRt.anchorMax = new Vector2(1f, 1f);
            ruleRt.pivot = new Vector2(0.5f, 1f);
            ruleRt.offsetMin = new Vector2(20f, 0f);
            ruleRt.offsetMax = new Vector2(-20f, 0f);
            ruleRt.sizeDelta = new Vector2(-40f, 2f);

            _readoutLabel = AddText(ReadoutZone, "Readout", "", 20f, 10f, PanelWidth - 40f, 100f, 17,
                new Color(0.75f, 0.85f, 0.8f));
            RectTransform readoutRt = _readoutLabel.rectTransform;
            readoutRt.anchorMin = new Vector2(0f, 0f);
            readoutRt.anchorMax = new Vector2(1f, 1f);
            readoutRt.pivot = new Vector2(0.5f, 0.5f);
            readoutRt.offsetMin = new Vector2(20f, 8f);
            readoutRt.offsetMax = new Vector2(-20f, -10f);
            _readoutLabel.alignment = TextAnchor.UpperLeft;

            // Truncate, never overflow: with Overflow the glyphs of a long readout grow straight out of
            // the zone and back over the rows, which is the same bug wearing a different hat.
            _readoutLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _readoutLabel.verticalOverflow = VerticalWrapMode.Truncate;
            ReadoutZone.gameObject.AddComponent<RectMask2D>();

            LayoutZones();

            // Collapse button lives outside the body so it survives hiding the panel.
            // Bottom-right: with the panel collapsed this is the emptiest corner of the composition,
            // so a screenshot for the design gate is not spoiled by stand chrome.
            var buttonHost = Ui.Layer(transform, "CollapseHost");
            buttonHost.anchorMin = new Vector2(1f, 0f);
            buttonHost.anchorMax = new Vector2(1f, 0f);
            buttonHost.pivot = new Vector2(1f, 0f);
            buttonHost.sizeDelta = new Vector2(200f, 40f);
            buttonHost.anchoredPosition = new Vector2(-16f, 16f);
            Button collapse = AddButton(buttonHost, "Collapse", "", 0f, 0f, 200f, 36f,
                () => SetVisible(!TuningConfig.PanelVisible));
            // Stand chrome sitting on top of the composition wears the stand's own palette.
            ((Image)collapse.targetGraphic).color = Mechanics.LevelOneData.VesselStroke;
            _toggleButtonLabel = collapse.GetComponentInChildren<Text>();

            ApplyVisibility();
            Refresh();
        }

        private float AddRow(TuningParam param, float y)
        {
            switch (param)
            {
                case FloatParam f: return AddFloatRow(f, y);
                case BoolParam b: return AddBoolRow(b, y);
                case ChoiceParam c: return AddChoiceRow(c, y);
                default: return y;
            }
        }

        private float AddFloatRow(FloatParam param, float y)
        {
            Text label = AddText(_rows, "Label_" + param.Label, param.Label, 20f, y, PanelWidth - 40f, 24f, 18, RowText);
            _rowRects.Add(label.rectTransform);

            var sliderGo = new GameObject("Slider_" + param.Label, typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(_rows, false);
            var sliderRt = (RectTransform)sliderGo.transform;
            SetTopLeft(sliderRt, 20f, y + 26f, PanelWidth - 40f, 22f);
            _rowRects.Add(sliderRt);

            var track = Ui.NewImage(sliderGo.transform, "Track");
            track.color = new Color(0.22f, 0.23f, 0.25f);
            track.raycastTarget = true;
            Stretch(track.rectTransform, 0f, 8f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)fillArea.transform, 0f, 8f);

            var fill = Ui.NewImage(fillArea.transform, "Fill");
            fill.color = Accent;
            var fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var handle = Ui.Circle(sliderGo.transform, "Handle", 0f, 0f, 11f, RowText, RowText);
            handle.raycastTarget = true;
            var handleRt = handle.rectTransform;
            handleRt.anchorMin = new Vector2(0f, 0.5f);
            handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.anchoredPosition = Vector2.zero;
            handleRt.sizeDelta = new Vector2(22f, 22f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle;
            slider.minValue = param.Min;
            slider.maxValue = param.Max;
            slider.wholeNumbers = param.WholeNumbers;
            slider.value = Mathf.Clamp(param.Get(), param.Min, param.Max);
            slider.onValueChanged.AddListener(v =>
            {
                param.Set(v);
                label.text = FormatFloat(param);
                TuningStore.Save();
            });

            label.text = FormatFloat(param);
            _refreshers.Add(() =>
            {
                float current = Mathf.Clamp(param.Get(), param.Min, param.Max);
                if (!Mathf.Approximately(slider.value, current)) slider.SetValueWithoutNotify(current);
                label.text = FormatFloat(param);
            });

            return y + 54f;
        }

        private float AddBoolRow(BoolParam param, float y)
        {
            Text label = AddText(_rows, "Label_" + param.Label, param.Label, 70f, y + 2f, PanelWidth - 96f, 24f, 18, RowText);
            _rowRects.Add(label.rectTransform);

            var box = Ui.NewImage(_rows, "Toggle_" + param.Label);
            box.color = Idle;
            box.raycastTarget = true;
            SetTopLeft(box.rectTransform, 20f, y, 36f, 28f);
            _rowRects.Add(box.rectTransform);

            var check = Ui.NewImage(box.transform, "Check");
            check.color = Accent;
            Stretch(check.rectTransform, 6f, 6f);

            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box;
            button.onClick.AddListener(() =>
            {
                param.Set(!param.Get());
                check.gameObject.SetActive(param.Get());
                TuningStore.Save();
            });

            check.gameObject.SetActive(param.Get());
            _refreshers.Add(() => check.gameObject.SetActive(param.Get()));
            return y + 38f;
        }

        private float AddChoiceRow(ChoiceParam param, float y)
        {
            Text label = AddText(_rows, "Label_" + param.Label, param.Label, 20f, y, PanelWidth - 40f, 24f, 18, RowText);
            _rowRects.Add(label.rectTransform);

            var buttons = new List<Image>();
            float width = (PanelWidth - 40f - (param.Options.Length - 1) * 8f) / param.Options.Length;
            for (int i = 0; i < param.Options.Length; i++)
            {
                int index = i;
                Button button = AddButton(_rows, "Choice_" + param.Label + "_" + i, param.Options[i],
                    20f + i * (width + 8f), y + 26f, width, 30f, () =>
                    {
                        param.Set(index);
                        PaintChoices(param, buttons);
                        TuningStore.Save();
                    });
                buttons.Add(button.targetGraphic as Image);
                _rowRects.Add(button.targetGraphic.rectTransform);
            }

            PaintChoices(param, buttons);
            _refreshers.Add(() => PaintChoices(param, buttons));
            return y + 62f;
        }

        private static void PaintChoices(ChoiceParam param, IList<Image> buttons)
        {
            int selected = param.Get();
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].color = i == selected ? Accent : Idle;
            }
        }

        private Button AddButton(Transform parent, string name, string text, float x, float y,
            float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var image = Ui.NewImage(parent, name);
            image.sprite = UiSprites.RoundedOf(10);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Idle;
            image.raycastTarget = true;
            SetTopLeft(image.rectTransform, x, y, w, h);

            Text label = Ui.Label(image.transform, "Text", text, 0f, 0f, w, h, 18, RowText);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            return button;
        }

        private static Text AddText(Transform parent, string name, string text, float x, float y,
            float w, float h, int size, Color colour, FontStyle style = FontStyle.Normal)
        {
            Text label = Ui.Label(parent, name, text, 0f, 0f, w, h, size, colour, TextAnchor.UpperLeft, style);
            SetTopLeft(label.rectTransform, x, y, w, h);
            return label;
        }

        private static void SetTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        private static void Stretch(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        private static string FormatFloat(FloatParam param)
        {
            string value = param.Get().ToString(param.Format, System.Globalization.CultureInfo.InvariantCulture);
            return param.Label + ":  " + value + (string.IsNullOrEmpty(param.Unit) ? "" : " " + param.Unit);
        }

        /// <summary>Show or collapse the panel (the collapsed state is remembered like any [tune] value).</summary>
        public void SetVisible(bool visible)
        {
            TuningConfig.PanelVisible = visible;
            ApplyVisibility();
            TuningStore.Save();
        }

        /// <summary>
        /// Give each band its slice of the panel, every frame the window may have changed. The rows
        /// viewport gets whatever is left between the header and the readout, so the two can never
        /// occupy the same pixels at any resolution — and when what is left is smaller than the list,
        /// the list scrolls instead of spilling.
        /// </summary>
        public void LayoutZones()
        {
            var self = (RectTransform)transform;
            ResolveZones(self.rect.height, out float header, out _, out float readout,
                out float chrome);

            if (RowsViewport != null && RowsViewport.parent is RectTransform scroll)
            {
                scroll.offsetMin = new Vector2(0f, chrome + readout);
                scroll.offsetMax = new Vector2(0f, -header);
            }

            if (ReadoutZone != null)
            {
                ReadoutZone.sizeDelta = new Vector2(0f, readout);
                ReadoutZone.anchoredPosition = new Vector2(0f, chrome);
            }
        }

        private void ApplyVisibility()
        {
            bool visible = TuningConfig.PanelVisible;
            _body.gameObject.SetActive(visible);
            if (visible) LayoutZones();
            if (_toggleButtonLabel != null) _toggleButtonLabel.text = visible ? "скрыть панель" : "параметры";
            if (_stage != null)
            {
                _stage.ReservedRight = visible ? PanelWidth : 0f;
                _stage.Fit();
            }
        }

        /// <summary>Pull every widget back in sync with TuningConfig (after a reset, or a scene change).</summary>
        public void Refresh()
        {
            for (int i = 0; i < _refreshers.Count; i++) _refreshers[i]();
        }

        private void Update()
        {
            if (!TuningConfig.PanelVisible) return;

            // The window is not fixed — the founder plays in a Game view of whatever size — so the
            // bands are re-measured every frame rather than once at build time.
            LayoutZones();
            if (_readoutLabel != null && _readout != null) _readoutLabel.text = _readout();
        }
    }
}
