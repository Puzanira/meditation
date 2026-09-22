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
    /// the dynamo — so this screen adds a bar under the button that fills as the two turns go in.
    ///
    /// …and, since 2026-08-08, the words that say WHICH two turns. The founder sat down in front of
    /// this screen and could not start the game: a drawn НАЧАТЬ over a bar that has not moved yet says
    /// «press me», and there is nothing here to press. The rule «поверх ничего не писать» was about not
    /// re-lettering a finished render, not about leaving a player stuck, so the line
    /// (<see cref="GameTexts.TitleStart"/>) goes where the render is empty — the road under the bar —
    /// and goes through the registry like any other string.
    ///
    /// ONE line, since 2026-09-22: a second, smaller one under it said «(на компьютере — колесо
    /// мыши)». «Из медитации срочно убрать все подсказки про клавиатуру — она играется на автомате»
    /// (founder). The bracket was a footnote for a desk, and this screen is never read at a desk.
    /// </summary>
    public sealed class TitleScreen : GameScreen
    {
        /// <summary>Two full turns of the crank start the game (SCREENS S1, walkthrough Э1).</summary>
        public const float StartDegrees = 720f;

        /// <summary>
        /// Where the drawn НАЧАТЬ button USED to sit, design px — measured off
        /// <c>экраны/Title/превью.png</c> (the render with the button) against
        /// <c>title-screen-no-button.png</c>: the difference is a 324×93 patch centred on (962, 864).
        ///
        /// The button is gone (founder, playtest 2026-09-22: «кнопку НАЧАТЬ убрать»). A drawn button on
        /// a cabinet that has nothing to press it with is an instruction to do the wrong thing, and it
        /// was doing exactly that — she stood in front of this screen and looked for the mouse.
        ///
        /// The PLACE stays, as <see cref="StartAnchor"/>: the render was composed with a hole in it at
        /// these coordinates, and the designer's animation of the handle (Катя, бриф 2026-09-22) is
        /// what goes into the hole. Until it arrives the anchor is an empty RectTransform, which is
        /// what an anchor should be.
        /// </summary>
        public static readonly Vector2 ButtonCentre = new Vector2(960f, 864f);

        /// <summary>Width the anchor (and the animation that will fill it) is laid out at, design px.</summary>
        public const float ButtonWidth = 320f;

        /// <summary>…and its height — the button's own 324×93 proportion at that width.</summary>
        public const float ButtonHeight = 92f;

        /// <summary>The fill bar under the anchor — its gap from it, design px.</summary>
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

            // The hole the button left, named and laid out but drawing nothing — see ButtonCentre.
            var anchor = new GameObject("StartAnimationAnchor", typeof(RectTransform));
            StartAnchor = (RectTransform)anchor.transform;
            StartAnchor.SetParent(Root, false);
            Ui.Place(StartAnchor, ButtonCentre.x, ButtonCentre.y, ButtonWidth, ButtonHeight);

            float barY = ButtonCentre.y + ButtonHeight * 0.5f + ProgressGap;
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

            StartLabel = Shadowed("StartLabel", GameTexts.TitleStart, StartLabelY, StartLabelPt);
        }

        /// <summary>
        /// Where the line stands, design px: under the fill bar, in the strip of road the render
        /// leaves empty between the bar (y ≈ 927) and the bottom edge.
        ///
        /// Not above the anchor, where the render's own horizon is: the sunset there runs from cream to
        /// orange and there is no ink colour that reads on both halves of it. The road below is the one
        /// large dark area of the picture — and even there the type is drawn twice (see
        /// <see cref="Shadowed"/>), because the founder's apples and her mug are down there too.
        ///
        /// The band used to hold two lines (983 and 1032); with the PC bracket withdrawn the
        /// instruction keeps its own line and the strip under it is road again.
        /// </summary>
        private const float StartLabelY = 983f;

        /// <summary>
        /// The instruction for the person standing at the cabinet, at the size it is read across a
        /// room from.
        /// </summary>
        private const int StartLabelPt = 40;

        /// <summary>The ink of both lines — the off-white the designer draws her own type in.</summary>
        private static readonly Color LabelInk = new Color(242f / 255f, 240f / 255f, 234f / 255f);

        /// <summary>…and the shadow under it, so the line survives whatever it happens to cross.</summary>
        private static readonly Color LabelShadow = new Color(0f, 0f, 0f, 0.75f);

        /// <summary>How far the shadow is offset, design px — down and to the right, one light source.</summary>
        private const float ShadowOffset = 3f;

        /// <summary>
        /// A line drawn twice: a dark copy offset by <see cref="ShadowOffset"/>, then the light one on
        /// top. It is the cheap way to put words over a picture nobody measured for them — the title's
        /// road is dark, but it also carries three apples, a flower and a mug, and a caption is not
        /// allowed to be legible only between them.
        /// </summary>
        private Text Shadowed(string name, string text, float y, int fontSize)
        {
            Ui.Label(Root, name + "Shadow", text, 960f + ShadowOffset, y + ShadowOffset,
                1600f, fontSize * 1.6f, fontSize, LabelShadow);
            return Ui.Label(Root, name, text, 960f, y, 1600f, fontSize * 1.6f, fontSize, LabelInk);
        }

        /// <summary>«Крути крутилку, чтобы начать» — the founder's own question, answered on screen.</summary>
        public Text StartLabel { get; }

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

        /// <summary>
        /// The place the НАЧАТЬ button occupied, empty and drawing nothing — the mount for the
        /// designer's handle animation. The suite asserts it is EMPTY, which is the whole of «кнопку
        /// убрать»: an anchor that quietly grew an Image again would be the button back.
        /// </summary>
        public RectTransform StartAnchor { get; }

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
