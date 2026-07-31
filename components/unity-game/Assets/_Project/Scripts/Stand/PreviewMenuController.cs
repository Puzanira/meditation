using System.Collections.Generic;
using AiGameStudio.ArcadeControls;
using Meditation.Mechanics;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Stand
{
    /// <summary>
    /// Entry scene of the preview stand: pick one of the four scenettes of MECHANICS §7 and open it.
    /// Driven by the cabinet controls only — joystick to move, green button to open, "в меню" to leave —
    /// so the stand itself already obeys the panel it will ship on.
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
        private bool _greenWasHeld;

        public DesignStage Stage { get; private set; }

        /// <summary>Index of the highlighted scenette (0..3).</summary>
        public int Selected { get; private set; }

        private void Awake()
        {
            if (Object.FindAnyObjectByType<ArcadeInputRunner>() == null)
                new GameObject("ArcadeInput").AddComponent<ArcadeInputRunner>();

            if (GetComponent<MenuButtonExit>() == null) gameObject.AddComponent<MenuButtonExit>();

            Stage = DesignStage.Create("StandCanvas");
            Build();
            Paint();
        }

        private void Build()
        {
            RectTransform root = Ui.Layer(Stage.Frame, "Menu");
            Ui.Box(root, "Background", 0f, 0f, 1920f, 1080f, LevelOneData.Sky);
            Ui.Box(root, "Ground", 0f, LevelOneData.SceneHeight, 1920f, 270f, LevelOneData.Ground);

            Ui.Label(root, "Title", "МЕДИТАЦИЯ В СПЕШКЕ", 960f, 130f, 1800f, 120f, 84, Ink,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Label(root, "Subtitle", "превью-стенд механик — 4 сценки", 960f, 215f, 1800f, 60f, 38,
                new Color(0.45f, 0.45f, 0.42f));

            for (int i = 0; i < PreviewScenes.Scenettes.Length; i++)
            {
                float y = 350f + i * 150f;
                Image card = Ui.Rounded(root, "Card" + (i + 1), 960f, y, 1400f, 120f,
                    CardIdle, LevelOneData.VesselStroke, 4f, 16);
                _cards.Add(card);

                CentreIn(Ui.Label(card.transform, "CardTitle", PreviewScenes.Titles[i], 0f, 0f, 1300f, 50f,
                    44, Ink, TextAnchor.MiddleLeft, FontStyle.Bold), new Vector2(-620f, 22f));
                Text description = Ui.Label(card.transform, "CardText", PreviewScenes.Descriptions[i],
                    0f, 0f, 1300f, 40f, 30, DescriptionIdle, TextAnchor.MiddleLeft);
                CentreIn(description, new Vector2(-620f, -24f));
                _descriptions.Add(description);
            }

            Ui.Label(root, "Hint", "джойстик ↑ ↓ — выбор   ·   зелёная кнопка — открыть   ·   кнопка «в меню» — выход",
                960f, 985f, 1800f, 50f, 34, new Color(0.35f, 0.35f, 0.33f));
            Ui.Label(root, "HintKeyboard",
                "на ПК: стрелки = джойстик, колесо мыши = динамо, Enter = зелёная, Esc = «в меню»",
                960f, 1035f, 1800f, 44f, 26, new Color(0.5f, 0.5f, 0.48f));
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
            float y = ArcadeInput.Joystick.Vector.y;

            if (Mathf.Abs(y) < 0.5f)
            {
                _repeatTimer = 0f;
            }
            else
            {
                _repeatTimer -= Time.deltaTime;
                if (_repeatTimer <= 0f)
                {
                    Move(y > 0f ? -1 : 1);
                    _repeatTimer = RepeatDelay;
                }
            }

            bool green = ArcadeInput.GreenButton.IsHeld;
            if (green && !_greenWasHeld) Open();
            _greenWasHeld = green;
        }

        /// <summary>Move the highlight (wraps around).</summary>
        public void Move(int delta)
        {
            int count = PreviewScenes.Scenettes.Length;
            Selected = (Selected + delta % count + count) % count;
            Paint();
        }

        /// <summary>Open the highlighted scenette.</summary>
        public void Open()
        {
            PreviewStandNav.OpenScene(PreviewScenes.Scenettes[Selected]);
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
