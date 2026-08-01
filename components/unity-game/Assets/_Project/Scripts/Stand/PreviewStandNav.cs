using System;
using UnityEngine.SceneManagement;

namespace Meditation.Stand
{
    /// <summary>
    /// Navigation of the stand, and the one place the cabinet's "в меню" contract is honoured.
    ///
    /// ARCADE_INTEGRATION_CONTRACT §5: the game must not own the process — no Application.Quit, no
    /// DontDestroyOnLoad leftovers. So "clean exit" here means: drop everything and load the entry
    /// scene, which is exactly what the launcher will observe when it takes the screen back. Until the
    /// hub exists, <see cref="ExitRequested"/> is the seam it will subscribe to.
    /// </summary>
    public static class PreviewStandNav
    {
        /// <summary>Raised the moment the player asks to leave — the launcher's hook.</summary>
        public static event Action ExitRequested;

        /// <summary>Is anyone (the launcher, or a test standing in for it) listening for the exit?</summary>
        public static bool ExitIsHandled => ExitRequested != null;

        /// <summary>
        /// The cabinet's exit (ARCADE_INTEGRATION_CONTRACT §5): hand the screen back to whoever started
        /// the game and stop. Returns false when nobody is listening — the standalone/editor case, where
        /// there is no launcher to hand anything to and the caller falls back on its own explicit path.
        ///
        /// Splitting this out is the point: the game used to «exit» by reloading its own entry scene,
        /// which on the cabinet would have restarted the game instead of leaving it.
        /// </summary>
        public static bool RequestExit()
        {
            Action handler = ExitRequested;
            if (handler == null) return false;
            handler();
            return true;
        }

        /// <summary>Open a scenette from the stand menu.</summary>
        public static void OpenScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// "В меню" from anywhere: from a scenette it lands in the stand menu, from the menu it is a
        /// clean reload of the entry scene. Either way every timer, coroutine and object of the
        /// previous screen is gone — a single-mode LoadScene tears the whole scene down.
        /// </summary>
        public static void ExitToMenu()
        {
            ExitToScene(PreviewScenes.Menu);
        }

        /// <summary>
        /// The same clean exit, aimed at a named entry scene. The game's «в меню» lands on
        /// <see cref="PreviewScenes.Game"/> and the stand's on its own menu, but it is one act either
        /// way: raise <see cref="ExitRequested"/> for the future hub, then tear the current scene down
        /// by loading the entry scene in Single mode.
        /// </summary>
        public static void ExitToScene(string sceneName)
        {
            RequestExit();
            ReloadEntryScene(sceneName);
        }

        /// <summary>
        /// The fallback exit, and the stand's own «в меню»: tear the current scene down by loading the
        /// entry scene in Single mode. On the cabinet this is never the exit — it is what happens when
        /// no launcher is listening, i.e. in the editor and in a standalone build of the game alone.
        /// </summary>
        public static void ReloadEntryScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
