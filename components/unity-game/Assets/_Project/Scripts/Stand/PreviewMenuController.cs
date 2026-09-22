using System.Collections.Generic;
using AiGameStudio.ArcadeControls;
using Meditation.Mechanics;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Stand
{
    /// <summary>
    /// Entry scene of the preview stand: pick something and open it. Driven by the cabinet controls
    /// only — joystick to move, green button to open, "в меню" to leave — so the stand itself already
    /// obeys the panel it will ship on.
    ///
    /// Two columns since 2026-08-19. The left one is what the stand always was: the four scenettes of
    /// MECHANICS §7, greybox rigs that isolate one rule each. The right one is the founder's order of
    /// that day — «надо запускать уровни сценками по отдельности, для дебага и разработки» — five
    /// entries that open the REAL level (art, rules, three controllers) with no title, no card and no
    /// tutorial in front of it (<see cref="StandLevelLaunch"/>).
    ///
    /// Two columns rather than a list of nine, because nine cards do not fit: the scenette card is
    /// 116 px tall with its description, and the frame has about 500 px between the subtitle and the
    /// control hints. Stacking them would have meant shrinking the type on a screen the founder reads
    /// across a room.
    /// </summary>
    [AddComponentMenu("Meditation/Preview Menu")]
    public sealed class PreviewMenuController : MonoBehaviour
    {
        private const float RepeatDelay = 0.35f;

        private static readonly Color CardIdle = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color CardSelected = new Color(0.5f, 0.79f, 0.65f);
        private static readonly Color Ink = new Color(0.23f, 0.23f, 0.21f);

        // Grey-on-green failed contrast on the selected card, so the selected row goes dark-on-green.
        private static readonly Color DescriptionIdle = new Color(0.42f, 0.42f, 0.4f);
        private static readonly Color DescriptionSelected = new Color(0.11f, 0.24f, 0.18f);

        private readonly List<Image> _cards = new List<Image>();
        private readonly List<Text> _descriptions = new List<Text>();
        private float _repeatTimer;
        private float _repeatTimerX;
        private bool _greenWasHeld;

        public DesignStage Stage { get; private set; }

        /// <summary>
        /// Index of the highlighted entry over BOTH columns: 0…3 are the scenettes, 4…8 the levels.
        /// One number rather than a column plus a row, because the joystick's up/down runs through the
        /// whole menu — a stand where the stick stops dead at the bottom of a column would need the
        /// player to know there is a second one.
        /// </summary>
        public int Selected { get; private set; }

        /// <summary>How many entries the menu has in total — four scenettes plus the five levels.</summary>
        public int EntryCount => PreviewScenes.Scenettes.Length + PreviewScenes.LevelCount;

        /// <summary>Is the highlight on a level entry rather than on a scenette?</summary>
        public bool LevelIsSelected => Selected >= PreviewScenes.Scenettes.Length;

        /// <summary>Which level the highlight is on (0-based), or -1 when it is on a scenette.</summary>
        public int SelectedLevelIndex =>
            LevelIsSelected ? Selected - PreviewScenes.Scenettes.Length : -1;

        private void Awake()
        {
            if (Object.FindAnyObjectByType<ArcadeInputRunner>() == null)
                new GameObject("ArcadeInput").AddComponent<ArcadeInputRunner>();

            if (GetComponent<MenuButtonExit>() == null) gameObject.AddComponent<MenuButtonExit>();

            // A request nobody picked up (the game scene never booted, or «в меню» came back here)
            // must not fire the next time the founder opens the game from the launcher.
            StandLevelLaunch.Clear();

            // …and the level she just came back FROM must not leave its band on the shared values:
            // this is the scene «в меню» lands in from a level launched off the right-hand column.
            // The scenettes repeat the call in their own Awake, because a scenette scene can be
            // opened without ever passing through the menu (the suite does exactly that).
            TuningConfig.LeaveLevelBand();

            Stage = DesignStage.Create("StandCanvas");
            Build();
            Paint();
        }

        // ---- layout, design px ---------------------------------------------------------------------

        private const float ScenetteColumnX = 490f;
        private const float LevelColumnX = 1430f;
        private const float ColumnWidth = 860f;
        private const float ScenetteCardHeight = 116f;
        private const float ScenetteFirstY = 330f;
        private const float ScenetteStepY = 140f;
        private const float LevelCardHeight = 96f;
        // Level of the first card in the RIGHT column: the same line the left one starts on. It used
        // to be 310 — twenty px higher, because the level cards are shorter — and that put its top
        // edge through «уровни игры целиком» (the column's own caption sits at 258).
        private const float LevelFirstY = 330f;
        private const float LevelStepY = 112f;

        private RectTransform _root;

        private void Build()
        {
            RectTransform root = Ui.Layer(Stage.Frame, "Menu");
            _root = root;
            Ui.Box(root, "Background", 0f, 0f, 1920f, 1080f, LevelOneData.Sky);
            Ui.Box(root, "Ground", 0f, LevelOneData.SceneHeight, 1920f, 270f, LevelOneData.Ground);

            Ui.Label(root, "Title", "МЕДИТАЦИЯ В СПЕШКЕ", 960f, 110f, 1800f, 120f, 84, Ink,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Label(root, "Subtitle", "превью-стенд: " + PreviewScenes.Scenettes.Length +
                " " + Plural(PreviewScenes.Scenettes.Length, "сценка", "сценки", "сценок") +
                " механик и " + PreviewScenes.LevelCount +
                " " + Plural(PreviewScenes.LevelCount, "уровень", "уровня", "уровней") + " целиком",
                960f, 190f, 1800f, 60f, 36, new Color(0.45f, 0.45f, 0.42f));

            Ui.Label(root, "ColumnScenettes", "механики по одной", ScenetteColumnX, 258f,
                ColumnWidth, 46f, 32, new Color(0.4f, 0.4f, 0.38f), TextAnchor.MiddleLeft);
            Ui.Label(root, "ColumnLevels", "уровни игры целиком", LevelColumnX, 258f,
                ColumnWidth, 46f, 32, new Color(0.4f, 0.4f, 0.38f), TextAnchor.MiddleLeft);

            for (int i = 0; i < PreviewScenes.Scenettes.Length; i++)
                BuildCard("Card" + (i + 1), ScenetteColumnX, ScenetteFirstY + i * ScenetteStepY,
                    ScenetteCardHeight, PreviewScenes.Titles[i], PreviewScenes.Descriptions[i], 40, 28);

            for (int i = 0; i < PreviewScenes.LevelCount; i++)
                BuildCard("LevelCard" + (i + 1), LevelColumnX, LevelFirstY + i * LevelStepY,
                    LevelCardHeight, PreviewScenes.LevelTitle(i), PreviewScenes.LevelDescription(i),
                    38, 26);

            Ui.Label(root, "Hint",
                "джойстик ↑ ↓ ← → — выбор   ·   зелёная кнопка — открыть   ·   кнопка «в меню» — выход",
                960f, 985f, 1800f, 50f, 34, new Color(0.35f, 0.35f, 0.33f));
            Ui.Label(root, "HintKeyboard",
                // The panel's own names for its own controls — system/CONTROLS_BRIEF.md, «колесо мыши =
                // крутилка». It read «динамо» here, which is the name of the MECHANIC (MECHANICS §1),
                // not of the thing under the player's hand.
                "на ПК: стрелки = джойстик, колесо мыши = крутилка, Enter = зелёная, Esc = «в меню»",
                960f, 1035f, 1800f, 44f, 26, new Color(0.5f, 0.5f, 0.48f));
        }

        /// <summary>
        /// Russian plural agreement — «4 сценки», «5 уровней».
        ///
        /// A caption built by concatenating a count and a noun is a caption that is wrong four times
        /// out of five, and this one was: «5 уровня». Three forms, chosen the way the language does
        /// it (11–14 take the many-form whatever their last digit says).
        /// </summary>
        public static string Plural(int n, string one, string few, string many)
        {
            int mod100 = Mathf.Abs(n) % 100;
            if (mod100 >= 11 && mod100 <= 14) return many;
            switch (mod100 % 10)
            {
                case 1: return one;
                case 2:
                case 3:
                case 4: return few;
                default: return many;
            }
        }

        /// <summary>One menu entry: a card with its title and its one line of explanation.</summary>
        private void BuildCard(string name, float centreX, float centreY, float height,
            string title, string description, int titleSize, int descriptionSize)
        {
            Image card = Ui.Rounded(_root, name, centreX, centreY,
                ColumnWidth, height, CardIdle, LevelOneData.VesselStroke, 4f, 16);
            _cards.Add(card);

            float inset = -ColumnWidth * 0.5f + 36f;
            CentreIn(Ui.Label(card.transform, "CardTitle", title, 0f, 0f, ColumnWidth - 72f, 48f,
                titleSize, Ink, TextAnchor.MiddleLeft, FontStyle.Bold), new Vector2(inset, height * 0.18f));

            Text line = Ui.Label(card.transform, "CardText", description, 0f, 0f, ColumnWidth - 72f, 38f,
                descriptionSize, DescriptionIdle, TextAnchor.MiddleLeft);
            CentreIn(line, new Vector2(inset, -height * 0.21f));
            _descriptions.Add(line);
        }

        /// <summary>Anchor a label to its card's centre and grow it rightwards from the offset.</summary>
        private static void CentreIn(Text label, Vector2 offset)
        {
            RectTransform rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);      // left edge sits ON the offset, text runs right
            rt.anchoredPosition = offset;
        }

        private void Update()
        {
            Vector2 stick = ArcadeInput.Joystick.Vector;

            if (Mathf.Abs(stick.y) < 0.5f)
            {
                _repeatTimer = 0f;
            }
            else
            {
                _repeatTimer -= Time.deltaTime;
                if (_repeatTimer <= 0f)
                {
                    Move(stick.y > 0f ? -1 : 1);
                    _repeatTimer = RepeatDelay;
                }
            }

            if (Mathf.Abs(stick.x) < 0.5f)
            {
                _repeatTimerX = 0f;
            }
            else
            {
                _repeatTimerX -= Time.deltaTime;
                if (_repeatTimerX <= 0f)
                {
                    SwitchColumn(stick.x > 0f);
                    _repeatTimerX = RepeatDelay;
                }
            }

            bool green = ArcadeInput.GreenButton.IsHeld;
            if (green && !_greenWasHeld) Open();
            _greenWasHeld = green;
        }

        /// <summary>Move the highlight down the whole menu (wraps around).</summary>
        public void Move(int delta)
        {
            int count = EntryCount;
            Selected = (Selected + delta % count + count) % count;
            Paint();
        }

        /// <summary>
        /// Hop to the other column, keeping the row. The stick's left/right is the short way across a
        /// two-column menu — walking there with up/down means crossing everything in between.
        /// </summary>
        public void SwitchColumn(bool toLevels)
        {
            int scenettes = PreviewScenes.Scenettes.Length;
            if (toLevels == LevelIsSelected) return;

            Selected = toLevels
                ? scenettes + Mathf.Min(Selected, PreviewScenes.LevelCount - 1)
                : Mathf.Min(Selected - scenettes, scenettes - 1);
            Paint();
        }

        /// <summary>Open the highlighted entry: a scenette scene, or the game straight on a level.</summary>
        public void Open()
        {
            if (!LevelIsSelected)
            {
                PreviewStandNav.OpenScene(PreviewScenes.Scenettes[Selected]);
                return;
            }

            StandLevelLaunch.Open(SelectedLevelIndex);
        }

        private void Paint()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                Image fill = _cards[i].transform.childCount > 0
                    ? _cards[i].transform.GetChild(0).GetComponent<Image>()
                    : null;
                if (fill != null) fill.color = i == Selected ? CardSelected : CardIdle;
                if (i < _descriptions.Count)
                    _descriptions[i].color = i == Selected ? DescriptionSelected : DescriptionIdle;
            }
        }
    }
}
