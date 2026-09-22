using System;
using UnityEngine;

namespace Meditation.Tuning
{
    /// <summary>What happens to collection progress when the crank stops (MECHANICS §1).</summary>
    public enum CrankStopMode
    {
        /// <summary>A. progress drops to zero, the detail flies home.</summary>
        ResetToZero = 0,
        /// <summary>B. progress melts away — come back to cranking fast enough and part of it is saved.</summary>
        Decay = 1
    }

    /// <summary>Does crank SPEED matter, or only continuity (MECHANICS §1)?</summary>
    public enum CrankSpeedMode
    {
        /// <summary>A. no — only continuity matters (more meditative).</summary>
        ContinuityOnly = 0,
        /// <summary>B. yes, up to a ceiling (more arcade-y).</summary>
        SpeedScaled = 1
    }

    /// <summary>Which thought(s) a hit of the отгон lands on (MECHANICS §3).</summary>
    public enum HitTargeting
    {
        /// <summary>A. every thought on screen takes the hit.</summary>
        AllOnScreen = 0,
        /// <summary>B. one — the one nearest to the screen centre.</summary>
        NearestToCenter = 1,
        /// <summary>
        /// C. the one the joystick is pointing at. Since the отгон moved to the height sensors
        /// (2026-08-07) the stick is nothing but the AIM, so this variant reads the aim — which is the
        /// same number it always read, and now without the gesture conflict MECHANICS §3 warned about.
        /// </summary>
        StickDirection = 2
    }

    /// <summary>How the player picks the detail to collect (MECHANICS §2 / SCREENS "Выбор детали").</summary>
    public enum NoticeMode
    {
        /// <summary>A. fixed authored order, current target highlighted.</summary>
        FixedOrder = 0,
        /// <summary>B. gaze circle driven by a smooth joystick tilt (base variant).</summary>
        GazeJoystick = 1,
        /// <summary>C. auto-pick the uncovered detail nearest to the vessel.</summary>
        AutoNearest = 2
    }

    /// <summary>Thought toughness class (MECHANICS §3): weak / medium / strong.</summary>
    public enum ThoughtStrength
    {
        Weak = 0,
        Medium = 1,
        Strong = 2
    }

    /// <summary>
    /// Every [tune]/[toggle] parameter from MECHANICS.md, in one place, as mutable static state.
    ///
    /// Static on purpose: the preview stand must keep the founder's values when she jumps between
    /// scenettes (done contract §7). This lives only in the tuning stand — shipping game code must
    /// not depend on it, and the final numbers get written back into MECHANICS.md after the playtest.
    ///
    /// Shipped starting values live in <see cref="Defaults"/>. Tests compare against those constants,
    /// never against a literal, so re-tuning a number after the playtest cannot turn a balance
    /// decision into a red test.
    ///
    /// Static state alone survives a scene change but not a closed editor, so <see cref="TuningStore"/>
    /// mirrors every field of this class into <c>UserSettings/tuning.json</c> on each panel interaction
    /// and reads it back before the first scenette. Adding a field here is enough to persist it — the
    /// store finds fields by reflection.
    /// </summary>
    public static class TuningConfig
    {
        /// <summary>
        /// Shipped starting values. After a playtest they change here — and only here.
        ///
        /// FINAL, 2026-08-01: every number below is the founder's own, read back out of
        /// <c>UserSettings/tuning.json</c> at the end of her playtest session and baked in. That baking
        /// is not cosmetic — the game ships to the cabinet as a Unity PACKAGE consumed by the arcade
        /// hub, and <c>UserSettings/</c> belongs to the meditation project, not to the hub: on the
        /// cabinet the file simply is not there, so anything left un-baked would silently play at the
        /// pre-playtest numbers.
        ///
        /// The GLOBAL §3/§4/§5 values are the level-1 band, and that is not a coincidence:
        /// <see cref="ApplyLevel"/> copies the active band over them at every level start, so what the
        /// file recorded is the last band the founder was in. They are kept in step with L1 on purpose —
        /// the globals are what the preview stand's scenettes run on, and the stand should teach the
        /// same feel the first level does.
        /// </summary>
        public static class Defaults
        {
            public const float CrankThresholdDegPerSec = 120f;
            public const float GraceMs = 300f;
            public const float CollectSeconds = 6f;
            public const CrankStopMode StopMode = CrankStopMode.ResetToZero;
            public const float DecayPerSecond = 0.4f;
            public const CrankSpeedMode SpeedMode = CrankSpeedMode.ContinuityOnly;
            public const float CrankReferenceDegPerSec = 360f;

            public const NoticeMode Notice = NoticeMode.GazeJoystick;
            public const float GazeSpeedPxPerSec = 900f;
            public const float GazeDwellSeconds = 0.4f;

            /// <summary>
            /// «Круг взгляда крупнее и заметнее» (founder, 2026-08-07). 90 px was the number SCREENS
            /// picked for a greybox frame of 50 px rectangles; on the art plates it is a faint disc
            /// about the size of the office paperclip, and the founder loses it in the picture. 130
            /// is a third bigger and still comfortably smaller than the gap between two details on
            /// the tightest level (the library's lamp and glasses are 287 px apart), so widening the
            /// circle does not turn «навёл» into «где-то там».
            /// </summary>
            public const float GazeRadiusPx = 130f;
            public const float GazeNeonGlow = 1.2f;
            public const float GazeNeonRingPx = 12f;

            /// <summary>
            /// Отгон на датчиках высоты (founder, 2026-08-07), retuned for «хаотичные махания» on
            /// 2026-08-08. All three numbers are in the sensor's own units — 0..1 of its travel and
            /// seconds — so none of them is the joystick pair renamed: 0.5 of a stick deflection and
            /// 0.5 of a sensor's range are not the same gesture.
            ///
            /// **Амплитуда 0.08** — was 0.2, a fifth of the range, and that fifth is what the founder
            /// was fighting: on the keyboard emulation (1.25 ед/с up, 2.0 ед/с of spring-back) it took
            /// 160 ms of holding Q one way and 100 ms the other, so anything faster than a metronome
            /// landed no hits at all. A twelfth of the range is 64 ms up and 40 ms down — the timing of
            /// a hand being jerked about rather than swept — and it is still comfortably above the
            /// ±0.05 ripple an analog line makes standing still.
            ///
            /// **Резкость 0.6 ед/с** — unchanged. Half of the emulated rise rate and under a third of
            /// the spring-back, so BOTH halves of a keyboard stroke clear it comfortably, while a hand
            /// slowly lowering onto the panel — which is a rest, not a swipe — never does.
            ///
            /// **Кулдаун 45 мс** — the floor between two hits of ONE sensor, i.e. a ceiling of 22
            /// hits/s. It is not what keeps a held key quiet (one movement lands one hit, full stop —
            /// see <see cref="Mechanics.SwipeDetector"/>); it is what keeps a jittering analog line,
            /// where every frame can be its own turn-around, from reading as a drum roll. Chosen under
            /// the fastest hand we could measure (~16 hits/s on mashed Q) so it never touches a player.
            /// </summary>
            public const float SwipeAmplitude = 0.08f;
            public const float SwipeSharpness = 0.6f;
            public const float SwipeCooldownMs = 45f;

            /// <summary>
            /// Белая подложка под штрихами мысли — OFF since 2026-08-08 (founder, live session:
            /// «мысли должны быть чисто чёрными»).
            ///
            /// It shipped ON because black hatching measures 1.6–3.5:1 against the five plates and the
            /// designer put her own sample strips on paper. The founder has now looked at the game with
            /// the halo in it and decided the halo costs more than it buys — so it is a [toggle] and the
            /// default is her answer, not ours. The risk is hers and she named it: on the dark zones of
            /// a plate the scribbles are harder to see, and this switch is how she compares.
            /// </summary>
            public const bool ThoughtBacking = false;

            // Pip counts of the walkthrough's own frames: гора посуды 4, клубок ? 3. Strong came down
            // from 7 to the level-1 band's 6 in the playtest — see the note on the globals above.
            public const int DurabilityWeak = 3;
            public const int DurabilityMedium = 4;
            public const int DurabilityStrong = 6;

            public const bool HitDecayEnabled = true;
            public const float HitDecayMs = 800f;
            public const HitTargeting Targeting = HitTargeting.NearestToCenter;

            public const float WaveIntervalSeconds = 10f;
            public const int WaveWeak = 1;
            public const int WaveMedium = 0;
            public const int WaveStrong = 0;
            public const bool PressureRamp = false;
            public const float PressureRampPercent = 0f;
            public const bool ThoughtDrift = true;
            public const float DriftPxPerSec = 25f;

            /// <summary>
            /// «Ведёрко ПОД мыслями» (founder, 2026-09-22): a thought flying over the vessel covers it,
            /// the way it covers everything else on the plate.
            ///
            /// ON since that playtest. It shipped OFF because the vessel is what the player aims the
            /// haul at, and a bucket you cannot see is a target you cannot hit — but the founder played
            /// the build and the opposite reading won: a thought that stops at the bucket's edge reads
            /// as a sprite with a hole in it, and «сколько собрано» is answered by the fill bar, which
            /// lives on the HUD and stays above the thoughts whatever this toggle says (blocker Б2).
            /// </summary>
            public const bool ThoughtsCoverVessel = true;

            /// <summary>
            /// «Порог проигрыша» — how much of the frame the thoughts have to hide, in per cent, before
            /// the level is lost. MECHANICS §5, and the one failure condition the game has.
            ///
            /// 92 % until 2026-09-22. It came down with the wave BUDGET (founder's «волны»): a level
            /// used to send thoughts forever, so any threshold under 100 was reached eventually and the
            /// number only decided WHEN. With a budget — five thoughts on level 1 and nothing after
            /// them — the threshold decides WHETHER, and 92 % of the frame is more than five weak blobs
            /// can hide even grown to their ceiling. 85 % is what the budgets of all five levels can
            /// actually reach with nobody beating a thought off (EveryLevel_FillsUpAndIsLost…), and it
            /// is still a screen the player cannot see past.
            /// </summary>
            public const float LossOverlapPercent = 85f;
            public const float PeakOverlapPercent = 65f;

            /// <summary>
            /// «Общий запас мыслей на уровень» (founder, 2026-09-22, «волны»): how many thoughts a level
            /// sends in total before it has nothing left to send. 0 = no budget, which is what the
            /// greybox stand runs on — a scenette is a rig for one rule and has to be able to go on
            /// sending blobs for as long as the founder watches it.
            ///
            /// The GAME's own numbers live in the per-level bands below; this global is only the value
            /// <see cref="ApplyLevel"/> has not overwritten yet.
            /// </summary>
            public const int ThoughtBudget = 0;

            /// <summary>
            /// «Мысли разрастаются со временем» (founder, 2026-08-07): how fast a thought grows past
            /// its class, in per cent of its spawn size per second, and how far it may go.
            ///
            /// **Both numbers went up by an order of magnitude on 2026-09-22** — «рост увеличить,
            /// разрастаются на весь экран» — and the two orders of the same playtest are one decision:
            /// the wave BUDGET took away the level's ability to bury a player under sheer count, so the
            /// screen now fills because each thought grows to the size of it. At 8 %/с a blob reaches
            /// its ×6 ceiling in a little over a minute, which is the same shape the knob always had —
            /// a duration in disguise — just a much longer and much larger one.
            /// </summary>
            public const float ThoughtGrowthPercentPerSec = 10f;
            public const float ThoughtGrowthCap = 12f;

            public const bool BreatherEnabled = true;
            public const float BreatherSeconds = 2f;
            public const bool AutoRetry = true;

            // ---- §8 Звук (заказ founder 2026-08-07) -------------------------------------------
            // Three layers, three groups of knobs. Every number below is the spec's own starting
            // value, not a guess — MECHANICS §8 lists each one with its slider range.
            public const float AudioBackgroundVolume = 0.6f;
            public const float AudioMeditationFadeInSeconds = 0.4f;
            public const float AudioMeditationFadeOutSeconds = 0.6f;
            public const float AudioMeditationTailSeconds = 1.5f;
            public const bool AudioMeditationReplacesBackground = true;
            public const float AudioThoughtsMaxVolume = 0.8f;
            public const float AudioThoughtsAtCount = 8f;
            public const float AudioThoughtsSmoothingSeconds = 0.5f;
            public const bool AudioThoughtsByOverlap = false;
            public const bool AudioSilentOffLevel = true;

            /// <summary>
            /// Crossfade between two LEVELS' background tracks, s (founder 2026-09-22, п.10).
            ///
            /// A different thing from the two «медитация» crossfades above, which move one layer against
            /// another inside a level. This one is the level change itself: until now the background
            /// source was stopped, given the next level's clip and started again — one frame of silence
            /// and a new track at full volume, five times a run. [tune]
            /// </summary>
            public const float AudioBackgroundCrossfadeSeconds = 1.2f;

            /// <summary>
            /// Shape of the «мысли» layer against screen coverage (founder 2026-09-22: «кривая мягче»).
            ///
            /// The layer used to be LINEAR in its dial, so half a screen of thoughts was half the
            /// ceiling and the noise was already loud while the level was still comfortable. An exponent
            /// above 1 bends the curve down in the middle and keeps the top: at 2.0, half a screen is a
            /// quarter of the ceiling and a full one is still the full ceiling. [tune 1–4]
            /// </summary>
            public const float AudioThoughtsCurve = 2f;

            // ---- Луч-подсветка деталей (SCREENS «Детали в сцене», заказ founder 2026-08-07) ----
            //
            // «Блики-пробеги по ВСЕМ объектам на ВСЕХ уровнях и с большей частотой» (founder
            // 2026-09-22, п.7). Three of these six changed with that one sentence: the period halved,
            // the band lights the detail the player is already holding too, and it stops thinning out
            // on the late levels — «реже на поздних» was a difficulty idea, and the founder's complaint
            // is that she cannot FIND the objects, which is not a difficulty she asked for.
            public const float SweepPeriodSeconds = 4.5f;
            public const float SweepDurationSeconds = 1.2f;
            public const float SweepWidthPx = 320f;
            public const float SweepStrength = 0.7f;
            public const bool SweepOnlyUnnoticed = false;
            public const bool SweepRarerOnLateLevels = false;

            // ---- Неон-обводка деталей (заказ founder 2026-08-07) --------------------------------
            /// <summary>
            /// OFF on purpose. This is the third thing on the screen saying «вот деталь» — after the
            /// pulse and after the light sweep — and the founder asked for it precisely so she can
            /// compare all three and switch two of them off. A toggle that ships ON would answer that
            /// question for her.
            /// </summary>
            public const bool DetailNeonOutline = false;
            public const float DetailOutlinePx = 6f;
            public const float DetailOutlineStrength = 0.8f;

            /// <summary>
            /// Is the tuning panel expanded on start? FALSE since the playtest: the panel is the
            /// founder's instrument, and on the cabinet the game opens as a game. She turns it back on
            /// with the panel's own toggle, and <see cref="TuningStore"/> remembers the choice.
            /// </summary>
            public const bool PanelVisible = false;

            // ---- §6 Прогрессия сложности: [tune per level] ------------------------------------
            // «с каждым уровнем больше мыслей, выше прочность, быстрее наплыв». The shipped ladder
            // is monotonic in PRESSURE — that monotonicity is what LevelProgressionTests pins, so
            // re-tuning a number after the playtest cannot quietly flatten the curve.
            //
            // Everything here is keyed to the level NUMBER rather than to the setting, so the
            // renumbering of 2026-08-07 left these columns exactly where the playtest of 2026-08-01
            // put them.
            //
            // **The «длительность уровня» column is gone** (founder, 2026-08-07: the timer is out).
            // The ladder used to be readable as two things at once — pressure going up and the clock
            // going down — and with the clock gone the ladder is one thing: how much screen the waves
            // buy per second. The test measures exactly that — the area of the thoughts this level
            // ACTUALLY sends (its own five silhouettes, cover-fitted to the wave's classes) per second,
            // not the area of the class boxes — because «больше мыслей» and «мысли крупнее» are two
            // ways of spending the same budget and the founder's own order for this round spends it
            // BOTH ways in the first two levels.
            //
            // Levels 1 and 2 are that order, and they are the only two numbers this round changed:
            //
            //   L1 «мыслей больше»       — 3 weak every 5.0 s. The old first level sent eight small
            //       blobs in a whole minute (~10 % of the frame if none were ever beaten off), which
            //       is not a level about thoughts filling the screen.
            //   L2 «меньше, но крупнее»  — 2 MEDIUM every 4.5 s: fewer blobs than level 1 (2 < 3), a
            //       class up (M instead of S), and still more screen per second, so the curve keeps
            //       climbing while the FEEL changes from a swarm to a few big ones — which is what
            //       «чуть больше первого» asks for.
            //
            // Both compositions were raised once more on 2026-08-08, together with a 4 % ramp on level
            // 1, because the round's own claim turned out to be untestable arithmetic: pressure was
            // being measured in CLASS area while the thoughts are drawn at their silhouettes' cover-fit
            // size, and against the real sizes level 2 was the LIGHTER level (17 318 px²/с against
            // 23 695). Worse, a player who did nothing at all could not lose levels 1 and 2 — the
            // coverage plateaued around 49 % on level 1, and «поражение = мысли заполнили экран» was a
            // rule the first two levels did not actually have. The numbers are now the ones that hold
            // both statements at once (LevelCatalogTests: the ladder on real sizes, and defeat reached
            // from a standing start inside three minutes on every level — ~124 s here, ~89 s on L2).
            //
            // ---- ВОЛНЫ ПО ЗАПАСУ (founder, плейтест 2026-09-22) -------------------------------
            //
            // Everything above this line about wave compositions is superseded, and by the same person
            // who set it. Her word was «волны», and what she dictated under it is a different shape of
            // level: «у уровня есть общий запас мыслей — на первом 5, на втором 10, на третьем 15 — и
            // приходят они раз в 10 секунд». A level is no longer a tap that runs until somebody turns
            // it off; it is a finite amount of interference with a beginning and an end, and «мысли
            // кончились» is a state the player can reach by playing well.
            //
            // The ladder that produced it is one line: the budget is FIVE waves on every level, and the
            // wave is what climbs.
            //
            //   У1  5 = 5×1  одна слабая                      — «все слабые», её слова
            //   У2 10 = 5×2  слабая + средняя                 — «по две за волну», её слова
            //   У3 15 = 5×3  слабая + средняя + крепкая       — третья ступень лестницы
            //   У4 20 = 5×4  слабая + 2 средние + крепкая     — ПРЕДЛОЖЕНО, на гейт founder
            //   У5 25 = 5×5  2 слабые + 2 средние + крепкая   — ПРЕДЛОЖЕНО, на гейт founder
            //
            // L4 and L5 are the owner's proposal (the founder's list stops at 15 and says «продолжить
            // лестницу в том же духе»): they keep the wave compositions levels 4 and 5 already had from
            // the 2026-08-01 playtest, so the only thing that changed there is the clock and the fact
            // that the tap now stops — and the budgets 20 and 25 fall out of the same 5-wave rule.
            //
            // The pressure ramp is OFF on every level, and that is not an omission: «раз в 10 секунд»
            // is a statement about the interval, and a ramp is a machine for making that sentence false
            // by the third wave. The knob stays on the panel at 0 so the founder can put it back.
            //
            // Durability, drift and the growth ceiling keep climbing level by level exactly as before —
            // that is the part of §6 this round did not touch.
            public const int L1ThoughtBudget = 5;
            public const float L1WaveIntervalSeconds = 10f;
            public const int L1WaveWeak = 1;
            public const int L1WaveMedium = 0;
            public const int L1WaveStrong = 0;
            public const int L1DurabilityWeak = 3;
            public const int L1DurabilityMedium = 4;
            public const int L1DurabilityStrong = 6;
            public const float L1DriftPxPerSec = 25f;
            public const float L1PressureRampPercent = 0f;
            public const float L1ThoughtGrowthPercentPerSec = 10f;
            public const float L1ThoughtGrowthCap = 12f;

            public const int L2ThoughtBudget = 10;
            public const float L2WaveIntervalSeconds = 10f;
            public const int L2WaveWeak = 1;
            public const int L2WaveMedium = 1;
            public const int L2WaveStrong = 0;
            public const int L2DurabilityWeak = 3;
            public const int L2DurabilityMedium = 5;
            public const int L2DurabilityStrong = 8;
            public const float L2DriftPxPerSec = 35f;
            public const float L2PressureRampPercent = 0f;
            public const float L2ThoughtGrowthPercentPerSec = 10f;
            public const float L2ThoughtGrowthCap = 12f;

            public const int L3ThoughtBudget = 15;
            public const float L3WaveIntervalSeconds = 10f;
            public const int L3WaveWeak = 1;
            public const int L3WaveMedium = 1;
            public const int L3WaveStrong = 1;
            public const int L3DurabilityWeak = 4;
            public const int L3DurabilityMedium = 6;
            public const int L3DurabilityStrong = 9;
            public const float L3DriftPxPerSec = 45f;
            public const float L3PressureRampPercent = 0f;
            public const float L3ThoughtGrowthPercentPerSec = 10f;
            public const float L3ThoughtGrowthCap = 12f;

            /// <summary>Уровень 4 — предложение владельца инкремента, ждёт «ок» основательницы.</summary>
            public const int L4ThoughtBudget = 20;
            public const float L4WaveIntervalSeconds = 10f;
            public const int L4WaveWeak = 1;
            public const int L4WaveMedium = 2;
            public const int L4WaveStrong = 1;
            public const int L4DurabilityWeak = 4;
            public const int L4DurabilityMedium = 7;
            public const int L4DurabilityStrong = 10;
            public const float L4DriftPxPerSec = 52f;
            public const float L4PressureRampPercent = 0f;
            public const float L4ThoughtGrowthPercentPerSec = 10f;
            public const float L4ThoughtGrowthCap = 12f;

            /// <summary>Уровень 5 — предложение владельца инкремента, ждёт «ок» основательницы.</summary>
            public const int L5ThoughtBudget = 25;
            public const float L5WaveIntervalSeconds = 10f;
            public const int L5WaveWeak = 2;
            public const int L5WaveMedium = 2;
            public const int L5WaveStrong = 1;
            public const int L5DurabilityWeak = 5;
            public const int L5DurabilityMedium = 8;
            public const int L5DurabilityStrong = 11;
            public const float L5DriftPxPerSec = 58f;
            public const float L5PressureRampPercent = 0f;
            public const float L5ThoughtGrowthPercentPerSec = 10f;
            public const float L5ThoughtGrowthCap = 12f;

            // ---- Тайминги заставок (founder 2026-09-22: «держать дольше») --------------------
            /// <summary>
            /// S2, карточка уровня — «экран с рыбкой» (уровень 1). Было 2.5 с: founder не успевала
            /// прочитать, что на ней написано. [tune]
            /// </summary>
            public const float LevelCardSeconds = 4f;

            /// <summary>S4, перебивка «Отлично!» в конце уровня. Было 2 с. [tune]</summary>
            public const float LevelCompleteSeconds = 3.5f;

            // ---- Переход «круглая рябь» (founder 2026-09-22, п.8) -----------------------------
            /// <summary>How long the ripple takes to close over the frame (and to open again), s. [tune]</summary>
            public const float RippleSeconds = 0.9f;

            /// <summary>Width of the wet ring at the ripple's edge, in fractions of the frame. [tune]</summary>
            public const float RippleRingWidth = 0.14f;

            /// <summary>How far the ripple pushes the picture sideways at its crest, UV. [tune]</summary>
            public const float RippleAmplitude = 0.035f;

            /// <summary>How many crests the ring carries — one is a wipe, four are water. [tune]</summary>
            public const float RippleWaves = 3f;

            // ---- Мерцание деталей (founder 2026-09-22, п.7: «активнее») -----------------------
            /// <summary>
            /// Amplitude of the details' breathing, as a share of their size. SCREENS says «1.0 → 1.12»
            /// and 0.12 is what that was; the founder could not see it on a photographic plate, so it is
            /// a [tune] now and it starts at twice the spec's number.
            /// </summary>
            public const float DetailPulseAmplitude = 0.24f;

            /// <summary>Seconds of one breath, in and out. SCREENS's own 1.6 s, faster since the same list.</summary>
            public const float DetailPulseSeconds = 1.2f;
        }

        /// <summary>How many per-level bands exist — one per level of <c>LevelCatalog</c>.</summary>
        public const int LevelBands = 5;

        // ---- §1 Сбор (динамо) ----------------------------------------------------------------
        /// <summary>Minimum crank speed that still counts as "cranking", deg/s. [tune]</summary>
        public static float CrankThresholdDegPerSec = Defaults.CrankThresholdDegPerSec;
        /// <summary>How long a stall is forgiven before progress is lost, ms. [tune 0–800]</summary>
        public static float GraceMs = Defaults.GraceMs;
        /// <summary>Time to collect one detail at normal cranking, s. [tune 3–15]</summary>
        public static float CollectSeconds = Defaults.CollectSeconds;
        /// <summary>A: reset to zero / B: melt. [toggle]</summary>
        public static CrankStopMode StopMode = Defaults.StopMode;
        /// <summary>Melting rate in mode B, progress fraction per second.</summary>
        public static float DecayPerSecond = Defaults.DecayPerSecond;
        /// <summary>A: continuity only / B: speed matters up to a ceiling. [toggle]</summary>
        public static CrankSpeedMode SpeedMode = Defaults.SpeedMode;
        /// <summary>Crank speed that equals "normal" collection speed in mode B, deg/s.</summary>
        public static float CrankReferenceDegPerSec = Defaults.CrankReferenceDegPerSec;

        // ---- §2 Выбор детали -----------------------------------------------------------------
        /// <summary>A/B/C detail picking. [toggle]</summary>
        public static NoticeMode Notice = Defaults.Notice;
        /// <summary>Gaze circle speed, px/s. [tune]</summary>
        public static float GazeSpeedPxPerSec = Defaults.GazeSpeedPxPerSec;
        /// <summary>How long the gaze must rest on a detail to notice it, s. [tune]</summary>
        public static float GazeDwellSeconds = Defaults.GazeDwellSeconds;
        /// <summary>Radius of the gaze circle, design px. [tune 60–220]</summary>
        public static float GazeRadiusPx = Defaults.GazeRadiusPx;
        /// <summary>How hard the neon ring of the gaze burns, 0..3. [tune]</summary>
        public static float GazeNeonGlow = Defaults.GazeNeonGlow;
        /// <summary>Thickness of that ring, design px. [tune 4–40]</summary>
        public static float GazeNeonRingPx = Defaults.GazeNeonRingPx;

        // ---- §3 Отгон мыслей (датчики высоты) ------------------------------------------------
        /// <summary>Sensor travel inside one movement that counts as a hit, 0..1. [tune]</summary>
        public static float SwipeAmplitude = Defaults.SwipeAmplitude;
        /// <summary>Sensor speed below which a movement is not a pass at all, units/s. [tune]</summary>
        public static float SwipeSharpness = Defaults.SwipeSharpness;
        /// <summary>Shortest gap between two hits of ONE sensor, ms — the rate ceiling. [tune 0–300]</summary>
        public static float SwipeCooldownMs = Defaults.SwipeCooldownMs;
        /// <summary>Hits needed to pop a weak thought. [tune 2–12]</summary>
        public static int DurabilityWeak = Defaults.DurabilityWeak;
        /// <summary>Hits needed to pop a medium thought. [tune 2–12]</summary>
        public static int DurabilityMedium = Defaults.DurabilityMedium;
        /// <summary>Hits needed to pop a strong thought. [tune 2–12]</summary>
        public static int DurabilityStrong = Defaults.DurabilityStrong;
        /// <summary>Does a thought's hit counter reset after a pause? [toggle]</summary>
        public static bool HitDecayEnabled = Defaults.HitDecayEnabled;
        /// <summary>Pause between hits that resets the counter, ms. [tune 400–1500]</summary>
        public static float HitDecayMs = Defaults.HitDecayMs;
        /// <summary>A/B/C hit targeting. [toggle]</summary>
        public static HitTargeting Targeting = Defaults.Targeting;

        // ---- §4 Мысли: волны и перекрытие ----------------------------------------------------
        /// <summary>Seconds between waves. [tune per level]</summary>
        public static float WaveIntervalSeconds = Defaults.WaveIntervalSeconds;
        /// <summary>Wave composition: weak thoughts per wave. [tune per level]</summary>
        public static int WaveWeak = Defaults.WaveWeak;
        /// <summary>Wave composition: medium thoughts per wave. [tune per level]</summary>
        public static int WaveMedium = Defaults.WaveMedium;
        /// <summary>Wave composition: strong thoughts per wave. [tune per level]</summary>
        public static int WaveStrong = Defaults.WaveStrong;
        /// <summary>Does the pressure grow inside a level — do waves come closer together? [toggle]</summary>
        public static bool PressureRamp = Defaults.PressureRamp;
        /// <summary>How much each wave shortens the next interval, %. [tune]</summary>
        public static float PressureRampPercent = Defaults.PressureRampPercent;
        /// <summary>Do thoughts drift towards the centre? [toggle]</summary>
        public static bool ThoughtDrift = Defaults.ThoughtDrift;
        /// <summary>Drift speed, px/s. [tune 20–60]</summary>
        public static float DriftPxPerSec = Defaults.DriftPxPerSec;
        /// <summary>May thoughts cover the vessel and the detail being dragged? [toggle] — ON since 2026-09-22.</summary>
        public static bool ThoughtsCoverVessel = Defaults.ThoughtsCoverVessel;
        /// <summary>Screen coverage that means defeat, %. [tune 50–100]</summary>
        public static float LossOverlapPercent = Defaults.LossOverlapPercent;

        /// <summary>
        /// «Общий запас мыслей на уровень» — how many the level sends in total, 0 = unlimited.
        /// [tune per level]
        /// </summary>
        public static int ThoughtBudget = Defaults.ThoughtBudget;

        /// <summary>
        /// Screen coverage at which the picture goes into «пик хаоса», %. [tune]
        ///
        /// SCREENS lists this as its own [tune] («Пик хаоса: при перекрытии ≥ порога»), separate from
        /// the defeat threshold of §4 — and it has to be, because a peak that starts where the level
        /// ends is a peak nobody ever sees. It is the warning: the edges darken while there is still
        /// time to dig out.
        /// </summary>
        public static float PeakOverlapPercent = Defaults.PeakOverlapPercent;

        /// <summary>
        /// Draw the light halo under a thought's hatching? [toggle] — OFF by default (founder,
        /// 2026-08-08: «мысли чисто чёрные»). See <see cref="Defaults.ThoughtBacking"/>.
        /// </summary>
        public static bool ThoughtBacking = Defaults.ThoughtBacking;

        /// <summary>How fast a thought grows past its class, % of its spawn size per second. [tune per level]</summary>
        public static float ThoughtGrowthPercentPerSec = Defaults.ThoughtGrowthPercentPerSec;
        /// <summary>Ceiling of that growth, × the spawn size (never below 1). [tune per level]</summary>
        public static float ThoughtGrowthCap = Defaults.ThoughtGrowthCap;

        // ---- §5 Победа, поражение --------------------------------------------------------------
        // «Длительность уровня» lived here until 2026-08-07. The timer is out (see LevelRules).
        /// <summary>Breather (no spawns) after each collected detail? [toggle]</summary>
        public static bool BreatherEnabled = Defaults.BreatherEnabled;
        /// <summary>Breather length, s. [tune 0–5]</summary>
        public static float BreatherSeconds = Defaults.BreatherSeconds;
        /// <summary>Auto-restart the scenette 4 s after a defeat instead of waiting for the crank. [toggle]</summary>
        public static bool AutoRetry = Defaults.AutoRetry;

        // ---- §8 Звук --------------------------------------------------------------------------
        /// <summary>Ceiling of the level's own background track, 0..1. [tune]</summary>
        public static float AudioBackgroundVolume = Defaults.AudioBackgroundVolume;
        /// <summary>Crossfade background → «медитация» when a collection is going well, s. [tune 0.1–1.5]</summary>
        public static float AudioMeditationFadeInSeconds = Defaults.AudioMeditationFadeInSeconds;
        /// <summary>Crossfade «медитация» → background when the detail slips, s. [tune 0.1–1.5]</summary>
        public static float AudioMeditationFadeOutSeconds = Defaults.AudioMeditationFadeOutSeconds;
        /// <summary>How long «медитация» plays on after the detail lands, s. [tune 0–4]</summary>
        public static float AudioMeditationTailSeconds = Defaults.AudioMeditationTailSeconds;
        /// <summary>Does «медитация» replace the background, or lie over a ducked one? [toggle]</summary>
        public static bool AudioMeditationReplacesBackground = Defaults.AudioMeditationReplacesBackground;
        /// <summary>Ceiling of the «мысли» layer, 0..1. [tune]</summary>
        public static float AudioThoughtsMaxVolume = Defaults.AudioThoughtsMaxVolume;
        /// <summary>How many thoughts on screen mean the ceiling. [tune 4–15]</summary>
        public static float AudioThoughtsAtCount = Defaults.AudioThoughtsAtCount;
        /// <summary>Smoothing of the «мысли» volume so it does not jump per popped blob, s. [tune 0.1–2]</summary>
        public static float AudioThoughtsSmoothingSeconds = Defaults.AudioThoughtsSmoothingSeconds;
        /// <summary>Drive the «мысли» layer by screen coverage instead of by count? [toggle]</summary>
        public static bool AudioThoughtsByOverlap = Defaults.AudioThoughtsByOverlap;
        /// <summary>Silence on the screens that are not a level (S2/S4/S5/S6). [toggle]</summary>
        public static bool AudioSilentOffLevel = Defaults.AudioSilentOffLevel;
        /// <summary>Crossfade from one level's background track to the next one's, s. [tune 0–4]</summary>
        public static float AudioBackgroundCrossfadeSeconds = Defaults.AudioBackgroundCrossfadeSeconds;
        /// <summary>Exponent bending the «мысли» layer's rise; 1 = the old straight line. [tune 1–4]</summary>
        public static float AudioThoughtsCurve = Defaults.AudioThoughtsCurve;

        // ---- Тайминги заставок, переход «рябь», мерцание деталей (founder 2026-09-22) ------------
        /// <summary>How long the level card is held, s. [tune 1.5–8]</summary>
        public static float LevelCardSeconds = Defaults.LevelCardSeconds;
        /// <summary>How long the «Отлично!» interstitial is held, s. [tune 1–8]</summary>
        public static float LevelCompleteSeconds = Defaults.LevelCompleteSeconds;
        /// <summary>Length of one half of the ripple transition, s. [tune 0.3–2.5]</summary>
        public static float RippleSeconds = Defaults.RippleSeconds;
        /// <summary>Width of the ripple's wet ring, fractions of the frame. [tune 0.04–0.4]</summary>
        public static float RippleRingWidth = Defaults.RippleRingWidth;
        /// <summary>How far the ripple displaces the picture at its crest, UV. [tune 0–0.12]</summary>
        public static float RippleAmplitude = Defaults.RippleAmplitude;
        /// <summary>Crests inside the ring. [tune 1–6]</summary>
        public static float RippleWaves = Defaults.RippleWaves;
        /// <summary>Amplitude of the details' pulse, share of their size. [tune 0–0.5]</summary>
        public static float DetailPulseAmplitude = Defaults.DetailPulseAmplitude;
        /// <summary>Seconds of one pulse. [tune 0.4–3]</summary>
        public static float DetailPulseSeconds = Defaults.DetailPulseSeconds;

        // ---- Луч-подсветка деталей (SCREENS «Детали в сцене») ------------------------------------
        /// <summary>Seconds between two passes of the light. [tune 4–20]</summary>
        public static float SweepPeriodSeconds = Defaults.SweepPeriodSeconds;
        /// <summary>How long one pass takes, s. [tune 0.6–2.5]</summary>
        public static float SweepDurationSeconds = Defaults.SweepDurationSeconds;
        /// <summary>Width of the band, design px. [tune 150–600]</summary>
        public static float SweepWidthPx = Defaults.SweepWidthPx;
        /// <summary>How bright the band makes a detail, 0..1. [tune 0.1–1]</summary>
        public static float SweepStrength = Defaults.SweepStrength;
        /// <summary>Light only the details the player has not noticed yet? [toggle]</summary>
        public static bool SweepOnlyUnnoticed = Defaults.SweepOnlyUnnoticed;
        /// <summary>Longer gaps on later levels (period ×1.5 per level) — fewer hints as it gets hard. [toggle]</summary>
        public static bool SweepRarerOnLateLevels = Defaults.SweepRarerOnLateLevels;

        // ---- Неон-обводка деталей ----------------------------------------------------------------
        /// <summary>Draw a neon rim around every uncollected detail? [toggle] — OFF by default.</summary>
        public static bool DetailNeonOutline = Defaults.DetailNeonOutline;
        /// <summary>How far that rim spreads past the sprite's own alpha, design px. [tune 2–20]</summary>
        public static float DetailOutlinePx = Defaults.DetailOutlinePx;
        /// <summary>How bright it burns, 0..1. [tune]</summary>
        public static float DetailOutlineStrength = Defaults.DetailOutlineStrength;

        // ---- §6 Прогрессия сложности — [tune per level] ---------------------------------------
        // Flat fields, one per level, rather than an array of bands: TuningStore persists every
        // mutable static field of this class by reflection, and its round-trip test walks the same
        // set — so a per-level knob is saved, restored and covered the moment it is declared here.
        // An array would have needed a second serialiser and a second test to trust it.

        public static int L1ThoughtBudget = Defaults.L1ThoughtBudget;
        public static float L1WaveIntervalSeconds = Defaults.L1WaveIntervalSeconds;
        public static int L1WaveWeak = Defaults.L1WaveWeak;
        public static int L1WaveMedium = Defaults.L1WaveMedium;
        public static int L1WaveStrong = Defaults.L1WaveStrong;
        public static int L1DurabilityWeak = Defaults.L1DurabilityWeak;
        public static int L1DurabilityMedium = Defaults.L1DurabilityMedium;
        public static int L1DurabilityStrong = Defaults.L1DurabilityStrong;
        public static float L1DriftPxPerSec = Defaults.L1DriftPxPerSec;
        public static float L1PressureRampPercent = Defaults.L1PressureRampPercent;
        public static float L1ThoughtGrowthPercentPerSec = Defaults.L1ThoughtGrowthPercentPerSec;
        public static float L1ThoughtGrowthCap = Defaults.L1ThoughtGrowthCap;

        public static int L2ThoughtBudget = Defaults.L2ThoughtBudget;
        public static float L2WaveIntervalSeconds = Defaults.L2WaveIntervalSeconds;
        public static int L2WaveWeak = Defaults.L2WaveWeak;
        public static int L2WaveMedium = Defaults.L2WaveMedium;
        public static int L2WaveStrong = Defaults.L2WaveStrong;
        public static int L2DurabilityWeak = Defaults.L2DurabilityWeak;
        public static int L2DurabilityMedium = Defaults.L2DurabilityMedium;
        public static int L2DurabilityStrong = Defaults.L2DurabilityStrong;
        public static float L2DriftPxPerSec = Defaults.L2DriftPxPerSec;
        public static float L2PressureRampPercent = Defaults.L2PressureRampPercent;
        public static float L2ThoughtGrowthPercentPerSec = Defaults.L2ThoughtGrowthPercentPerSec;
        public static float L2ThoughtGrowthCap = Defaults.L2ThoughtGrowthCap;

        public static int L3ThoughtBudget = Defaults.L3ThoughtBudget;
        public static float L3WaveIntervalSeconds = Defaults.L3WaveIntervalSeconds;
        public static int L3WaveWeak = Defaults.L3WaveWeak;
        public static int L3WaveMedium = Defaults.L3WaveMedium;
        public static int L3WaveStrong = Defaults.L3WaveStrong;
        public static int L3DurabilityWeak = Defaults.L3DurabilityWeak;
        public static int L3DurabilityMedium = Defaults.L3DurabilityMedium;
        public static int L3DurabilityStrong = Defaults.L3DurabilityStrong;
        public static float L3DriftPxPerSec = Defaults.L3DriftPxPerSec;
        public static float L3PressureRampPercent = Defaults.L3PressureRampPercent;
        public static float L3ThoughtGrowthPercentPerSec = Defaults.L3ThoughtGrowthPercentPerSec;
        public static float L3ThoughtGrowthCap = Defaults.L3ThoughtGrowthCap;

        public static int L4ThoughtBudget = Defaults.L4ThoughtBudget;
        public static float L4WaveIntervalSeconds = Defaults.L4WaveIntervalSeconds;
        public static int L4WaveWeak = Defaults.L4WaveWeak;
        public static int L4WaveMedium = Defaults.L4WaveMedium;
        public static int L4WaveStrong = Defaults.L4WaveStrong;
        public static int L4DurabilityWeak = Defaults.L4DurabilityWeak;
        public static int L4DurabilityMedium = Defaults.L4DurabilityMedium;
        public static int L4DurabilityStrong = Defaults.L4DurabilityStrong;
        public static float L4DriftPxPerSec = Defaults.L4DriftPxPerSec;
        public static float L4PressureRampPercent = Defaults.L4PressureRampPercent;
        public static float L4ThoughtGrowthPercentPerSec = Defaults.L4ThoughtGrowthPercentPerSec;
        public static float L4ThoughtGrowthCap = Defaults.L4ThoughtGrowthCap;

        public static int L5ThoughtBudget = Defaults.L5ThoughtBudget;
        public static float L5WaveIntervalSeconds = Defaults.L5WaveIntervalSeconds;
        public static int L5WaveWeak = Defaults.L5WaveWeak;
        public static int L5WaveMedium = Defaults.L5WaveMedium;
        public static int L5WaveStrong = Defaults.L5WaveStrong;
        public static int L5DurabilityWeak = Defaults.L5DurabilityWeak;
        public static int L5DurabilityMedium = Defaults.L5DurabilityMedium;
        public static int L5DurabilityStrong = Defaults.L5DurabilityStrong;
        public static float L5DriftPxPerSec = Defaults.L5DriftPxPerSec;
        public static float L5PressureRampPercent = Defaults.L5PressureRampPercent;
        public static float L5ThoughtGrowthPercentPerSec = Defaults.L5ThoughtGrowthPercentPerSec;
        public static float L5ThoughtGrowthCap = Defaults.L5ThoughtGrowthCap;

        // ---- Стенд ---------------------------------------------------------------------------
        /// <summary>Is the tuning panel expanded? Remembered between scenettes like every other value.</summary>
        public static bool PanelVisible = Defaults.PanelVisible;

        /// <summary>Total thoughts in one wave (composition of the three types).</summary>
        public static int ThoughtsPerWave => Mathf.Max(1, WaveWeak + WaveMedium + WaveStrong);

        /// <summary>Back to the shipped defaults (the panel's "сброс" button).</summary>
        public static void ResetToDefaults()
        {
            // Everything below is about to be written, so the band's way home is the defaults now —
            // keeping the old snapshot would let a later LeaveLevelBand undo this reset in part.
            _standBand = null;

            CrankThresholdDegPerSec = Defaults.CrankThresholdDegPerSec;
            GraceMs = Defaults.GraceMs;
            CollectSeconds = Defaults.CollectSeconds;
            StopMode = Defaults.StopMode;
            DecayPerSecond = Defaults.DecayPerSecond;
            SpeedMode = Defaults.SpeedMode;
            CrankReferenceDegPerSec = Defaults.CrankReferenceDegPerSec;

            Notice = Defaults.Notice;
            GazeSpeedPxPerSec = Defaults.GazeSpeedPxPerSec;
            GazeDwellSeconds = Defaults.GazeDwellSeconds;
            GazeRadiusPx = Defaults.GazeRadiusPx;
            GazeNeonGlow = Defaults.GazeNeonGlow;
            GazeNeonRingPx = Defaults.GazeNeonRingPx;

            SwipeAmplitude = Defaults.SwipeAmplitude;
            SwipeSharpness = Defaults.SwipeSharpness;
            SwipeCooldownMs = Defaults.SwipeCooldownMs;
            DurabilityWeak = Defaults.DurabilityWeak;
            DurabilityMedium = Defaults.DurabilityMedium;
            DurabilityStrong = Defaults.DurabilityStrong;
            HitDecayEnabled = Defaults.HitDecayEnabled;
            HitDecayMs = Defaults.HitDecayMs;
            Targeting = Defaults.Targeting;

            WaveIntervalSeconds = Defaults.WaveIntervalSeconds;
            WaveWeak = Defaults.WaveWeak;
            WaveMedium = Defaults.WaveMedium;
            WaveStrong = Defaults.WaveStrong;
            PressureRamp = Defaults.PressureRamp;
            PressureRampPercent = Defaults.PressureRampPercent;
            ThoughtDrift = Defaults.ThoughtDrift;
            DriftPxPerSec = Defaults.DriftPxPerSec;
            ThoughtsCoverVessel = Defaults.ThoughtsCoverVessel;
            ThoughtBudget = Defaults.ThoughtBudget;
            LossOverlapPercent = Defaults.LossOverlapPercent;
            PeakOverlapPercent = Defaults.PeakOverlapPercent;

            ThoughtBacking = Defaults.ThoughtBacking;
            ThoughtGrowthPercentPerSec = Defaults.ThoughtGrowthPercentPerSec;
            ThoughtGrowthCap = Defaults.ThoughtGrowthCap;
            BreatherEnabled = Defaults.BreatherEnabled;
            BreatherSeconds = Defaults.BreatherSeconds;
            AutoRetry = Defaults.AutoRetry;

            AudioBackgroundVolume = Defaults.AudioBackgroundVolume;
            AudioMeditationFadeInSeconds = Defaults.AudioMeditationFadeInSeconds;
            AudioMeditationFadeOutSeconds = Defaults.AudioMeditationFadeOutSeconds;
            AudioMeditationTailSeconds = Defaults.AudioMeditationTailSeconds;
            AudioMeditationReplacesBackground = Defaults.AudioMeditationReplacesBackground;
            AudioThoughtsMaxVolume = Defaults.AudioThoughtsMaxVolume;
            AudioThoughtsAtCount = Defaults.AudioThoughtsAtCount;
            AudioThoughtsSmoothingSeconds = Defaults.AudioThoughtsSmoothingSeconds;
            AudioThoughtsByOverlap = Defaults.AudioThoughtsByOverlap;
            AudioSilentOffLevel = Defaults.AudioSilentOffLevel;

            SweepPeriodSeconds = Defaults.SweepPeriodSeconds;
            SweepDurationSeconds = Defaults.SweepDurationSeconds;
            SweepWidthPx = Defaults.SweepWidthPx;
            SweepStrength = Defaults.SweepStrength;
            SweepOnlyUnnoticed = Defaults.SweepOnlyUnnoticed;
            SweepRarerOnLateLevels = Defaults.SweepRarerOnLateLevels;
            DetailNeonOutline = Defaults.DetailNeonOutline;
            DetailOutlinePx = Defaults.DetailOutlinePx;
            DetailOutlineStrength = Defaults.DetailOutlineStrength;

            L1ThoughtBudget = Defaults.L1ThoughtBudget;
            L1WaveIntervalSeconds = Defaults.L1WaveIntervalSeconds;
            L1WaveWeak = Defaults.L1WaveWeak;
            L1WaveMedium = Defaults.L1WaveMedium;
            L1WaveStrong = Defaults.L1WaveStrong;
            L1DurabilityWeak = Defaults.L1DurabilityWeak;
            L1DurabilityMedium = Defaults.L1DurabilityMedium;
            L1DurabilityStrong = Defaults.L1DurabilityStrong;
            L1DriftPxPerSec = Defaults.L1DriftPxPerSec;
            L1PressureRampPercent = Defaults.L1PressureRampPercent;
            L1ThoughtGrowthPercentPerSec = Defaults.L1ThoughtGrowthPercentPerSec;
            L1ThoughtGrowthCap = Defaults.L1ThoughtGrowthCap;

            L2ThoughtBudget = Defaults.L2ThoughtBudget;
            L2WaveIntervalSeconds = Defaults.L2WaveIntervalSeconds;
            L2WaveWeak = Defaults.L2WaveWeak;
            L2WaveMedium = Defaults.L2WaveMedium;
            L2WaveStrong = Defaults.L2WaveStrong;
            L2DurabilityWeak = Defaults.L2DurabilityWeak;
            L2DurabilityMedium = Defaults.L2DurabilityMedium;
            L2DurabilityStrong = Defaults.L2DurabilityStrong;
            L2DriftPxPerSec = Defaults.L2DriftPxPerSec;
            L2PressureRampPercent = Defaults.L2PressureRampPercent;
            L2ThoughtGrowthPercentPerSec = Defaults.L2ThoughtGrowthPercentPerSec;
            L2ThoughtGrowthCap = Defaults.L2ThoughtGrowthCap;

            L3ThoughtBudget = Defaults.L3ThoughtBudget;
            L3WaveIntervalSeconds = Defaults.L3WaveIntervalSeconds;
            L3WaveWeak = Defaults.L3WaveWeak;
            L3WaveMedium = Defaults.L3WaveMedium;
            L3WaveStrong = Defaults.L3WaveStrong;
            L3DurabilityWeak = Defaults.L3DurabilityWeak;
            L3DurabilityMedium = Defaults.L3DurabilityMedium;
            L3DurabilityStrong = Defaults.L3DurabilityStrong;
            L3DriftPxPerSec = Defaults.L3DriftPxPerSec;
            L3PressureRampPercent = Defaults.L3PressureRampPercent;
            L3ThoughtGrowthPercentPerSec = Defaults.L3ThoughtGrowthPercentPerSec;
            L3ThoughtGrowthCap = Defaults.L3ThoughtGrowthCap;

            L4ThoughtBudget = Defaults.L4ThoughtBudget;
            L4WaveIntervalSeconds = Defaults.L4WaveIntervalSeconds;
            L4WaveWeak = Defaults.L4WaveWeak;
            L4WaveMedium = Defaults.L4WaveMedium;
            L4WaveStrong = Defaults.L4WaveStrong;
            L4DurabilityWeak = Defaults.L4DurabilityWeak;
            L4DurabilityMedium = Defaults.L4DurabilityMedium;
            L4DurabilityStrong = Defaults.L4DurabilityStrong;
            L4DriftPxPerSec = Defaults.L4DriftPxPerSec;
            L4PressureRampPercent = Defaults.L4PressureRampPercent;
            L4ThoughtGrowthPercentPerSec = Defaults.L4ThoughtGrowthPercentPerSec;
            L4ThoughtGrowthCap = Defaults.L4ThoughtGrowthCap;

            L5ThoughtBudget = Defaults.L5ThoughtBudget;
            L5WaveIntervalSeconds = Defaults.L5WaveIntervalSeconds;
            L5WaveWeak = Defaults.L5WaveWeak;
            L5WaveMedium = Defaults.L5WaveMedium;
            L5WaveStrong = Defaults.L5WaveStrong;
            L5DurabilityWeak = Defaults.L5DurabilityWeak;
            L5DurabilityMedium = Defaults.L5DurabilityMedium;
            L5DurabilityStrong = Defaults.L5DurabilityStrong;
            L5DriftPxPerSec = Defaults.L5DriftPxPerSec;
            L5PressureRampPercent = Defaults.L5PressureRampPercent;
            L5ThoughtGrowthPercentPerSec = Defaults.L5ThoughtGrowthPercentPerSec;
            L5ThoughtGrowthCap = Defaults.L5ThoughtGrowthCap;

            PanelVisible = Defaults.PanelVisible;
        }

        // ---- the per-level band, as one addressable record --------------------------------------

        private static int _activeLevelIndex;

        /// <summary>
        /// The level currently on screen, 0-based. A property, not a field, precisely so
        /// <see cref="TuningStore"/> does not persist it: where the player is is not a tuning value,
        /// and a stale one in the file would apply the wrong band on the next start.
        /// </summary>
        public static int ActiveLevelIndex
        {
            get => _activeLevelIndex;
            set => _activeLevelIndex = value;
        }

        /// <summary>
        /// Copy level <paramref name="index"/>'s band (0-based) into the live values the mechanics
        /// read. The rules classes (<c>ThoughtField</c>, <c>LevelRules</c>, <c>Thought</c>) keep
        /// reading plain statics — the level machinery decides WHICH numbers those are, and does it
        /// in exactly one place, at the moment a level starts.
        ///
        /// Consequence worth knowing: starting a game level overwrites the shared values the preview
        /// stand also uses. That is deliberate — after this increment the levels are where tuning
        /// happens — but it is only survivable because the stand can get them BACK: see
        /// <see cref="LeaveLevelBand"/>, which every stand scene calls as it boots.
        /// </summary>
        public static void ApplyLevel(int index)
        {
            // The first level of a session puts the stand's own values aside. Only the first: the
            // panel re-applies the active band on every slider drag (see Write), and a snapshot taken
            // then would be a snapshot OF the level.
            if (_standBand == null)
            {
                _standBand = new float[BandValues.Length];
                for (int i = 0; i < BandValues.Length; i++) _standBand[i] = BandValues[i].Read();
            }

            for (int i = 0; i < BandValues.Length; i++) BandValues[i].Write(BandValues[i].OfLevel(index));
        }

        /// <summary>
        /// Put back what the stand was running on before some level's band was copied over it —
        /// «здесь уровня нет», said by every scene of the preview stand as it boots.
        ///
        /// Without it a level leaks into the greybox rigs: «Уровень 5» off the stand menu leaves
        /// <see cref="ThoughtBudget"/> at 25, and «Отгон взмахами», which is a rig for ONE rule and
        /// must be able to send blobs for as long as the founder watches it, stops sending them after
        /// the twenty-fifth (Codex, 2026-09-22). The other twelve values of the band are the stand's
        /// own panel rows, so returning them is the same statement: the stand runs on the numbers its
        /// own sliders show, not on the ones the last level happened to leave behind.
        ///
        /// Restores the values the stand HAD, not <see cref="Defaults"/> — the founder tunes those
        /// sliders in the scenettes and the panel persists them, and a reset on entry would throw her
        /// session away every time she walked through a level.
        ///
        /// A no-op when no level has run: there is nothing to give back, and the live values are
        /// whatever the tuning file loaded.
        /// </summary>
        public static void LeaveLevelBand()
        {
            if (_standBand == null) return;

            for (int i = 0; i < BandValues.Length; i++) BandValues[i].Write(_standBand[i]);
            _standBand = null;
        }

        /// <summary>True while a level's band is sitting on the shared values.</summary>
        public static bool ALevelBandIsApplied => _standBand != null;

        /// <summary>What the stand was running on before the first <see cref="ApplyLevel"/>.</summary>
        private static float[] _standBand;

        /// <summary>
        /// One live value a level band owns: how to read it, how to write it, and what level N's band
        /// says it should be.
        ///
        /// Floats for all of them — the band carries counts, seconds and one toggle, and every one of
        /// those survives a round trip through a float exactly. The alternative (three parallel tables,
        /// one per type) is three places to forget a value in.
        /// </summary>
        private readonly struct BandValue
        {
            public readonly Func<float> Read;
            public readonly Action<float> Write;
            public readonly Func<int, float> OfLevel;

            public BandValue(Func<float> read, Action<float> write, Func<int, float> ofLevel)
            {
                Read = read;
                Write = write;
                OfLevel = ofLevel;
            }
        }

        /// <summary>
        /// THE band: every shared value a level owns while it is on screen, in one table.
        ///
        /// One table rather than a list of assignments in <see cref="ApplyLevel"/> plus a second list
        /// somewhere that undoes them, because the second list is the one that falls behind. A value
        /// added to a level band from now on is applied AND given back by construction, and
        /// <c>LevelBand_GivesBackEveryValueItTakes</c> holds the line by reflection if anyone writes a
        /// shared value from ApplyLevel without adding a row here.
        /// </summary>
        private static readonly BandValue[] BandValues =
        {
            new BandValue(() => ThoughtBudget, v => ThoughtBudget = Mathf.RoundToInt(v),
                i => ThoughtBudgetOf(i)),
            new BandValue(() => WaveIntervalSeconds, v => WaveIntervalSeconds = v,
                i => WaveIntervalOf(i)),
            new BandValue(() => WaveWeak, v => WaveWeak = Mathf.RoundToInt(v), i => WaveWeakOf(i)),
            new BandValue(() => WaveMedium, v => WaveMedium = Mathf.RoundToInt(v), i => WaveMediumOf(i)),
            new BandValue(() => WaveStrong, v => WaveStrong = Mathf.RoundToInt(v), i => WaveStrongOf(i)),
            new BandValue(() => DurabilityWeak, v => DurabilityWeak = Mathf.RoundToInt(v),
                i => DurabilityWeakOf(i)),
            new BandValue(() => DurabilityMedium, v => DurabilityMedium = Mathf.RoundToInt(v),
                i => DurabilityMediumOf(i)),
            new BandValue(() => DurabilityStrong, v => DurabilityStrong = Mathf.RoundToInt(v),
                i => DurabilityStrongOf(i)),
            new BandValue(() => DriftPxPerSec, v => DriftPxPerSec = v, i => DriftOf(i)),
            new BandValue(() => PressureRampPercent, v => PressureRampPercent = v,
                i => PressureRampPercentOf(i)),
            new BandValue(() => ThoughtGrowthPercentPerSec, v => ThoughtGrowthPercentPerSec = v,
                i => ThoughtGrowthPercentPerSecOf(i)),
            new BandValue(() => ThoughtGrowthCap, v => ThoughtGrowthCap = v, i => ThoughtGrowthCapOf(i)),

            // «Рост давления внутри уровня» is on exactly when this level's band asks for it — a 0 %
            // shortening is the same statement as "no ramp", so one number says both. On the way back
            // the stand's own toggle returns, which is why it is a row here and not a line derived
            // after the loop.
            new BandValue(() => PressureRamp ? 1f : 0f, v => PressureRamp = v > 0.5f,
                i => PressureRampPercentOf(i) > 0.01f ? 1f : 0f)
        };

        /// <summary>Band index of a level, clamped: the flow never asks for one that is not there.</summary>
        private static int Band(int i) => Mathf.Clamp(i, 0, LevelBands - 1);

        private static float Pick(int i, float a, float b, float c, float d, float e)
        {
            switch (Band(i))
            {
                case 0: return a;
                case 1: return b;
                case 2: return c;
                case 3: return d;
                default: return e;
            }
        }

        private static int Pick(int i, int a, int b, int c, int d, int e)
        {
            switch (Band(i))
            {
                case 0: return a;
                case 1: return b;
                case 2: return c;
                case 3: return d;
                default: return e;
            }
        }

        /// <summary>
        /// The level's total supply of thoughts — «общий запас» (founder, 2026-09-22). 5/10/15/20/25.
        /// </summary>
        public static int ThoughtBudgetOf(int i) =>
            Pick(i, L1ThoughtBudget, L2ThoughtBudget, L3ThoughtBudget, L4ThoughtBudget, L5ThoughtBudget);

        /// <summary>How many waves the budget pays for on this level — five, by design.</summary>
        public static int WavesInBudgetOf(int i) =>
            Mathf.Max(1, Mathf.CeilToInt(ThoughtBudgetOf(i) / (float)Mathf.Max(1, ThoughtsPerWaveOf(i))));

        public static float WaveIntervalOf(int i) =>
            Pick(i, L1WaveIntervalSeconds, L2WaveIntervalSeconds, L3WaveIntervalSeconds,
                L4WaveIntervalSeconds, L5WaveIntervalSeconds);

        public static int WaveWeakOf(int i) =>
            Pick(i, L1WaveWeak, L2WaveWeak, L3WaveWeak, L4WaveWeak, L5WaveWeak);

        public static int WaveMediumOf(int i) =>
            Pick(i, L1WaveMedium, L2WaveMedium, L3WaveMedium, L4WaveMedium, L5WaveMedium);

        public static int WaveStrongOf(int i) =>
            Pick(i, L1WaveStrong, L2WaveStrong, L3WaveStrong, L4WaveStrong, L5WaveStrong);

        public static int DurabilityWeakOf(int i) =>
            Pick(i, L1DurabilityWeak, L2DurabilityWeak, L3DurabilityWeak, L4DurabilityWeak,
                L5DurabilityWeak);

        public static int DurabilityMediumOf(int i) =>
            Pick(i, L1DurabilityMedium, L2DurabilityMedium, L3DurabilityMedium, L4DurabilityMedium,
                L5DurabilityMedium);

        public static int DurabilityStrongOf(int i) =>
            Pick(i, L1DurabilityStrong, L2DurabilityStrong, L3DurabilityStrong, L4DurabilityStrong,
                L5DurabilityStrong);

        public static float DriftOf(int i) =>
            Pick(i, L1DriftPxPerSec, L2DriftPxPerSec, L3DriftPxPerSec, L4DriftPxPerSec, L5DriftPxPerSec);

        public static float PressureRampPercentOf(int i) =>
            Pick(i, L1PressureRampPercent, L2PressureRampPercent, L3PressureRampPercent,
                L4PressureRampPercent, L5PressureRampPercent);

        public static float ThoughtGrowthPercentPerSecOf(int i) =>
            Pick(i, L1ThoughtGrowthPercentPerSec, L2ThoughtGrowthPercentPerSec,
                L3ThoughtGrowthPercentPerSec, L4ThoughtGrowthPercentPerSec,
                L5ThoughtGrowthPercentPerSec);

        public static float ThoughtGrowthCapOf(int i) =>
            Pick(i, L1ThoughtGrowthCap, L2ThoughtGrowthCap, L3ThoughtGrowthCap, L4ThoughtGrowthCap,
                L5ThoughtGrowthCap);

        /// <summary>Thoughts sent by one wave of level <paramref name="i"/> — the progression's «больше мыслей».</summary>
        public static int ThoughtsPerWaveOf(int i) =>
            Mathf.Max(1, WaveWeakOf(i) + WaveMediumOf(i) + WaveStrongOf(i));

        // ---- per-level writers (the panel's setters) ---------------------------------------------
        // Each one re-applies the band when it belongs to the level being played, so a slider moved
        // during a level changes that level in the same frame (done contract §7 «правки действуют
        // сразу»), while editing another level's band only changes what happens when you get there.

        public static void SetThoughtBudget(int i, int v) { Write(i, ref L1ThoughtBudget, ref L2ThoughtBudget, ref L3ThoughtBudget, ref L4ThoughtBudget, ref L5ThoughtBudget, v); }
        public static void SetWaveInterval(int i, float v) { Write(i, ref L1WaveIntervalSeconds, ref L2WaveIntervalSeconds, ref L3WaveIntervalSeconds, ref L4WaveIntervalSeconds, ref L5WaveIntervalSeconds, v); }
        public static void SetDrift(int i, float v) { Write(i, ref L1DriftPxPerSec, ref L2DriftPxPerSec, ref L3DriftPxPerSec, ref L4DriftPxPerSec, ref L5DriftPxPerSec, v); }
        public static void SetPressureRampPercent(int i, float v) { Write(i, ref L1PressureRampPercent, ref L2PressureRampPercent, ref L3PressureRampPercent, ref L4PressureRampPercent, ref L5PressureRampPercent, v); }
        public static void SetThoughtGrowthPercentPerSec(int i, float v) { Write(i, ref L1ThoughtGrowthPercentPerSec, ref L2ThoughtGrowthPercentPerSec, ref L3ThoughtGrowthPercentPerSec, ref L4ThoughtGrowthPercentPerSec, ref L5ThoughtGrowthPercentPerSec, v); }
        public static void SetThoughtGrowthCap(int i, float v) { Write(i, ref L1ThoughtGrowthCap, ref L2ThoughtGrowthCap, ref L3ThoughtGrowthCap, ref L4ThoughtGrowthCap, ref L5ThoughtGrowthCap, v); }

        public static void SetWaveWeak(int i, int v) { Write(i, ref L1WaveWeak, ref L2WaveWeak, ref L3WaveWeak, ref L4WaveWeak, ref L5WaveWeak, v); }
        public static void SetWaveMedium(int i, int v) { Write(i, ref L1WaveMedium, ref L2WaveMedium, ref L3WaveMedium, ref L4WaveMedium, ref L5WaveMedium, v); }
        public static void SetWaveStrong(int i, int v) { Write(i, ref L1WaveStrong, ref L2WaveStrong, ref L3WaveStrong, ref L4WaveStrong, ref L5WaveStrong, v); }
        public static void SetDurabilityWeak(int i, int v) { Write(i, ref L1DurabilityWeak, ref L2DurabilityWeak, ref L3DurabilityWeak, ref L4DurabilityWeak, ref L5DurabilityWeak, v); }
        public static void SetDurabilityMedium(int i, int v) { Write(i, ref L1DurabilityMedium, ref L2DurabilityMedium, ref L3DurabilityMedium, ref L4DurabilityMedium, ref L5DurabilityMedium, v); }
        public static void SetDurabilityStrong(int i, int v) { Write(i, ref L1DurabilityStrong, ref L2DurabilityStrong, ref L3DurabilityStrong, ref L4DurabilityStrong, ref L5DurabilityStrong, v); }

        private static void Write(int index, ref float first, ref float second, ref float third,
            ref float fourth, ref float fifth, float value)
        {
            switch (Band(index))
            {
                case 0: first = value; break;
                case 1: second = value; break;
                case 2: third = value; break;
                case 3: fourth = value; break;
                default: fifth = value; break;
            }

            if (Band(index) == ActiveLevelIndex) ApplyLevel(index);
        }

        private static void Write(int index, ref int first, ref int second, ref int third,
            ref int fourth, ref int fifth, int value)
        {
            switch (Band(index))
            {
                case 0: first = value; break;
                case 1: second = value; break;
                case 2: third = value; break;
                case 3: fourth = value; break;
                default: fifth = value; break;
            }

            if (Band(index) == ActiveLevelIndex) ApplyLevel(index);
        }

        /// <summary>Hits needed to pop a thought of the given strength.</summary>
        public static int DurabilityOf(ThoughtStrength strength)
        {
            switch (strength)
            {
                case ThoughtStrength.Weak: return DurabilityWeak;
                case ThoughtStrength.Medium: return DurabilityMedium;
                default: return DurabilityStrong;
            }
        }
    }
}
