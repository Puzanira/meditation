using Meditation.Mechanics;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Game
{
    /// <summary>
    /// S2 «Карточка уровня» — 2.5 s, automatic. Dark plate, the level's name, the silhouette of the
    /// vessel you will fill and one empty slot per detail: what is about to be asked of you, and
    /// nothing else. Also the screen a restart comes back through, so a failed level always re-opens
    /// with its own name rather than dropping the player straight back into the noise.
    /// </summary>
    public sealed class LevelCardScreen : GameScreen
    {
        public const float HoldSeconds = 2.5f;

        private static readonly Color Backdrop = LevelOneData.Hex("4b4844");
        private static readonly Color Ink = LevelOneData.Hex("f2efe8");

        private readonly LevelDefinition _level;

        public LevelCardScreen(GameFlow flow, LevelDefinition level) : base(flow, "LevelCardScreen")
        {
            _level = level;

            Ui.BoxCentred(Root, "CardBackdrop", 960f, 540f, 1920f, 1080f, Backdrop);

            // Two lines, as the mock draws them (frame 2) and as the registry writes them:
            // «Уровень 1 / Улица».
            Ui.Label(Root, "CardNumber", GameTexts.LevelNumber(level.Number), 960f, 430f, 1600f, 90f,
                GameTexts.LevelCardSize, Ink);
            Ui.Label(Root, "CardTitle", level.Title, 960f, 520f, 1600f, 90f,
                GameTexts.LevelCardSize, Ink);

            BuildVesselSilhouette();
            BuildEmptySlots();
        }

        /// <summary>
        /// The vessel of this level. SCREENS S2 puts the silhouette at (960, 620), but the card's own
        /// second line is 64 pt at y 520 — at that centre a 150 px vessel runs into the setting's name.
        /// The walkthrough's own frame 2 draws the rectangle at 580–730, so the mock's centre (655) is
        /// the one used: same document, the version that was actually looked at.
        /// </summary>
        private void BuildVesselSilhouette()
        {
            Sprite sprite = ArtLibrary.VesselOf(_level);

            // Scaled to a common height so a tall backpack and a wide briefcase read as the same
            // kind of promise.
            float height = 150f;
            float width = sprite != null
                ? height * sprite.rect.width / Mathf.Max(1f, sprite.rect.height)
                : height * _level.VesselSize.x / Mathf.Max(1f, _level.VesselSize.y);

            // Level 3's bag is cut out of its plate; on the card it stands alone against a dark
            // backdrop, where a raw rectangle shows its edges and a corner of the passenger's coat.
            Transform parent = Root;
            if (_level.VesselIsBaked)
                parent = Ui.RoundedMask(Root, "CardVesselWindow", 960f, 655f, width, height, 18);

            var vessel = Ui.NewImage(parent, "CardVessel");
            vessel.sprite = sprite;
            vessel.preserveAspect = true;
            vessel.color = sprite != null ? Color.white : new Color(0.65f, 0.62f, 0.56f);
            Ui.Place(vessel.rectTransform, 960f, 655f, width, height);

            if (parent != Root)
            {
                RectTransform rt = vessel.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        /// <summary>One empty slot per detail of this level (SCREENS S2 «пустые слоты деталей»).</summary>
        private void BuildEmptySlots()
        {
            int count = _level.DetailCount;
            const float size = 60f;
            const float step = 76f;
            float left = 960f - (count - 1) * step * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Ui.Rounded(Root, "CardSlot" + (i + 1), left + i * step, 820f, size, size,
                    Backdrop, new Color(0.6f, 0.6f, 0.6f), 3f, 8);
            }
        }

        protected override void Tick(float deltaTime, in Hands hands)
        {
            if (Age >= HoldSeconds) Flow.LevelCardFinished();
        }

        public override string Readout()
        {
            return "экран: карточка уровня " + _level.Number + " · " + _level.Title + "\n" +
                   "деталей: " + _level.DetailCount + "\n" +
                   "автопереход через " + Mathf.Max(0f, HoldSeconds - Age).ToString("0.0") + " с";
        }
    }
}
