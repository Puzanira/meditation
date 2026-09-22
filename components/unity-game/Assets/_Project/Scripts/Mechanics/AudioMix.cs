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
        private float _trackBlend = 1f;
        private int _outgoingLevel = NoTrack;

        /// <summary>«No second track» — the value of <see cref="OutgoingLevelIndex"/> outside a swap.</summary>
        public const int NoTrack = -1;

        /// <summary>
        /// The background BUS, 0..1 — how loud the level's music is right now, whichever track that is.
        /// Already scaled by the tuned ceiling and already ducked (or replaced) by «медитация».
        /// </summary>
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
        public int BackgroundLevelIndex { get; private set; } = NoTrack;

        // ---- смена дорожки: кроссфейд (founder, плейтест 2026-09-22, п.10) ----------------------

        /// <summary>
        /// Where the swap between two levels' tracks stands: 0 = all the previous level's track,
        /// 1 = all of this one's. Sits at 1 whenever no swap is running.
        ///
        /// Until this playtest a level change was <c>Stop()</c> · assign · <c>Play()</c>, i.e. a frame
        /// of silence and then a new track at full volume, five times a run. §8 spends three [tune]
        /// knobs on crossfading the meditation LAYER against the background inside a level and had
        /// nothing at all to say about the one change of music the player actually hears.
        ///
        /// Stands still while the background bus is silent — see <see cref="TickTrackSwap"/>.
        /// </summary>
        public float TrackBlend => _trackBlend;

        /// <summary>The level whose track is still fading out, or <see cref="NoTrack"/>.</summary>
        public int OutgoingLevelIndex => _outgoingLevel;

        /// <summary>True while two background tracks are sounding at once.</summary>
        public bool SwappingTracks => _outgoingLevel != NoTrack;

        /// <summary>Volume of the arriving track — what the bus is, times its share of the swap.</summary>
        public float BackgroundIn => Background * _trackBlend;

        /// <summary>…and of the one leaving. Zero unless <see cref="SwappingTracks"/>.</summary>
        public float BackgroundOut => SwappingTracks ? Background * (1f - _trackBlend) : 0f;

        /// <summary>Everything down and the tail forgotten — «в меню» is instant silence, not a fade.</summary>
        public void SilenceNow()
        {
            Background = 0f;
            Meditation = 0f;
            Thoughts = 0f;
            _meditationTail = 0f;
            _meditationBlend = 0f;
            _trackBlend = 1f;
            _outgoingLevel = NoTrack;
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
            // §8: a level always has its background; off the level the toggle decides, and the title
            // is the exception the spec names by hand («старт — тишина, кроме титула»). Computed
            // first because the swap of tracks needs to know whether anyone can HEAR it.
            bool backgroundWanted = scene.OnLevel || scene.BackgroundAllowed ||
                                    !TuningConfig.AudioSilentOffLevel;

            TickTrackSwap(deltaTime, Mathf.Max(0, scene.LevelIndex), backgroundWanted);

            // ---- слой 2, «медитация» -------------------------------------------------------------
            if (scene.Collecting) _meditationTail = 0f;
            else _meditationTail = Mathf.Max(0f, _meditationTail - deltaTime);

            bool meditating = scene.OnLevel && (scene.Collecting || _meditationTail > 0f);
            float fadeIn = Mathf.Max(0.01f, TuningConfig.AudioMeditationFadeInSeconds);
            float fadeOut = Mathf.Max(0.01f, TuningConfig.AudioMeditationFadeOutSeconds);
            _meditationBlend = Mathf.MoveTowards(_meditationBlend, meditating ? 1f : 0f,
                deltaTime / (meditating ? fadeIn : fadeOut));

            // ---- слой 1, фон уровня --------------------------------------------------------------
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
                ? ThoughtsCurve(pressure) * Mathf.Clamp01(TuningConfig.AudioThoughtsMaxVolume)
                : 0f;

            float smoothing = Mathf.Max(0.01f, TuningConfig.AudioThoughtsSmoothingSeconds);
            Thoughts = Mathf.MoveTowards(Thoughts, thoughtsTarget, deltaTime / smoothing);
        }

        /// <summary>
        /// «Кривая слоя мыслей — менее агрессивная» (founder, 2026-09-22, п.10): the same dial, bent.
        ///
        /// Both ENDS are fixed by construction — silence at nothing on screen, the ceiling at a full
        /// one — so an exponent is the whole of the change and the knob cannot turn the layer off or
        /// make it louder than its ceiling. At the shipped 2.0 the middle of the range is a quarter of
        /// the ceiling instead of a half, which is what the founder heard as «уже орёт, а играть ещё
        /// нормально». At 1.0 it is exactly the straight line it used to be.
        ///
        /// Public because this is the whole claim of that line item, and the EditMode suite checks it
        /// as arithmetic rather than against a speaker batch mode does not have.
        /// </summary>
        public static float ThoughtsCurve(float pressure01)
        {
            float exponent = Mathf.Clamp(TuningConfig.AudioThoughtsCurve, 1f, 8f);
            float p = Mathf.Clamp01(pressure01);
            return Mathf.Approximately(exponent, 1f) ? p : Mathf.Pow(p, exponent);
        }

        /// <summary>
        /// Advance the swap between two levels' background tracks.
        ///
        /// The FIRST track is not a swap: at boot there is nothing to fade out of, and crossfading from
        /// silence would open every run with a level-1 track sliding in under the title.
        ///
        /// …and a swap only moves while the background bus is WANTED on this screen, which is the
        /// whole of «фоновый должен немного затухать, а новый должен нарастать» (founder, 2026-09-22).
        /// The next level's index reaches the mix on its CARD (<c>LevelCardScreen.Audio</c>), and the
        /// card is silence under the shipped «тишина вне уровня» [toggle] — so a blend that ran on the
        /// card ran out in that silence, and the level opened on the new track alone, fading in from
        /// nothing, with the old one already stopped (Codex, 2026-09-22). Frozen, the swap instead
        /// begins on the first frame of the level, where both tracks are audible and one really does
        /// hand over to the other. With the toggle OFF the background carries through the card, the
        /// bus is wanted there, and the crossfade happens on the card — which is also where it is
        /// heard. Either way the rule is the same one: the fade runs where the ear is.
        /// </summary>
        private void TickTrackSwap(float deltaTime, int wantedLevel, bool audible)
        {
            if (BackgroundLevelIndex == NoTrack)
            {
                BackgroundLevelIndex = wantedLevel;
                _trackBlend = 1f;
                _outgoingLevel = NoTrack;
                return;
            }

            if (!audible) return;

            if (wantedLevel != BackgroundLevelIndex)
            {
                // A swap asked for while one is still running: the track that was arriving becomes the
                // one that is leaving, at the volume it had got to. Never a third source.
                _outgoingLevel = BackgroundLevelIndex;
                _trackBlend = 0f;
                BackgroundLevelIndex = wantedLevel;
            }

            if (_outgoingLevel == NoTrack) return;

            float seconds = Mathf.Max(0.01f, TuningConfig.AudioBackgroundCrossfadeSeconds);
            _trackBlend = Mathf.MoveTowards(_trackBlend, 1f, deltaTime / seconds);
            if (_trackBlend >= 1f) _outgoingLevel = NoTrack;
        }
    }
}
