using UnityEditor;

namespace Meditation.EditorTools
{
    /// <summary>
    /// Kept as a forwarder to <see cref="GameSceneBuilder"/>.
    ///
    /// The stand's five scenes are no longer built on their own: since the game exists, Build Settings
    /// has to list <c>Game.unity</c> FIRST (it is the entry scene the launcher opens), and a builder
    /// that knew only about the stand would quietly rewrite the list without it. One builder owns all
    /// six scenes; this type survives only because the old menu item and the headless
    /// <c>-executeMethod</c> line are written down in the architecture doc and in people's shells.
    /// </summary>
    public static class PreviewStandSceneBuilder
    {
        [MenuItem("Meditation/Rebuild Preview Stand Scenes")]
        public static void Rebuild() => GameSceneBuilder.Rebuild();
    }
}
