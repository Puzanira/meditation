using UnityEngine;

namespace Meditation.View
{
    /// <summary>
    /// Resources keys of the finished screens and buttons of the drop 2026-08-07.
    ///
    /// One place, like <c>GameTexts</c> used to be one place for the strings — and for the same reason.
    /// Since this drop the screens ARE the text: «Медитация в спешке», «Уровень 3 · Метро», «Отлично!
    /// Ты был здесь и сейчас…» are pixels in these files, so a typo in a key is now what a typo in a
    /// line used to be, and the list of what the game is allowed to put on screen is this list.
    ///
    /// The renders arrived at 3840×2160 and were downscaled ×0.5 on the way into the project (SCREENS
    /// «S1–S6»); the buttons arrived at 2–3× their game size and are drawn at
    /// <see cref="ButtonHint.ButtonHeight"/>.
    /// </summary>
    public static class ArtScreens
    {
        // ---- S1 ----------------------------------------------------------------------------------
        public const string Title = "screens/title";
        public const string StartButton = "buttons/start";

        // ---- S2: one card per level, by level NUMBER (1-based, as the card itself says) ----------
        public static string CardOf(int levelNumber) =>
            "screens/level-" + Mathf.Clamp(levelNumber, 1, 5) + "-card";

        // ---- S4 / S5 / S6 ------------------------------------------------------------------------
        public const string LevelComplete = "screens/level-complete";
        public const string GameOver = "screens/game-over";
        public const string Finale = "screens/finale";

        // ---- Обучение (уровень 1) ----------------------------------------------------------------
        /// <summary>«НАВОДИ» — beat 1, the gaze.</summary>
        public const string ButtonAim = "buttons/navodi";

        /// <summary>«КРУТИ РУЧКУ» — beat 2, the dynamo.</summary>
        public const string ButtonCrank = "buttons/kruti-ruchku";

        /// <summary>«ТАЩИ» — beat 2, beside the detail on its way to the vessel.</summary>
        public const string ButtonDrag = "buttons/tashi";

        // …and there is deliberately no ButtonShake: the drop ships no «ТРЯСИ», the question is with
        // the designer, and until it comes back that beat is an arrow with no words
        // (ButtonHint.ShowArrowOnly).
    }
}
