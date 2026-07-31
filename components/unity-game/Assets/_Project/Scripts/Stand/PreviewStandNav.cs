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
        /// <summary>Raised the moment the player asks to leave — the hub's future hook.</summary>
        public static event Action ExitRequested;

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
            ExitRequested?.Invoke();
            SceneManager.LoadScene(PreviewScenes.Menu);
        }
    }
}
