using Meditation.Mechanics;

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

        // ---- прямой запуск уровней (заказ founder 2026-08-19) ------------------------------------

        /// <summary>
        /// How many «Уровень N» entries the menu offers — the game's own five, never a number of its
        /// own: an entry the catalogue does not have is a menu item that opens nothing.
        /// </summary>
        public static int LevelCount => LevelCatalog.Count;

        /// <summary>
        /// Title of the «Уровень N» entry, e.g. «Уровень 1 · Набережная».
        ///
        /// Built from <see cref="LevelCatalog"/> rather than typed out here — and NOT taken from
        /// <c>GameTexts</c>, which is the registry of what the GAME may render and lists «Уровень N»
        /// among the lines the drop withdrew (the level cards carry their own typography now). The
        /// stand is an instrument, not the game speaking to a player, and its own captions have always
        /// lived here beside <see cref="Titles"/>. Naming the levels off the catalogue is what keeps a
        /// renamed or reordered level from leaving a stale label on the stand.
        /// </summary>
        public static string LevelTitle(int index) =>
            "Уровень " + LevelCatalog.At(index).Number + " · " + LevelCatalog.At(index).Title;

        /// <summary>The one line under it — what this entry opens, and what it skips.</summary>
        public static string LevelDescription(int index) =>
            LevelCatalog.At(index).DetailCount + " деталей · сразу в игру, без титула и обучения";
    }
}
