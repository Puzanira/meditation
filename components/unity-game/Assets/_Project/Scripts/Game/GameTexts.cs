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
        // ---- Титул (S1) ---------------------------------------------------------------------------

        /// <summary>
        /// The title's «как начать» — and, since the founder's playtest of 2026-09-22, the only thing
        /// on that screen besides the render itself.
        ///
        /// Her two orders of that day are one order: «кнопку НАЧАТЬ убрать» and «вместо неё — „Крути
        /// ручку, чтобы начать“». A drawn button on a cabinet with nothing to press it with is an
        /// instruction to do the wrong thing, and it was answering the question badly enough that the
        /// line under it («Раскрути ручку — два оборота») was reading as a caption for the button
        /// rather than as the instruction. So the button goes and the sentence takes its place.
        ///
        /// The wording is hers, and it is the line the greybox title carried before the drop — it went
        /// into <see cref="Withdrawn"/> on 2026-08-07 because the render was thought to say it, and
        /// two playtests have now established that it does not. «Раскрути ручку — два оборота» swaps
        /// places with it: the AMOUNT is what the fill bar under the words shows, and it did not need
        /// saying twice.
        ///
        /// «КРУТИЛКА», not «ручка», since later the same day — the founder's own correction while
        /// playing. It is not a synonym swap: the cabinet's panel HAS a name for each of its controls
        /// (<c>system/CONTROLS_BRIEF.md</c> — «колесо мыши = крутилка», Q/A = датчик, red button = в
        /// меню), the player is standing in front of that panel, and a game that calls the крутилка a
        /// ручка is naming a thing the room does not have. Every live line that named it was changed
        /// at once; the old wordings are in <see cref="Withdrawn"/>.
        /// </summary>
        public const string TitleStart = "Крути крутилку, чтобы начать";

        /// <summary>
        /// …and the second, smaller line: the same instruction for whoever is playing at a desk.
        ///
        /// The cabinet's crank is a mouse wheel on a PC (arcade-controls, <c>crankDegreesPerScrollUnit</c>),
        /// and there is no way to guess that from a picture of a handle. It is a bracket in every sense:
        /// smaller type, in brackets, and false on the machine this game ships to — which is why it is
        /// its own line and not a clause of the one above.
        /// </summary>
        public const string TitleStartOnDesk = "(на компьютере — колесо мыши)";

        // ---- Обучение, уровень 1 (SCREENS §Обучение) ------------------------------------------------
        //
        // Four sentences, dictated by the founder at the playtest of 2026-09-22. They are not captions
        // for the drawn buttons — НАВОДИ, КРУТИ РУЧКУ and ТАЩИ name the HAND, in one word, standing
        // next to the thing the hand acts on, and that is all a drawn button can do. What the founder
        // could not work out from them is what the game is ASKING FOR: «наводи» on what, and why.
        //
        // The tone is hers too: «в тоне медитации, без клавиатурного жаргона». Nothing here names a
        // key; the PC bracket under each line is a separate, smaller string for exactly that reason
        // (see TitleStartOnDesk — false on the machine this ships to).

        /// <summary>Бит 1, «наведение»: the gaze, and what it is for.</summary>
        public const string BeatAim = "Наводи джойстиком на объект";

        /// <summary>…its PC bracket. The joystick is the arrow keys at a desk.</summary>
        public const string BeatAimOnDesk = "(на компьютере — стрелки)";

        /// <summary>
        /// Бит 2, «сбор» — one sentence for both hands, because the beat is both hands at once
        /// (SCREENS §Обучение п.2 shows two buttons: КРУТИ РУЧКУ and ТАЩИ).
        /// </summary>
        public const string BeatCollect = "Замечай детали вокруг. Крути крутилку и тащи объект";

        /// <summary>…its PC bracket.</summary>
        public const string BeatCollectOnDesk = "(на компьютере — колесо мыши)";

        /// <summary>
        /// Бит 3, «отгон» — the beat with no drawn button at all (the drop ships no «ТРЯСИ», and it
        /// would be the wrong verb anyway since the отгон moved to the height sensors).
        ///
        /// It used to be «Маши над датчиком!», which named the gesture and not the thing: the founder,
        /// playing on 2026-09-22, said the blobs need introducing — «это навязчивые мысли» is the half
        /// of the sentence that makes the other half worth doing. The stand's own caption for the same
        /// rule (<c>Scene2ShakeAway</c>) keeps the short form: the scenette is a rig for the RULE and
        /// its frame has no room for a sentence.
        /// </summary>
        public const string SwipeHint = "Это навязчивые мысли. Маши рукой над датчиком, чтобы отогнать их";

        /// <summary>…its PC bracket — the two height sensors are two keys at a desk.</summary>
        public const string SwipeHintOnDesk = "(на компьютере — Q и A)";

        /// <summary>
        /// Бит 4, «весь уровень» — new on 2026-09-22, and the only beat that is not about one hand.
        ///
        /// The lesson used to end when the first thought was beaten off, which taught three verbs and
        /// never said what they were FOR. This is the sentence that says it, and it is up while the
        /// level is already being played: nothing is blocked, nothing is scripted, it is the goal
        /// written over the game the player has just been given.
        /// </summary>
        public const string BeatWhole =
            "Заметь все объекты, перетащи их в ведёрко и не дай мыслям помешать тебе";

        // ---- Финал (S6) ------------------------------------------------------------------------------

        /// <summary>
        /// «Подпись про выход в основное меню» (founder, 2026-09-22, п.9).
        ///
        /// The finale leaves on any input or after twenty seconds, and until now it said so nowhere:
        /// the player stood in front of a panorama with no idea whether the game was over or stuck.
        /// The wording names the BUTTON on the panel — the founder's own framing, «формулировка под
        /// красную кнопку пульта» — rather than the action, because on the cabinet the red «в меню»
        /// button is a thing you can see from where you stand.
        /// </summary>
        public const string FinaleExit = "Красная кнопка — выход в главное меню";

        /// <summary>…and its PC bracket, the only place a key is named anywhere in the registry.</summary>
        public const string FinaleExitOnDesk = "(на компьютере — Esc)";

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
            TitleStart,
            TitleStartOnDesk,
            BeatAim,
            BeatAimOnDesk,
            BeatCollect,
            BeatCollectOnDesk,
            SwipeHint,
            SwipeHintOnDesk,
            BeatWhole,
            FinaleExit,
            FinaleExitOnDesk
        };

        /// <summary>
        /// «Выведено из игры» — walkthrough frame 25. Every one of these is now pixels in
        /// <c>арт/экраны/</c> or <c>арт/кнопки/</c>, drawn by the designer — or, for the last two,
        /// a line the founder replaced with a better one.
        /// </summary>
        public static readonly string[] Withdrawn =
        {
            "МЕДИТАЦИЯ В СПЕШКЕ",
            "Мысли захватили всё. Вдохни.",
            "Крути ручку — попробуй снова",
            "Ты заметил(а) всё. Даже в спешке.",
            "Крути ручку!",
            "Тряси джойстик!",
            "Оглядись — наклони стик",
            "КРУТИ",
            "ТРЯСИ",
            "собирай детали",
            "отгоняй мысли",

            // Withdrawn 2026-09-22, both by the founder's own word at the playtest.
            // «Раскрути ручку — два оборота» said the amount the fill bar already draws, and it said it
            // as a caption under a button that has itself been removed.
            "Раскрути ручку — два оборота",
            // «Маши над датчиком!» named the gesture without ever naming the thing — see SwipeHint.
            "Маши над датчиком!",

            // Withdrawn 2026-09-22 (later the same day): the panel's own word for the control is
            // «крутилка» (system/CONTROLS_BRIEF.md), and these are the wordings that called it a
            // ручка. Both were LIVE lines until this round, so they are exactly the kind of string
            // this list exists for — the one that comes back the next time somebody needs «a caption
            // about the handle». See TitleStart.
            "Крути ручку, чтобы начать",
            "Замечай детали вокруг. Крути ручку и тащи объект"
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
