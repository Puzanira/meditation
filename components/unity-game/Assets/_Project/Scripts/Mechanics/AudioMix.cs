using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>What the game sounds like right now, as the screens report it (MECHANICS §8).</summary>
    public readonly struct AudioScene
    {
        /// <summary>Is a level being played? Only a level runs all three layers.</summary>
        public readonly bool OnLevel;

        /// <summary>Which level's background track belongs here, 0-based.</summary>
        public readonly int LevelIndex;

        /// <summary>
        /// May the background play on this screen at all? §8: «на экранах S1/S2/S4/S5/S6 фон уровня не
        /// играет [toggle], старт — тишина, кроме титула» — so the title is the one non-level screen
        /// that says yes.
        /// </summary>
        public readonly bool BackgroundAllowed;

        /// <summary>
        /// Is a collection going WELL right now — noticed, on its thread, handle above the threshold?
        /// That, and only that, is what «медитация» rewards.
        /// </summary>
        public readonly bool Collecting;

        /// <summary>Thoughts on screen — the «мысли» layer's own dial.</summary>
        public readonly int ThoughtCount;

        /// <summary>…and the other dial it can be driven by [toggle], per cent of the frame.</summary>
        public readonly float OverlapPercent;

        public AudioScene(bool onLevel, int levelIndex, bool backgroundAllowed, bool collecting,
            int thoughtCount, float overlapPercent)
        {
            OnLevel = onLevel;
            LevelIndex = levelIndex;
            BackgroundAllowed = backgroundAllowed;
            Collecting = collecting;
            ThoughtCount = thoughtCount;
            OverlapPercent = overlapPercent;
        }

        /// <summary>A screen that is not a level and does not want the background — cards, outcomes.</summary>
        public static AudioScene Quiet(int levelIndex) =>
            new AudioScene(false, levelIndex, false, false, 0, 0f);
    }

    /// <summary>
    /// The three-layer mix of MECHANICS §8, as arithmetic: given what the screen is doing, how loud is
    /// each of the three tracks this frame?
    ///
    /// Kept apart from the <c>AudioSource</c>s on purpose. Every claim §8 makes is about a NUMBER over
    /// TIME — «кроссфейд 0.4 с», «доигрывает ещё 1.5 с», «громкость растёт с числом мыслей», «кнопка
    /// „в меню“ — мгновенная тишина» — and a number over time is checkable in an EditMode test in
    /// microseconds, while the same claim made against a real audio device in batch mode is checkable
    /// only against a device that is not there. <c>GameAudio</c> is then a dozen lines that copy these
    /// three floats onto three sources.
    /// </summary>
    public sealed class AudioMix
    {
        /// <summary>
        /// How far the background is ducked under «медитация» when the toggle says the two play
        /// together rather than one replacing the other. Not a [tune]: §8 offers the choice as a
        /// toggle and gives no second number, and «поверх приглушённого фона» has to be audibly
        /// quieter than the background alone or the toggle does nothing.
        /// </summary>
        public const float DuckedBackground = 0.35f;

        private float _meditationTail;
        private float _meditationBlend;

        /// <summary>Level background, 0..1 — already scaled by the tuned ceiling.</summary>
        public float Background { get; private set; }

        /// <summary>
        /// «Медитация», 0..1, already scaled by the same ceiling the background uses.
        ///
        /// §8 gives this layer a crossfade but no loudness of its own, and it has to be that way: it
        /// REPLACES the background, so if it were louder than the track it replaces, «получается» would
        /// announce itself with a jump in volume rather than with a change of music.
        /// </summary>
        public float Meditation { get; private set; }

        /// <summary>Where the crossfade stands, 0 = background, 1 = meditation — before any ceiling.</summary>
        public float MeditationBlend => _meditationBlend;

        /// <summary>«Мысли», 0..1 — smoothed, already scaled by the tuned ceiling.</summary>
        public float Thoughts { get; private set; }

        /// <summary>Which level's background track the mix wants loaded, 0-based.</summary>
        public int BackgroundLevelIndex { get; private set; }

        /// <summary>Everything down and the tail forgotten — «в меню» is instant silence, not a fade.</summary>
        public void SilenceNow()
        {
            Background = 0f;
            Meditation = 0f;
            Thoughts = 0f;
            _meditationTail = 0f;
            _meditationBlend = 0f;
        }

        /// <summary>
        /// A detail just landed in the vessel. §8: «медитация доигрывает ещё [tune] с и уходит в фон;
        /// если за это время начался новый сбор — не выключается». So this starts a tail rather than
        /// ending the layer, and <see cref="Tick"/> cancels the tail the moment collecting resumes.
        /// </summary>
        public void NoteDetailLanded()
        {
            _meditationTail = Mathf.Max(0f, TuningConfig.AudioMeditationTailSeconds);
        }

        /// <summary>A slip: the layer leaves at once (through its own crossfade), tail and all.</summary>
        public void NoteCollectionBroken()
        {
            _meditationTail = 0f;
        }

        public void Tick(float deltaTime, in AudioScene scene)
        {
            BackgroundLevelIndex = Mathf.Max(0, scene.LevelIndex);

            // ---- слой 2, «медитация» -------------------------------------------------------------
            if (scene.Collecting) _meditationTail = 0f;
            else _meditationTail = Mathf.Max(0f, _meditationTail - deltaTime);

            bool meditating = scene.OnLevel && (scene.Collecting || _meditationTail > 0f);
            float fadeIn = Mathf.Max(0.01f, TuningConfig.AudioMeditationFadeInSeconds);
            float fadeOut = Mathf.Max(0.01f, TuningConfig.AudioMeditationFadeOutSeconds);
            _meditationBlend = Mathf.MoveTowards(_meditationBlend, meditating ? 1f : 0f,
                deltaTime / (meditating ? fadeIn : fadeOut));

            // ---- слой 1, фон уровня --------------------------------------------------------------
            // §8: a level always has its background; off the level the toggle decides, and the title
            // is the exception the spec names by hand («старт — тишина, кроме титула»).
            bool backgroundWanted = scene.OnLevel || scene.BackgroundAllowed ||
                                    !TuningConfig.AudioSilentOffLevel;
            float ceiling = Mathf.Clamp01(TuningConfig.AudioBackgroundVolume);
            Meditation = _meditationBlend * ceiling;

            // «Медитация» either replaces the background or lies over a ducked one — the same
            // crossfade either way, only the floor it fades down to differs.
            float floor = TuningConfig.AudioMeditationReplacesBackground ? 0f : DuckedBackground;
            float backgroundTarget = backgroundWanted ? ceiling * Mathf.Lerp(1f, floor, _meditationBlend) : 0f;

            // The background moves on the same crossfade as the meditation layer, so the two really
            // exchange places instead of both dipping in the middle.
            Background = Mathf.MoveTowards(Background, backgroundTarget,
                deltaTime / (backgroundTarget > Background ? fadeIn : fadeOut));

            // ---- слой 3, «мысли» -----------------------------------------------------------------
            float pressure = TuningConfig.AudioThoughtsByOverlap
                ? Mathf.Clamp01(scene.OverlapPercent / Mathf.Max(1f, TuningConfig.LossOverlapPercent))
                : Mathf.Clamp01(scene.ThoughtCount / Mathf.Max(1f, TuningConfig.AudioThoughtsAtCount));

            float thoughtsTarget = scene.OnLevel
                ? pressure * Mathf.Clamp01(TuningConfig.AudioThoughtsMaxVolume)
                : 0f;

            float smoothing = Mathf.Max(0.01f, TuningConfig.AudioThoughtsSmoothingSeconds);
            Thoughts = Mathf.MoveTowards(Thoughts, thoughtsTarget, deltaTime / smoothing);
        }
    }
}
