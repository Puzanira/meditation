using Meditation.Mechanics;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Game
{
    /// <summary>
    /// S6 «Финал», after the last level: the designer's panorama with every location in one frame, and
    /// the game leaves on any input or after twenty seconds of quiet.
    ///
    /// The row of five vessels with their haul is gone — «сосуды в ряд больше не рисуем (в рендере их
    /// нет)» (SCREENS S6, walkthrough Э5), and so is the line «Ты заметил(а) всё. Даже в спешке.»,
    /// which the render replaces with its own. The haul now has exactly one place it is shown, and that
    /// is the reward beat of every level's victory — which is why that beat was kept.
    /// </summary>
    public sealed class FinaleScreen : GameScreen
    {
        /// <summary>Silence after which the cabinet takes its screen back (SCREENS S6).</summary>
        public const float IdleExitSeconds = 20f;

        /// <summary>Input in the first moment is the button-press that won the last level.</summary>
        private const float InputGraceSeconds = 1f;

        public FinaleScreen(GameFlow flow) : base(flow, "FinaleScreen")
        {
            Panorama = Ui.NewImage(Root, "FinaleScreenArt");
            Panorama.sprite = ArtLibrary.Get(ArtScreens.Finale);
            Panorama.color = Panorama.sprite != null ? Color.white : LevelOneData.Sky;
            Ui.Place(Panorama.rectTransform, 960f, 540f, 1920f, 1080f);

            // «Подпись про выход в основное меню» (founder, 2026-09-22, п.9). The finale leaves on any
            // input or after twenty seconds and said so nowhere: the player stood in front of a
            // panorama with no way to tell whether the game was over or stuck.
            ExitLabel = Shadowed("FinaleExitLabel", GameTexts.FinaleExit, ExitLabelY, ExitLabelPt);
            DeskLabel = Shadowed("FinaleExitDesk", GameTexts.FinaleExitOnDesk, DeskLabelY, DeskLabelPt);

            // …and the three mounts for the designer's animations on this screen (гусь с бубликами,
            // мышь, птички — бриф Кате 2026-09-22). Empty RectTransforms: the panorama is finished art
            // and the animations go ON it at places the render was composed around, so the places are
            // what this screen owes them.
            GooseAnchor = Anchor("GooseAnchor", new Vector2(430f, 700f), new Vector2(360f, 300f));
            MouseAnchor = Anchor("MouseAnchor", new Vector2(1180f, 905f), new Vector2(220f, 160f));
            BirdsAnchor = Anchor("BirdsAnchor", new Vector2(1480f, 210f), new Vector2(420f, 220f));
        }

        /// <summary>The render — the suite checks that the picture is really on screen.</summary>
        public Image Panorama { get; }

        /// <summary>«Красная кнопка — выход в главное меню».</summary>
        public Text ExitLabel { get; }

        /// <summary>…and its PC bracket, «(на компьютере — Esc)».</summary>
        public Text DeskLabel { get; }

        /// <summary>Mount for Катя's goose-with-bagels animation. Empty by design.</summary>
        public RectTransform GooseAnchor { get; }

        /// <summary>Mount for the mouse.</summary>
        public RectTransform MouseAnchor { get; }

        /// <summary>Mount for the birds.</summary>
        public RectTransform BirdsAnchor { get; }

        /// <summary>The caption's band, design px — the darkest strip the panorama leaves free.</summary>
        private const float ExitLabelY = 990f;
        private const float DeskLabelY = 1040f;
        private const int ExitLabelPt = 36;
        private const int DeskLabelPt = 24;

        private static readonly Color LabelInk = new Color(242f / 255f, 240f / 255f, 234f / 255f);
        private static readonly Color LabelShadow = new Color(0f, 0f, 0f, 0.78f);
        private const float ShadowOffset = 3f;

        /// <summary>
        /// A line drawn twice — a dark copy offset by <see cref="ShadowOffset"/>, then the light one
        /// over it. The same device the title uses, and for the same reason: this is a finished
        /// panorama nobody measured for a caption.
        /// </summary>
        private Text Shadowed(string name, string text, float y, int fontSize)
        {
            Ui.Label(Root, name + "Shadow", text, 960f + ShadowOffset, y + ShadowOffset,
                1600f, fontSize * 1.6f, fontSize, LabelShadow);
            return Ui.Label(Root, name, text, 960f, y, 1600f, fontSize * 1.6f, fontSize, LabelInk);
        }

        private RectTransform Anchor(string name, Vector2 centre, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Root, false);
            Ui.Place(rt, centre.x, centre.y, size.x, size.y);
            return rt;
        }

        public override AudioScene Audio => AudioScene.Quiet(LevelCatalog.Count - 1);

        protected override void Tick(float deltaTime, in Hands hands)
        {
            // The grace window keeps the very input that finished the last level from skipping the
            // finale before it has been seen.
            bool leaving = Age >= IdleExitSeconds || (Age > InputGraceSeconds && hands.AnyInput);
            if (leaving) Flow.ExitToLauncher();
        }

        public override string Readout()
        {
            return "экран: финал\n" +
                   "выход по любому вводу или через " +
                   Mathf.Max(0f, IdleExitSeconds - Age).ToString("0") + " с";
        }
    }
}
