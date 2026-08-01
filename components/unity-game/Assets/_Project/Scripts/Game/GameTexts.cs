namespace Meditation.Game
{
    /// <summary>
    /// Every string the game shows, copied verbatim from the text registry —
    /// <c>docs/design/gameplay-walkthrough.html</c>, frame 25 «Реестр текстов».
    ///
    /// One place on purpose: the registry says «Других текстов в игре нет… Любой новый текст сначала
    /// добавляется сюда», so a line that is not in this file is a line that was never approved.
    /// The font sizes travel with the strings because the registry fixes those too.
    /// </summary>
    public static class GameTexts
    {
        // ---- S1 Титул ---------------------------------------------------------------------------
        public const string Title = "МЕДИТАЦИЯ В СПЕШКЕ";
        public const int TitleSize = 100;

        public const string CrankCardTitle = "КРУТИ";
        public const string CrankCardSubtitle = "собирай детали";
        public const string ShakeCardTitle = "ТРЯСИ";
        public const string ShakeCardSubtitle = "отгоняй мысли";
        public const int CardTitleSize = 36;
        public const int CardSubtitleSize = 28;

        public const string TitleStartHint = "Крути ручку, чтобы начать";
        public const int TitleStartHintSize = 40;

        // ---- S2 Карточка уровня -----------------------------------------------------------------
        public const int LevelCardSize = 64;

        /// <summary>«Уровень N» — first line of the card.</summary>
        public static string LevelNumber(int number) => "Уровень " + number;

        // ---- Обучение (уровень 1) ---------------------------------------------------------------
        public const string TutorialCrank = "Крути ручку!";
        public const string TutorialShake = "Тряси джойстик!";
        public const string TutorialGaze = "Оглядись — наклони стик";
        public const int TutorialSize = 44;

        // ---- S4 Победа уровня -------------------------------------------------------------------
        public const int CollectedSize = 52;

        /// <summary>«Собрано: &lt;название сеттинга&gt;».</summary>
        public static string Collected(string setting) => "Собрано: " + setting;

        // ---- S5 Поражение -----------------------------------------------------------------------
        public const string DefeatBig = "Мысли захватили всё. Вдохни.";
        public const string DefeatSmall = "Крути ручку — попробуй снова";
        public const int DefeatBigSize = 60;
        public const int DefeatSmallSize = 40;

        // ---- S6 Финал ---------------------------------------------------------------------------
        public const string FinaleBig = "Ты заметил(а) всё. Даже в спешке.";
        public const int FinaleBigSize = 56;
        public const int FinaleSmallSize = 32;
    }
}
