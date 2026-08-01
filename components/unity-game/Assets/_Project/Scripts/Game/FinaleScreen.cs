using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Game
{
    /// <summary>
    /// S6 «Финал», after the last level. The vessels of the run stand in a row with their haul inside,
    /// one line above them, and the game leaves on any input or after twenty seconds of quiet —
    /// «чистый выход в лаунчер» (SCREENS S6, walkthrough frame 23).
    /// </summary>
    public sealed class FinaleScreen : GameScreen
    {
        /// <summary>Silence after which the cabinet takes its screen back (SCREENS S6).</summary>
        public const float IdleExitSeconds = 20f;

        /// <summary>Input in the first moment is the button-press that won the last level.</summary>
        private const float InputGraceSeconds = 1f;

        /// <summary>Row of vessels at y = 540 (SCREENS S6).</summary>
        private const float RowY = 540f;

        /// <summary>
        /// Step between vessels, SCREENS S6: «все 5 сосудов в ряд по центру (шаг 340, y=540)».
        /// The row is centred on 960, so five vessels stand at 280 · 620 · 960 · 1300 · 1640 — and the
        /// step is clamped down if a longer row ever has to fit the frame.
        /// </summary>
        private const float RowStep = 340f;

        public FinaleScreen(GameFlow flow) : base(flow, "FinaleScreen")
        {
            Ui.BoxCentred(Root, "FinaleBackdrop", 960f, 540f, 1920f, 1080f, LevelOneData.Sky);

            Ui.Label(Root, "FinaleTitle", GameTexts.FinaleBig, 960f, 300f, 1800f, 100f,
                GameTexts.FinaleBigSize, new Color(0.23f, 0.23f, 0.21f));

            int count = LevelCatalog.Count;
            float step = Mathf.Min(RowStep, 1920f / (count + 0.4f));
            for (int i = 0; i < count; i++)
                BuildVessel(LevelCatalog.At(i), 960f + (i - (count - 1) * 0.5f) * step);

            Ui.Label(Root, "FinaleVessels", LevelCatalog.VesselWords(), 960f, 720f, 1800f, 60f,
                GameTexts.FinaleSmallSize, new Color(0.47f, 0.47f, 0.47f));
        }

        /// <summary>One vessel of the run with every detail of its level inside it.</summary>
        private void BuildVessel(LevelDefinition level, float centreX)
        {
            // The mock draws the row 150 px tall (frame 23), and with five vessels at a 340 step that
            // is also what keeps the widest of them — the briefcase — from touching its neighbour.
            const float height = 150f;
            float width = height * level.VesselSize.x / Mathf.Max(1f, level.VesselSize.y);

            var host = new GameObject("FinaleVessel" + level.Number, typeof(RectTransform));
            host.transform.SetParent(Root, false);
            var rt = (RectTransform)host.transform;
            Ui.Place(rt, centreX, RowY, width, height);

            // A vessel cut out of its plate (level 3's bag) travels here as a rectangle with hard
            // edges and a corner of the passenger's coat inside it. Standing in a row of five drawn
            // objects, that reads as a rendering bug — so the cut-out gets a rounded window.
            Transform vesselParent = rt;
            if (level.VesselIsBaked)
                vesselParent = Ui.RoundedMask(rt, "FinaleVesselWindow" + level.Number,
                    0f, 0f, width, height, BakedVesselCorner);

            var image = Ui.NewImage(vesselParent, "FinaleVesselArt" + level.Number);
            image.sprite = ArtLibrary.VesselOf(level);
            image.preserveAspect = true;
            image.color = image.sprite != null ? Color.white : new Color(0.62f, 0.6f, 0.55f);
            RectTransform art = image.rectTransform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = Vector2.zero;
            art.offsetMax = Vector2.zero;

            if (vesselParent != rt)
            {
                RectTransform window = (RectTransform)vesselParent;
                window.anchorMin = new Vector2(0.5f, 0.5f);
                window.anchorMax = new Vector2(0.5f, 0.5f);
                window.anchoredPosition = Vector2.zero;
                window.sizeDelta = new Vector2(width, height);
            }

            // The haul, laid out inside the vessel by the same rule the level uses (VesselHaul), so
            // nothing sits on the rim of the mug or beside the briefcase.
            int count = level.DetailCount;
            var items = new List<RectTransform>(count);
            var sceneSizes = new List<Vector2>(count);
            float shrink = height / Mathf.Max(1f, level.VesselSize.y);

            for (int i = 0; i < count; i++)
            {
                var detail = Ui.NewImage(rt, "FinaleDetail" + level.Number + "_" + i);
                // The icon, not the whole sprite — this row is the smallest a detail is ever drawn at,
                // and a ribbon fitted whole into one of these cells is a hair.
                detail.sprite = ArtLibrary.IconOf(level.Details[i]);
                detail.preserveAspect = true;
                detail.color = detail.sprite != null ? Color.white : Color.magenta;
                items.Add(detail.rectTransform);
                // The row draws vessels at a common height, so «не больше, чем в сцене» is measured
                // in the same shrunken units.
                sceneSizes.Add(LevelCatalog.IconSizeOf(level.Details[i]) * shrink);
            }

            // Same rule and the same measured silhouette the level uses: the row of five is where a
            // haul that ignores the vessel's shape shows up worst, five times over.
            VesselHaul.Layout(items, new Vector2(width, height), LevelCatalog.SolidOf(level), sceneSizes);
        }

        /// <summary>Corner radius of the rounded window a baked vessel travels in.</summary>
        private const int BakedVesselCorner = 18;

        protected override void Tick(float deltaTime, in Hands hands)
        {
            // The grace window keeps the very input that finished level 3 from skipping the finale
            // before it has been seen.
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
