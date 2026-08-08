namespace Meditation.Stand
{
    /// <summary>Scene names of the preview stand. The menu is the entry scene.</summary>
    public static class PreviewScenes
    {
        /// <summary>The GAME's entry scene — first in Build Settings, the one the launcher opens.</summary>
        public const string Game = "Game";

        public const string Menu = "PreviewMenu";
        public const string CrankCollect = "Scene1_CrankCollect";
        public const string ShakeAway = "Scene2_ShakeAway";
        public const string TwoHands = "Scene3_TwoHands";
        public const string FullLevel = "Scene4_FullLevel";

        public static readonly string[] All =
        {
            Menu, CrankCollect, ShakeAway, TwoHands, FullLevel
        };

        /// <summary>The four scenettes in the order MECHANICS.md §7 asks for them.</summary>
        public static readonly string[] Scenettes =
        {
            CrankCollect, ShakeAway, TwoHands, FullLevel
        };

        public static readonly string[] Titles =
        {
            "1 · Динамо-сбор",
            "2 · Отгон взмахами",
            "3 · Две руки",
            "4 · Уровень целиком"
        };

        public static readonly string[] Descriptions =
        {
            "одна деталь и сосуд, без мыслей — тюним кручение",
            "только мысли, без сбора — тюним взмахи над датчиками",
            "сбор и волны мыслей вместе — конфликт внимания",
            // Таймера нет с 2026-08-07 (решение founder) — карточка сценки обещала часы, которых
            // в игре не существует, а стенд судит ПРАВИЛА.
            "победа, поражение по заполнению, передышки"
        };
    }
}
