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
    /// The inversion was ABSOLUTE for one day: between the drop and 2026-08-08 the game rendered no
    /// string of its own at all. Then the founder played it and could not tell how to start, nor what
    /// the arrow on the отгон beat was asking her to do — two questions the finished renders do not
    /// answer, because in both cases the answer is a GESTURE with a piece of furniture, and the
    /// designer's pictures name the hand, not the movement. So the registry is a registry again: a
    /// short list of lines the game is allowed to say, in <see cref="Live"/>, next to the long list of
    /// the ones it is not.
    ///
    /// The rule that produced <see cref="Withdrawn"/> is unchanged and now cuts both ways — a new line
    /// on a screen is added HERE first, and <c>GameFlowTests</c> walks the flow to prove that every
    /// string on it is one of these. Both of the new lines are temporary in the same way the отгон
    /// beat's missing button is temporary: when the designer sends drawn art for them they become
    /// pixels and move to <see cref="Withdrawn"/>.
    /// </summary>
    public static class GameTexts
    {
        /// <summary>
        /// «Маши над датчиком!» — the отгон beat of the tutorial (SCREENS §Обучение п.3).
        ///
        /// The beat has never had words: the drop ships НАВОДИ, КРУТИ РУЧКУ and ТАЩИ, and «ТРЯСИ» —
        /// which would have been the wrong verb anyway since the отгон moved to the height sensors —
        /// is still with the designer. What the player got instead was an arrow to the bottom edge of
        /// the screen, and an arrow at a piece of furniture is not an instruction (founder, 2026-08-08,
        /// playing the build). The same sentence is already the stand's own caption for this rule
        /// (<c>Scene2ShakeAway</c>), so this is one line in two places rather than two lines.
        /// </summary>
        public const string SwipeHint = "Маши над датчиком!";

        /// <summary>
        /// The title's «как начать» — under the drawn НАЧАТЬ button, which is a call and not a control.
        ///
        /// The wording follows the CODE: the run starts on two full turns of the dynamo
        /// (<see cref="TitleScreen.StartDegrees"/>), not on a button and not on any key. «Крути ручку,
        /// чтобы начать» is the withdrawn line and stays withdrawn — that one belonged to the greybox
        /// title and is baked into the render's own typography; this one names the amount, which is the
        /// part a player standing at the cabinet cannot guess from a bar that has not started moving.
        /// </summary>
        public const string TitleStart = "Раскрути ручку — два оборота";

        /// <summary>
        /// …and the second, smaller line: the same instruction for whoever is playing at a desk.
        ///
        /// The cabinet's crank is a mouse wheel on a PC (arcade-controls, <c>crankDegreesPerScrollUnit</c>),
        /// and there is no way to guess that from a picture of a handle. It is a bracket in every sense:
        /// smaller type, in brackets, and false on the machine this game ships to — which is why it is
        /// its own line and not a clause of the one above.
        /// </summary>
        public const string TitleStartOnDesk = "(на компьютере — колесо мыши)";

        /// <summary>
        /// The lines the game itself renders, in one array — what «сначала добавляется сюда» means in
        /// practice. <c>GameFlowTests</c> walks it against the screens the other way round from
        /// <see cref="Withdrawn"/>: each of these has to be FOUND on the screen that owns it, so a
        /// caption that quietly stops being drawn is a red test rather than a silent regression.
        ///
        /// It is not a whitelist of every string on the stage — the tuning panel is also a pile of
        /// UGUI text, and the panel is an instrument, not the game speaking to a player.
        /// </summary>
        public static readonly string[] Live =
        {
            SwipeHint,
            TitleStart,
            TitleStartOnDesk
        };

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
