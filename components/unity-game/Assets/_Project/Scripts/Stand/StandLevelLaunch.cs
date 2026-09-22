namespace Meditation.Stand
{
    /// <summary>
    /// The stand's request to the game: «open level N directly, as a level, and come back here».
    ///
    /// Asked for by the founder on 2026-08-19 — «надо запускать уровни сценками по отдельности, для
    /// дебага и разработки». The four scenettes of MECHANICS §7 are greybox rigs that prove RULES; a
    /// level is art plus rules plus three controllers, and the only way to look at one used to be to
    /// play the run from the title down to it.
    ///
    /// A static hand-off rather than a parameter, because the two sides are two SCENES: the menu
    /// loads <see cref="PreviewScenes.Game"/> and disappears with its own scene, and
    /// <c>GameFlow.Awake</c> in the next scene is the first code that could receive anything. It is
    /// consumed (<see cref="Take"/>) rather than read, so exactly one boot of the game acts on it —
    /// the cabinet's launcher opens the same scene with nothing pending and gets the title, which is
    /// the shipped path and must stay untouched by a debug affordance.
    /// </summary>
    public static class StandLevelLaunch
    {
        /// <summary>«Nothing pending» — the value of every boot that did not come from the stand.</summary>
        public const int None = -1;

        /// <summary>0-based index of the level the stand asked for, or <see cref="None"/>.</summary>
        public static int PendingLevelIndex { get; private set; } = None;

        public static bool IsPending => PendingLevelIndex >= 0;

        /// <summary>Open the game scene straight on level <paramref name="levelIndex"/> (0-based).</summary>
        public static void Open(int levelIndex)
        {
            PendingLevelIndex = levelIndex;
            PreviewStandNav.OpenScene(PreviewScenes.Game);
        }

        /// <summary>Read the request and clear it — a boot acts on it once and never again.</summary>
        public static int Take()
        {
            int pending = PendingLevelIndex;
            PendingLevelIndex = None;
            return pending;
        }

        /// <summary>Drop a request nobody picked up (tests, and the stand's own «в меню»).</summary>
        public static void Clear() => PendingLevelIndex = None;
    }
}
