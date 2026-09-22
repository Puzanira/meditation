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

        /// <summary>
        /// The second background source — the track a level change is fading OUT of (founder,
        /// 2026-09-22, п.10). Two sources rather than one, because a crossfade is by definition two
        /// clips sounding at once and an <c>AudioSource</c> plays one.
        /// </summary>
        private readonly AudioSource _backgroundOut;
        private readonly AudioSource _meditation;
        private readonly AudioSource _thoughts;

        private int _loadedLevel = -1;
        private int _loadedOutLevel = AudioMix.NoTrack;

        public GameAudio(Transform parent)
        {
            _host = new GameObject("GameAudio");
            _host.transform.SetParent(parent, false);

            EnsureAListener();

            _background = MakeSource("Background", null);
            _backgroundOut = MakeSource("BackgroundOut", null);
            _meditation = MakeSource("Meditation", ArtLibrary.Clip(MeditationKey));
            _thoughts = MakeSource("Thoughts", ArtLibrary.Clip(ThoughtsKey));
        }

        public AudioMix Mix { get; } = new AudioMix();

        /// <summary>The sources, for the suite: it checks clips and volumes, not the speakers.</summary>
        public AudioSource BackgroundSource => _background;

        /// <summary>The outgoing track of a level change — silent and clipless outside one.</summary>
        public AudioSource BackgroundOutSource => _backgroundOut;
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

            // A level change hands the CURRENT source over to the outgoing one and loads the new track
            // next to it, so both are audible while the mix crossfades them. The source objects never
            // swap roles — the clips move — because everything that reads this class by name
            // (`BackgroundSource`) means «the track of the level we are on».
            if (Mix.BackgroundLevelIndex != _loadedLevel)
            {
                if (_loadedLevel >= 0 && Mix.SwappingTracks)
                {
                    _loadedOutLevel = _loadedLevel;
                    _backgroundOut.Stop();
                    _backgroundOut.clip = _background.clip;
                    _backgroundOut.timeSamples = SafeSamples(_background);
                    if (_backgroundOut.clip != null) _backgroundOut.Play();
                }

                _loadedLevel = Mix.BackgroundLevelIndex;
                AudioClip clip = ArtLibrary.Clip(BackgroundKeyOf(_loadedLevel));
                if (clip != _background.clip)
                {
                    _background.Stop();
                    _background.clip = clip;
                    if (clip != null) _background.Play();
                }
            }

            if (!Mix.SwappingTracks && _loadedOutLevel != AudioMix.NoTrack)
            {
                _loadedOutLevel = AudioMix.NoTrack;
                Stop(_backgroundOut);
                _backgroundOut.clip = null;
            }

            Apply(_background, Mix.BackgroundIn);
            Apply(_backgroundOut, Mix.BackgroundOut);
            Apply(_meditation, Mix.Meditation);
            Apply(_thoughts, Mix.Thoughts);
        }

        /// <summary>
        /// Where the handed-over track is, in samples — so the old level's music goes on from where it
        /// was rather than restarting under the new one. Guarded because a streamed clip that has not
        /// started yet reports a position past its own length.
        /// </summary>
        private static int SafeSamples(AudioSource source)
        {
            if (source == null || source.clip == null) return 0;
            return Mathf.Clamp(source.timeSamples, 0, Mathf.Max(0, source.clip.samples - 1));
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
            Stop(_backgroundOut);
            Stop(_meditation);
            Stop(_thoughts);
            _loadedOutLevel = AudioMix.NoTrack;
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
