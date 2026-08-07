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
        }

        /// <summary>The render — the suite checks that the picture is really on screen.</summary>
        public Image Panorama { get; }

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
