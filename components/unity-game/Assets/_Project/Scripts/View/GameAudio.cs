using Meditation.Mechanics;
using UnityEngine;

namespace Meditation.View
{
    /// <summary>
    /// The three looping tracks of MECHANICS §8 on three <c>AudioSource</c>s: the level's background,
    /// «медитация» and «мысли».
    ///
    /// All the thinking is in <see cref="AudioMix"/> — this class only owns the sources, swaps the
    /// background clip when the level changes, and copies three floats every frame. That is deliberate:
    /// batch mode has no audio device, so anything decided here would be untestable, while everything
    /// decided in the mix is arithmetic an EditMode test runs in microseconds.
    ///
    /// The tracks are heavy (18 MB for the library, 11 for the metro), so they are imported streaming
    /// and not preloaded (<c>AudioImportSettings</c>) — the level's own track is the only one that ever
    /// has to be swapped, and it is swapped once per level.
    /// </summary>
    public sealed class GameAudio
    {
        /// <summary>Resources key of a level's own background track, 1-based like the level number.</summary>
        public static string BackgroundKeyOf(int levelIndex) =>
            "audio/level-" + (Mathf.Clamp(levelIndex, 0, LevelCatalog.Count - 1) + 1);

        /// <summary>«Медитация» — the reward layer.</summary>
        public const string MeditationKey = "audio/meditation";

        /// <summary>«Мысли» — the interference layer, always on, loudest when the screen is full.</summary>
        public const string ThoughtsKey = "audio/thoughts";

        private readonly GameObject _host;
        private readonly AudioSource _background;
        private readonly AudioSource _meditation;
        private readonly AudioSource _thoughts;

        private int _loadedLevel = -1;

        public GameAudio(Transform parent)
        {
            _host = new GameObject("GameAudio");
            _host.transform.SetParent(parent, false);

            EnsureAListener();

            _background = MakeSource("Background", null);
            _meditation = MakeSource("Meditation", ArtLibrary.Clip(MeditationKey));
            _thoughts = MakeSource("Thoughts", ArtLibrary.Clip(ThoughtsKey));
        }

        public AudioMix Mix { get; } = new AudioMix();

        /// <summary>The three sources, for the suite: it checks clips and volumes, not the speakers.</summary>
        public AudioSource BackgroundSource => _background;
        public AudioSource MeditationSource => _meditation;
        public AudioSource ThoughtsSource => _thoughts;

        /// <summary>
        /// A scene with sources and no listener is a scene Unity logs about on the first note played,
        /// and the game's own scene had no listener because until this drop it had no sound.
        ///
        /// Put here rather than on the camera in the scene builder because it belongs to the sound, not
        /// to the picture: the day this game is hosted inside the arcade hub, the camera is the hub's
        /// and the listener still has to exist for exactly as long as the game is making noise.
        /// </summary>
        private void EnsureAListener()
        {
            if (Object.FindAnyObjectByType<AudioListener>() != null) return;
            _host.AddComponent<AudioListener>();
        }

        private AudioSource MakeSource(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_host.transform, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;               // §8: «три слоя, все зациклены»
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = 0f;         // a cabinet speaker, not a point in a 3-D world
            if (clip != null) source.Play();
            return source;
        }

        /// <summary>
        /// Advance the mix and put it on the sources. The background clip follows the mix's own idea of
        /// which level is current, so a level change is one clip swap and never a gap in the other two.
        /// </summary>
        public void Tick(float deltaTime, in AudioScene scene)
        {
            Mix.Tick(deltaTime, scene);

            if (Mix.BackgroundLevelIndex != _loadedLevel)
            {
                _loadedLevel = Mix.BackgroundLevelIndex;
                AudioClip clip = ArtLibrary.Clip(BackgroundKeyOf(_loadedLevel));
                if (clip != _background.clip)
                {
                    _background.Stop();
                    _background.clip = clip;
                    if (clip != null) _background.Play();
                }
            }

            Apply(_background, Mix.Background);
            Apply(_meditation, Mix.Meditation);
            Apply(_thoughts, Mix.Thoughts);
        }

        /// <summary>
        /// «Кнопка „в меню“ — мгновенная тишина вместе с выходом» (§8). Not a fade and not a volume of
        /// zero on a source that keeps running: the sources STOP, so nothing of this run is still
        /// decoding behind the launcher.
        /// </summary>
        public void SilenceNow()
        {
            Mix.SilenceNow();
            Stop(_background);
            Stop(_meditation);
            Stop(_thoughts);
        }

        public void Dispose()
        {
            SilenceNow();
            if (_host != null) Object.Destroy(_host);
        }

        private static void Apply(AudioSource source, float volume)
        {
            if (source == null) return;
            source.volume = Mathf.Clamp01(volume);
            if (source.clip != null && !source.isPlaying && source.volume > 0f) source.Play();
        }

        private static void Stop(AudioSource source)
        {
            if (source == null) return;
            source.volume = 0f;
            source.Stop();
        }
    }
}
