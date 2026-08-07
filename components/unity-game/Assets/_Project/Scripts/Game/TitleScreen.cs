using Meditation.Mechanics;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Game
{
    /// <summary>
    /// S1 «Титул» — the designer's finished render (<c>screens/title</c>) with the drawn НАЧАТЬ button
    /// on it, and nothing of ours on top.
    ///
    /// The whole screen used to be built here: the street of level 1 with four thoughts drifting over
    /// it, a veil to make type readable, the game's name at 100 pt, two pictogram cards for the two
    /// hands, and the line «Крути ручку, чтобы начать». All of it is withdrawn with the drop of
    /// 2026-08-07 (walkthrough «Реестр текстов», строка «Выведено из игры») — the title is a picture
    /// with its own typography, its own emblem and its own instruction baked in, and the rule for this
    /// drop is «поверх ничего не писать».
    ///
    /// What is NOT in the picture is the answer to «did the machine hear me». The button is a call, not
    /// a control — the cabinet has no mouse (CABINET_BRIEF) and the run still starts on two turns of
    /// the dynamo — so the one thing this screen adds is a bar under the button that fills as the two
    /// turns go in. It carries no words, which is the point: it is feedback, not text.
    /// </summary>
    public sealed class TitleScreen : GameScreen
    {
        /// <summary>Two full turns of the crank start the game (SCREENS S1, walkthrough Э1).</summary>
        public const float StartDegrees = 720f;

        /// <summary>
        /// Where the drawn button sits, design px — measured off <c>экраны/Title/превью.png</c>
        /// (the render with the button) against <c>title-screen-no-button.png</c>: the difference is a
        /// 324×93 patch centred on (962, 864). Drawn at 320 px wide with the button's own aspect.
        /// </summary>
        public static readonly Vector2 ButtonCentre = new Vector2(960f, 864f);

        /// <summary>Width the button is drawn at, design px; the height follows its own proportions.</summary>
        public const float ButtonWidth = 320f;

        /// <summary>The fill bar under the button — its top edge, design px.</summary>
        private const float ProgressGap = 18f;

        private const float ProgressHeight = 10f;

        private float _turned;

        public TitleScreen(GameFlow flow) : base(flow, "TitleScreen")
        {
            var background = Ui.NewImage(Root, "TitleBackground");
            background.sprite = ArtLibrary.Get(ArtScreens.Title);
            // A missing render must not look like a level that failed to load.
            background.color = background.sprite != null ? Color.white : LevelOneData.Sky;
            Ui.Place(background.rectTransform, 960f, 540f, 1920f, 1080f);
            Background = background;

            StartButton = Ui.NewImage(Root, "StartButton");
            StartButton.sprite = ArtLibrary.Get(ArtScreens.StartButton);
            StartButton.preserveAspect = true;
            StartButton.color = StartButton.sprite != null ? Color.white : new Color(0.56f, 0.79f, 0.79f);

            float height = StartButton.sprite != null && StartButton.sprite.rect.width > 1f
                ? ButtonWidth * StartButton.sprite.rect.height / StartButton.sprite.rect.width
                : 89f;
            Ui.Place(StartButton.rectTransform, ButtonCentre.x, ButtonCentre.y, ButtonWidth, height);

            float barY = ButtonCentre.y + height * 0.5f + ProgressGap;
            Ui.Rounded(Root, "StartProgressTrack", ButtonCentre.x, barY, ButtonWidth, ProgressHeight,
                new Color(1f, 1f, 1f, 0.16f), Color.clear, 0f, 5);

            // The bar grows from the left by its WIDTH rather than by Image.Type.Filled: the rounded
            // sprite is nine-sliced, and a sliced sprite in a filled image is a warning per frame.
            Progress = Ui.Rounded(Root, "StartProgress", ButtonCentre.x, barY, ButtonWidth,
                ProgressHeight, ButtonGlow, Color.clear, 0f, 5);
            _progressRect = Progress.rectTransform;
            _progressRect.pivot = new Vector2(0f, 0.5f);
            _progressRect.anchoredPosition =
                new Vector2(ButtonCentre.x - ButtonWidth * 0.5f, -barY);
            SetProgress(0f);
        }

        private readonly RectTransform _progressRect;

        private void SetProgress(float progress01)
        {
            Progress01 = Mathf.Clamp01(progress01);
            _progressRect.sizeDelta = new Vector2(ButtonWidth * Progress01, ProgressHeight);
        }

        /// <summary>The turquoise of the drop's buttons — the glowing border of <c>title-start-button</c>.</summary>
        private static readonly Color ButtonGlow = new Color(0.60f, 0.86f, 0.86f, 0.95f);

        /// <summary>The finished render behind everything — the suite asserts the picture is really up.</summary>
        public Image Background { get; }

        /// <summary>The drawn НАЧАТЬ button. Not clickable: on the cabinet there is nothing to click with.</summary>
        public Image StartButton { get; }

        /// <summary>How much of the two turns is in, as a bar the player can watch.</summary>
        public Image Progress { get; }

        /// <summary>…and the same number, 0..1 — what the suite reads instead of a fill amount.</summary>
        public float Progress01 { get; private set; }

        /// <summary>Degrees turned so far, capped at the two turns that start the game.</summary>
        public float Turned => _turned;

        public override AudioScene Audio =>
            // «На экранах S1/S2/S4/S5/S6 фон уровня не играет [toggle], старт — тишина, КРОМЕ титула»
            // (MECHANICS §8): the title is the one screen off a level that keeps its music.
            new AudioScene(false, 0, true, false, 0, 0f);

        protected override void Tick(float deltaTime, in Hands hands)
        {
            // Direction does not matter — turning the handle is turning the handle.
            _turned = Mathf.Min(StartDegrees, _turned + Mathf.Abs(hands.CrankDelta));
            SetProgress(_turned / StartDegrees);

            if (_turned >= StartDegrees) Flow.StartRun();
        }

        public override string Readout()
        {
            return "экран: титул\n" +
                   "оборотов: " + (_turned / 360f).ToString("0.00") + " / 2\n" +
                   "старт по двум оборотам динамо";
        }
    }
}
