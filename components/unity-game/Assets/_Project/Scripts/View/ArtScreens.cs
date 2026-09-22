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
    /// «S1–S6»).
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
        //
        // Nothing. The drop's three teaching buttons — «НАВОДИ» (buttons/navodi), «КРУТИ РУЧКУ»
        // (buttons/kruti-ruchku) and «ТАЩИ» (buttons/tashi) — left the teaching screens on 2026-09-22
        // by the founder's own word: «убрать стрелки все с экранов обучений, оставить только плашки с
        // нашим текстом последним, остальные элементы подсказок убрать». The PNGs stay in Resources,
        // because an asset that has been paid for and drawn is not deleted by a change of mind about
        // where it stands; there is simply nothing in the game that names them any more, and this
        // list is what the game is allowed to put on screen.
        //
        // (There was never a ButtonShake — the отгон beat's «ТРЯСИ» was still with the designer when
        // the whole set was withdrawn.)
    }
}
