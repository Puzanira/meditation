using AiGameStudio.ArcadeControls;
using UnityEngine;

namespace Meditation.Stand
{
    /// <summary>
    /// The cabinet's service button, wired the way ARCADE_INTEGRATION_CONTRACT §5 asks: react to
    /// <c>ArcadeInput.MenuButton.Pressed</c> and leave immediately, with no confirmation and no text.
    ///
    /// It re-binds itself whenever <c>ArcadeInput.Initialize</c> hands out fresh control objects (a
    /// backend swap in a test, or a re-init at scene load) — otherwise the subscription would silently
    /// point at a dead ButtonControl and the exit contract would fail exactly when it matters.
    /// </summary>
    [AddComponentMenu("Meditation/Menu Button Exit")]
    [DisallowMultipleComponent]
    public sealed class MenuButtonExit : MonoBehaviour
    {
        private ButtonControl _bound;

        /// <summary>Set by tests / tools that want the press without a scene load.</summary>
        public bool ExitOnPress = true;

        /// <summary>
        /// Entry scene this button lands on. Empty = the preview stand's menu; the game's scene sets
        /// its own, so «в меню» is a clean restart of whichever product the player is inside.
        /// </summary>
        public string ExitScene = "";

        /// <summary>
        /// What the press actually does, when the screen owns its own way out. The game sets it: its
        /// exit is the cabinet's (hand the screen back to the launcher, tear the run down), and only
        /// its fallback is a scene load. Unset — the stand's plain navigation below.
        /// </summary>
        public System.Action ExitAction;

        /// <summary>How many presses this component has seen (test observability).</summary>
        public int PressCount { get; private set; }

        private void OnEnable() => Bind();

        private void Update()
        {
            // ArcadeInput.Initialize replaces the control instances; follow them.
            if (!ReferenceEquals(_bound, ArcadeInput.MenuButton)) Bind();
        }

        private void OnDisable() => Unbind();

        private void Bind()
        {
            Unbind();
            _bound = ArcadeInput.MenuButton;
            if (_bound != null) _bound.Pressed += OnPressed;
        }

        private void Unbind()
        {
            if (_bound == null) return;
            _bound.Pressed -= OnPressed;
            _bound = null;
        }

        private void OnPressed()
        {
            PressCount++;
            if (!ExitOnPress) return;

            if (ExitAction != null) ExitAction();
            else if (string.IsNullOrEmpty(ExitScene)) PreviewStandNav.ExitToMenu();
            else PreviewStandNav.ExitToScene(ExitScene);
        }
    }
}
