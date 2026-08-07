namespace Meditation.Game
{
    /// <summary>
    /// The game's text registry — which since the drop of 2026-08-07 is a list of what the game must
    /// NOT say.
    ///
    /// This class used to hold every string the game showed, copied verbatim from
    /// <c>docs/design/gameplay-walkthrough.html</c> (frame 25 «Реестр текстов»), because the registry
    /// says «Других текстов в игре нет… Любой новый текст сначала добавляется сюда». The drop replaced
    /// the title, the five level cards and all three outcome screens with finished renders that carry
    /// their own typography, and the registry's own row «Выведено из игры» lists the lines that left
    /// with them. So the one-place rule survives, inverted: these are the strings the game is no longer
    /// allowed to render, and <c>GameFlowTests</c> walks every screen of the flow to prove none of them
    /// is on it.
    ///
    /// That is not pedantry. A withdrawn line does not disappear by being deleted from one screen — it
    /// comes back the next time somebody needs «a caption here», and it comes back in a font the
    /// designer did not choose, over a picture that already says it.
    ///
    /// The only text the game still renders itself is the level timer's «NN с», and that is a HUD
    /// readout with a box drawn under it in SCREENS «Зоны», not a line of copy.
    /// </summary>
    public static class GameTexts
    {
        /// <summary>
        /// «Выведено из игры» — walkthrough frame 25. Every one of these is now pixels in
        /// <c>арт/экраны/</c> or <c>арт/кнопки/</c>, drawn by the designer.
        /// </summary>
        public static readonly string[] Withdrawn =
        {
            "МЕДИТАЦИЯ В СПЕШКЕ",
            "Крути ручку, чтобы начать",
            "Мысли захватили всё. Вдохни.",
            "Крути ручку — попробуй снова",
            "Ты заметил(а) всё. Даже в спешке.",
            "Крути ручку!",
            "Тряси джойстик!",
            "Оглядись — наклони стик",
            "КРУТИ",
            "ТРЯСИ",
            "собирай детали",
            "отгоняй мысли"
        };

        /// <summary>
        /// «Собрано: …» took a setting's name, so it cannot be listed as a constant — this is the same
        /// line, recognisable by its stem.
        /// </summary>
        public const string WithdrawnCollectedPrefix = "Собрано:";

        /// <summary>«Уровень N» — the card's first line, now baked into the card's own render.</summary>
        public const string WithdrawnLevelPrefix = "Уровень ";
    }
}
