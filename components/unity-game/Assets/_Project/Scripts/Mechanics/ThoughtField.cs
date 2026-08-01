using System;
using System.Collections.Generic;
using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// MECHANICS §3–§4 — the thoughts themselves: waves, drift, hits, screen coverage.
    /// Pure logic over design-space rectangles (1920×1080, origin top-left); the view mirrors the list.
    /// </summary>
    public sealed class ThoughtField
    {
        public const float ScreenWidth = 1920f;
        public const float ScreenHeight = 1080f;

        private const int GridCols = 64;   // 30 px cells — coverage is a design threshold, not a physics sim
        private const int GridRows = 36;

        private readonly List<Thought> _thoughts = new List<Thought>();
        private readonly System.Random _random;
        private float _waveTimer;
        private int _labelCursor;
        private int _wavesSpawned;

        public ThoughtField(int seed = 12345)
        {
            _random = new System.Random(seed);
        }

        public IReadOnlyList<Thought> Thoughts => _thoughts;

        /// <summary>Seconds until the next wave (live readout).</summary>
        public float SecondsToNextWave => Mathf.Max(0f, _waveTimer);

        /// <summary>Waves spawned since the last <see cref="Clear"/> (drives the pressure ramp).</summary>
        public int WavesSpawned => _wavesSpawned;

        /// <summary>
        /// Interval before the next wave. With the pressure ramp [toggle] on, every wave inside the
        /// level shortens it by the tuned percentage (MECHANICS §4 "рост давления внутри уровня").
        /// </summary>
        public float CurrentIntervalSeconds
        {
            get
            {
                float interval = TuningConfig.WaveIntervalSeconds;
                if (TuningConfig.PressureRamp)
                {
                    float factor = Mathf.Clamp01(1f - TuningConfig.PressureRampPercent * 0.01f);
                    interval *= Mathf.Pow(factor, _wavesSpawned);
                }
                return Mathf.Max(0.5f, interval);
            }
        }

        /// <summary>Screen area covered by thoughts, 0..100 %.</summary>
        public float OverlapPercent { get; private set; }

        /// <summary>Raised when a thought runs out of pips.</summary>
        public event Action<Thought> Popped;

        /// <summary>Labels to cycle through — greybox names on the stand, art keys in the game.</summary>
        public string[] Labels = LevelOneData.ThoughtLabels;

        /// <summary>
        /// Optional: the size a freshly spawned thought is drawn at. The game sets it so a blob takes
        /// its silhouette's aspect (fitted inside the S/M/L class), which keeps the coverage maths and
        /// the picture talking about the same rectangle. Null on the stand — plain class sizes.
        /// </summary>
        public Func<Thought, Vector2> ArtSizer;

        public void Clear()
        {
            _thoughts.Clear();
            _waveTimer = 0f;
            _wavesSpawned = 0;
            OverlapPercent = 0f;
        }

        /// <summary>Restart the wave clock without touching the thoughts already on screen.</summary>
        public void ResetWaveTimer(float seconds)
        {
            _waveTimer = seconds;
        }

        /// <param name="deltaTime">Frame time.</param>
        /// <param name="stick">Current joystick vector (needed by targeting variant C).</param>
        /// <param name="hits">Shake hits registered this frame.</param>
        /// <param name="spawningAllowed">False during a breather / tutorial beat / finished level.</param>
        public void Tick(float deltaTime, Vector2 stick, int hits, bool spawningAllowed)
        {
            if (deltaTime > 0f)
            {
                if (spawningAllowed)
                {
                    _waveTimer -= deltaTime;
                    if (_waveTimer <= 0f)
                    {
                        SpawnWave();
                        _waveTimer = CurrentIntervalSeconds;
                    }
                }

                for (int i = 0; i < _thoughts.Count; i++)
                {
                    Thought t = _thoughts[i];
                    t.Age += deltaTime;
                    t.SecondsSinceHit += deltaTime;

                    if (TuningConfig.HitDecayEnabled && t.HitsTaken > 0 &&
                        t.SecondsSinceHit * 1000f > TuningConfig.HitDecayMs)
                    {
                        t.HitsTaken = 0;
                    }

                    if (TuningConfig.ThoughtDrift)
                        t.Position += t.DriftDirection * (TuningConfig.DriftPxPerSec * deltaTime);
                }
            }

            if (hits > 0) ApplyHits(hits, stick);

            OverlapPercent = ComputeOverlapPercent();
        }

        /// <summary>Land <paramref name="hits"/> shake hits according to the targeting [toggle].</summary>
        public void ApplyHits(int hits, Vector2 stick)
        {
            if (hits <= 0 || _thoughts.Count == 0) return;

            for (int h = 0; h < hits; h++)
            {
                switch (TuningConfig.Targeting)
                {
                    case ShakeTargeting.AllOnScreen:
                        for (int i = _thoughts.Count - 1; i >= 0; i--) Damage(_thoughts[i]);
                        break;

                    case ShakeTargeting.StickDirection:
                        Thought aimed = PickByStick(stick);
                        if (aimed != null) Damage(aimed);
                        break;

                    default:
                        Thought nearest = PickNearestToCentre();
                        if (nearest != null) Damage(nearest);
                        break;
                }
            }
        }

        /// <summary>
        /// Columns and rows the defeat wallpaper is laid on. The grid deliberately runs half a cell
        /// PAST every edge: mock 17 draws its blobs from −60 to 1980, i.e. the screen is covered by
        /// thoughts that hang off it, not by a mosaic that stops politely at the frame. With the grid
        /// inside the frame the defeat screen came out 79 % covered — a fifth of the level still
        /// showing through the thing that is supposed to have taken it.
        /// </summary>
        public const int CoverColumns = 9;
        public const int CoverRows = 6;

        /// <summary>How far past the frame the wallpaper starts, as a share of one cell.</summary>
        public const float CoverBleed = 0.5f;

        /// <summary>Each wallpaper blob is drawn this much larger than its cell, so seams close.</summary>
        public const float CoverCellOversize = 1.35f;

        /// <summary>
        /// Close the screen over with thoughts, filling only the parts that are still open.
        ///
        /// The defeat screen IS thoughts (SCREENS S5: «экран целиком закрыт мыслями»), and the retry is
        /// wiping them off with the crank — so a level lost on the clock, where the screen may be almost
        /// clear, still has to close over before it can be cleared. Ordinary <see cref="Spawn"/> cannot
        /// do this: with drift on it puts a thought just OUTSIDE the frame so it can sail in, which
        /// covers nothing at all. Here the blobs are placed on a grid, and cells that are already hidden
        /// are skipped — so the thoughts that actually beat the player stay exactly where they beat them.
        /// </summary>
        /// <returns>How many thoughts were added.</returns>
        public int CoverScreen(int cap = CoverColumns * CoverRows)
        {
            // The grid spans the frame plus a bleed on every side, so the wallpaper runs off the edges
            // the way mock 17 draws it.
            float spanW = ScreenWidth + 2f * CoverBleed * (ScreenWidth / CoverColumns);
            float spanH = ScreenHeight + 2f * CoverBleed * (ScreenHeight / CoverRows);
            float cellW = spanW / CoverColumns;
            float cellH = spanH / CoverRows;
            float originX = -CoverBleed * (ScreenWidth / CoverColumns);
            float originY = -CoverBleed * (ScreenHeight / CoverRows);
            int added = 0;

            for (int row = 0; row < CoverRows; row++)
            for (int col = 0; col < CoverColumns; col++)
            {
                if (_thoughts.Count >= cap) break;

                var spot = new Vector2(originX + (col + 0.5f) * cellW, originY + (row + 0.5f) * cellH);
                if (IsCovered(spot)) continue;

                Thought blob = Spawn(ThoughtStrength.Strong);
                blob.Position = spot;

                // A silhouette fitted inside its class box leaves a gap around itself; the wallpaper
                // needs the cell CLOSED, so a defeat blob is sized to its cell, not to its class.
                blob.ArtSize = new Vector2(cellW, cellH) * CoverCellOversize;
                blob.Wallpaper = true;
                added++;
            }

            OverlapPercent = ComputeOverlapPercent();
            return added;
        }

        /// <summary>Remove a thought without a hit (used by the "crank away the defeat screen" retry).</summary>
        public bool RemoveOldest()
        {
            if (_thoughts.Count == 0) return false;
            int oldest = 0;
            for (int i = 1; i < _thoughts.Count; i++)
                if (_thoughts[i].Age > _thoughts[oldest].Age) oldest = i;

            Thought t = _thoughts[oldest];
            _thoughts.RemoveAt(oldest);
            Popped?.Invoke(t);
            OverlapPercent = ComputeOverlapPercent();
            return true;
        }

        /// <summary>Is this design-space point hidden under a thought? (targeting variant C, auto-notice)</summary>
        public bool IsCovered(Vector2 point)
        {
            for (int i = 0; i < _thoughts.Count; i++)
                if (_thoughts[i].Rect.Contains(point)) return true;
            return false;
        }

        /// <summary>
        /// One wave, composed the way the panel says: N weak + N medium + N strong
        /// (MECHANICS §4 "состав волны: сколько и каких мыслей"). An all-zero composition still
        /// sends one weak thought, so a mis-set panel cannot silently stop the game.
        /// </summary>
        public Thought SpawnWave()
        {
            int weak = Mathf.Max(0, TuningConfig.WaveWeak);
            int medium = Mathf.Max(0, TuningConfig.WaveMedium);
            int strong = Mathf.Max(0, TuningConfig.WaveStrong);
            if (weak + medium + strong <= 0) weak = 1;

            Thought last = null;
            for (int i = 0; i < weak; i++) last = Spawn(ThoughtStrength.Weak);
            for (int i = 0; i < medium; i++) last = Spawn(ThoughtStrength.Medium);
            for (int i = 0; i < strong; i++) last = Spawn(ThoughtStrength.Strong);

            _wavesSpawned++;
            return last;
        }

        /// <summary>Spawn one thought: from a screen edge when drifting, on-screen when static.</summary>
        public Thought Spawn(ThoughtStrength strength)
        {
            var thought = new Thought { Strength = strength, Label = NextLabel() };

            // The art size is decided before the spawn point, because where a blob enters the screen
            // is measured off its own edge — a silhouette narrower than its class must not start with
            // a gap between it and the frame.
            if (ArtSizer != null) thought.ArtSize = ArtSizer(thought);

            Vector2 size = thought.Size;
            Vector2 position;

            if (TuningConfig.ThoughtDrift)
            {
                int side = _random.Next(4);
                switch (side)
                {
                    case 0: position = new Vector2(Range(size.x * 0.5f, ScreenWidth - size.x * 0.5f), -size.y * 0.4f); break;
                    case 1: position = new Vector2(Range(size.x * 0.5f, ScreenWidth - size.x * 0.5f), ScreenHeight + size.y * 0.4f); break;
                    case 2: position = new Vector2(-size.x * 0.4f, Range(size.y * 0.5f, ScreenHeight - size.y * 0.5f)); break;
                    default: position = new Vector2(ScreenWidth + size.x * 0.4f, Range(size.y * 0.5f, ScreenHeight - size.y * 0.5f)); break;
                }
            }
            else
            {
                position = new Vector2(
                    Range(size.x * 0.5f, ScreenWidth - size.x * 0.5f),
                    Range(size.y * 0.5f, ScreenHeight * 0.75f));
            }

            var centre = new Vector2(ScreenWidth * 0.5f, ScreenHeight * 0.5f);
            Vector2 toCentre = centre - position;
            thought.Position = position;
            thought.DriftDirection = toCentre.sqrMagnitude > 0.001f ? toCentre.normalized : Vector2.zero;
            _thoughts.Add(thought);
            OverlapPercent = ComputeOverlapPercent();
            return thought;
        }

        /// <summary>
        /// A named thought at a named place. The level-1 tutorial needs exactly this: SCREENS puts the
        /// plush bear ON TOP of the next detail so that the collection really is blocked until it is
        /// shaken off — a random spawn could land anywhere and teach nothing.
        /// </summary>
        public Thought SpawnAt(ThoughtStrength strength, string label, Vector2 position)
        {
            var thought = new Thought { Strength = strength, Label = label, Position = position };
            if (ArtSizer != null) thought.ArtSize = ArtSizer(thought);

            Vector2 centre = new Vector2(ScreenWidth * 0.5f, ScreenHeight * 0.5f);
            Vector2 toCentre = centre - position;
            thought.DriftDirection = toCentre.sqrMagnitude > 0.001f ? toCentre.normalized : Vector2.zero;

            _thoughts.Add(thought);
            OverlapPercent = ComputeOverlapPercent();
            return thought;
        }

        private void Damage(Thought thought)
        {
            thought.HitsTaken++;
            thought.SecondsSinceHit = 0f;
            if (!thought.IsPopped) return;

            _thoughts.Remove(thought);
            Popped?.Invoke(thought);
            OverlapPercent = ComputeOverlapPercent();
        }

        private Thought PickNearestToCentre()
        {
            var centre = new Vector2(ScreenWidth * 0.5f, ScreenHeight * 0.5f);
            Thought best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _thoughts.Count; i++)
            {
                float d = (_thoughts[i].Position - centre).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = _thoughts[i];
            }
            return best;
        }

        private Thought PickByStick(Vector2 stick)
        {
            if (stick.sqrMagnitude < 0.0001f) return PickNearestToCentre();

            // Design space grows downwards, the stick's Y grows upwards.
            Vector2 aim = new Vector2(stick.x, -stick.y).normalized;
            var centre = new Vector2(ScreenWidth * 0.5f, ScreenHeight * 0.5f);

            Thought best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < _thoughts.Count; i++)
            {
                Vector2 offset = _thoughts[i].Position - centre;
                if (offset.sqrMagnitude < 0.0001f) continue;
                float score = Vector2.Dot(offset.normalized, aim);
                if (score <= bestScore) continue;
                bestScore = score;
                best = _thoughts[i];
            }
            return best ?? PickNearestToCentre();
        }

        private float ComputeOverlapPercent()
        {
            if (_thoughts.Count == 0) return 0f;

            float cellW = ScreenWidth / GridCols;
            float cellH = ScreenHeight / GridRows;
            int covered = 0;

            for (int row = 0; row < GridRows; row++)
            {
                float y = (row + 0.5f) * cellH;
                for (int col = 0; col < GridCols; col++)
                {
                    float x = (col + 0.5f) * cellW;
                    for (int i = 0; i < _thoughts.Count; i++)
                    {
                        if (!_thoughts[i].Rect.Contains(new Vector2(x, y))) continue;
                        covered++;
                        break;
                    }
                }
            }

            return covered * 100f / (GridCols * GridRows);
        }

        /// <summary>
        /// The next label off the registry, skipping the ones already on screen. Two «фото бывшего»
        /// side by side in one frame read as a rendering bug, and the pool cannot be widened to fix it:
        /// the walkthrough's registry is canon, inventing new thoughts is not ours to do. So the rule is
        /// the weaker one that needs no new text — never hand out a duplicate while the registry still
        /// has an unused entry. Past that (more thoughts on screen than the registry has lines) it falls
        /// back to the plain cycle, which keeps consecutive spawns different in any case.
        /// </summary>
        private string NextLabel()
        {
            if (Labels == null || Labels.Length == 0) return "мысль";

            for (int step = 0; step < Labels.Length; step++)
            {
                int index = (_labelCursor + step) % Labels.Length;
                if (IsOnScreen(Labels[index])) continue;
                _labelCursor = (index + 1) % Labels.Length;
                return Labels[index];
            }

            string label = Labels[_labelCursor % Labels.Length];
            _labelCursor = (_labelCursor + 1) % Labels.Length;
            return label;
        }

        private bool IsOnScreen(string label)
        {
            for (int i = 0; i < _thoughts.Count; i++)
                if (_thoughts[i].Label == label) return true;
            return false;
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    }
}
