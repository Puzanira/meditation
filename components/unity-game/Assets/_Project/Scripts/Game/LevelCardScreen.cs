using Meditation.Mechanics;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Game
{
    /// <summary>
    /// S2 «Карточка уровня» — 2.5 s, automatic: the designer's finished render for this level and
    /// nothing else on it.
    ///
    /// The card used to be assembled here — a dark plate, «Уровень 2» over the setting's name, the
    /// silhouette of the vessel scaled to a common height, and one empty slot per detail. All of it is
    /// in the picture now, promise and typography together, and SCREENS says the card is «показывается
    /// целиком, без наложенных силуэтов сосуда и пустых слотов (в рендере их нет)».
    ///
    /// It is still the screen a restart comes back through, so a failed level re-opens with its own
    /// name rather than dropping the player straight back into the noise.
    /// </summary>
    public sealed class LevelCardScreen : GameScreen
    {
        public const float HoldSeconds = 2.5f;

        private readonly LevelDefinition _level;

        public LevelCardScreen(GameFlow flow, LevelDefinition level) : base(flow, "LevelCardScreen")
        {
            _level = level;

            Card = Ui.NewImage(Root, "LevelCard");
            Card.sprite = ArtLibrary.Get(ArtScreens.CardOf(level.Number));
            Card.color = Card.sprite != null ? Color.white : LevelOneData.Hex("4b4844");
            Ui.Place(Card.rectTransform, 960f, 540f, 1920f, 1080f);
        }

        /// <summary>The render itself — the suite checks that this level's own card is the one up.</summary>
        public Image Card { get; }

        public override AudioScene Audio => AudioScene.Quiet(_level.Number - 1);

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
