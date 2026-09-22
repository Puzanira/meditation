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
        private int _spentFromBudget;

        public ThoughtField(int seed = 12345)
        {
            _random = new System.Random(seed);
        }

        public IReadOnlyList<Thought> Thoughts => _thoughts;

        /// <summary>Seconds until the next wave (live readout).</summary>
        public float SecondsToNextWave => Mathf.Max(0f, _waveTimer);

        /// <summary>Waves spawned since the last <see cref="Clear"/> (drives the pressure ramp).</summary>
        public int WavesSpawned => _wavesSpawned;

        // ---- общий запас мыслей на уровень (founder, плейтест 2026-09-22) -----------------------

        /// <summary>
        /// How many thoughts this level still has to send — «общий запас», the founder's «волны».
        /// <see cref="Unlimited"/> when the level has no budget at all, which is what the greybox
        /// scenettes run on: a rig for one rule has to be able to go on sending blobs indefinitely.
        ///
        /// Counted on the WAVES only. The tutorial's own cat (<see cref="SpawnAt"/>) is a scripted
        /// beat of the lesson and not part of the level's supply, and the defeat wallpaper
        /// (<see cref="CoverScreen"/>) is not thoughts at all — it is the SCREENS S5 picture the crank
        /// rubs off. Charging either to the budget would mean a level 1 that teaches with a fifth of
        /// its own interference, or a defeat screen that could not be drawn because the level had
        /// already spent everything it had.
        /// </summary>
        public int BudgetLeft
        {
            get
            {
                int budget = TuningConfig.ThoughtBudget;
                if (budget <= 0) return Unlimited;
                return Mathf.Max(0, budget - _spentFromBudget);
            }
        }

        /// <summary>«Запас не ограничен» — what <see cref="BudgetLeft"/> answers on the stand.</summary>
        public const int Unlimited = int.MaxValue;

        /// <summary>Thoughts this level has already sent out of its supply.</summary>
        public int SpentFromBudget => _spentFromBudget;

        /// <summary>
        /// True once the level has sent everything it had AND the screen is clear of it — «мысли
        /// кончились», the state a player reaches by playing well. Always false without a budget.
        /// </summary>
        public bool ThoughtsAreOver =>
            TuningConfig.ThoughtBudget > 0 && BudgetLeft <= 0 && _thoughts.Count == 0;

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
        /// Optional: hand a freshly spawned thought the metrics of the sprite it will be drawn as —
        /// the rectangle (its silhouette's aspect, scaled to cover the S/M/L class) and how much of
        /// that rectangle the sprite paints. Null on the stand: greybox blobs are solid class boxes.
        ///
        /// One hook for both numbers rather than a sizer and an inker, because a thought sized off the
        /// drop but still counted as a solid rectangle is exactly the wrong half of the pair — see
        /// <see cref="Thought.Ink"/>. The game wires it to
        /// <see cref="Meditation.View.ArtLibrary.FitThought(Thought)"/>.
        /// </summary>
        public Action<Thought> ArtFitter;

        public void Clear()
        {
            _thoughts.Clear();
            _waveTimer = 0f;
            _wavesSpawned = 0;
            _spentFromBudget = 0;
            OverlapPercent = 0f;
        }

        /// <summary>Restart the wave clock without touching the thoughts already on screen.</summary>
        public void ResetWaveTimer(float seconds)
        {
            _waveTimer = seconds;
        }

        /// <param name="deltaTime">Frame time.</param>
        /// <param name="stick">Current joystick vector (needed by targeting variant C).</param>
        /// <param name="hits">Hits of the отгон registered this frame (swipes over the height sensors).</param>
        /// <param name="spawningAllowed">False during a breather / tutorial beat / finished level.</param>
        public void Tick(float deltaTime, Vector2 stick, int hits, bool spawningAllowed)
        {
            if (deltaTime > 0f)
            {
                // …and only while the level still HAS thoughts to send. A budget that has run out is
                // not a pause: the clock stops with it, so that turning the отгон on late in a level
                // cannot be punished by five waves arriving at once the moment it is switched back.
                if (spawningAllowed && BudgetLeft > 0)
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
                        t.Position = HeldInFrame(
                            t.Position + t.DriftDirection * (TuningConfig.DriftPxPerSec * deltaTime),
                            t.Size);
                }
            }

            if (hits > 0) ApplyHits(hits, stick);

            OverlapPercent = ComputeOverlapPercent();
        }

        /// <summary>
        /// Where a drifting thought is allowed to be: far enough in that its own rectangle is still on
        /// the screen, and dead centre once it has grown bigger than the screen.
        ///
        /// **This is new with the wave budget (founder, 2026-09-22) and it is the rule that makes the
        /// budget work at all.** «Дрейф к центру» was implemented as a direction and nothing else: a
        /// thought was given a unit vector at the far edge of the frame and then moved along it for
        /// ever, so it crossed the centre and sailed out the opposite side. That was invisible while a
        /// level sent waves until somebody stopped it — there was always a fresh blob behind the one
        /// leaving — and it is fatal the moment a level has five thoughts and no more: the first
        /// simulation of the new numbers had level 1 sitting at 0 % coverage after three minutes,
        /// because everything it owned had drifted off the right-hand edge.
        ///
        /// Clamped rather than stopped at the centre: five blobs piled on one point waste four of
        /// them. Held against the frame each one parks just inside the edge it came in from, and as
        /// growth takes it past the size of the screen the same clamp walks it to the middle — which
        /// is «мысли разрастаются и заполняют экран», arrived at by geometry rather than by a second
        /// rule.
        /// </summary>
        public static Vector2 HeldInFrame(Vector2 position, Vector2 size)
        {
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;

            return new Vector2(
                halfW * 2f >= ScreenWidth
                    ? ScreenWidth * 0.5f
                    : Mathf.Clamp(position.x, halfW, ScreenWidth - halfW),
                halfH * 2f >= ScreenHeight
                    ? ScreenHeight * 0.5f
                    : Mathf.Clamp(position.y, halfH, ScreenHeight - halfH));
        }

        /// <summary>Land <paramref name="hits"/> hits according to the targeting [toggle].</summary>
        public void ApplyHits(int hits, Vector2 stick)
        {
            if (hits <= 0 || _thoughts.Count == 0) return;

            for (int h = 0; h < hits; h++)
            {
                switch (TuningConfig.Targeting)
                {
                    case HitTargeting.AllOnScreen:
                        for (int i = _thoughts.Count - 1; i >= 0; i--) Damage(_thoughts[i]);
                        break;

                    case HitTargeting.StickDirection:
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
                if (IsClosed(spot)) continue;

                Thought blob = Spawn(ThoughtStrength.Strong);
                blob.Position = spot;

                // The wallpaper needs the cell CLOSED, so a defeat blob is sized to its cell rather
                // than to its class (and <see cref="Thought.SpawnSize"/> lets it: the class floor is
                // about thoughts the player fights). Its ink counts as solid for the same reason —
                // this IS the «экран целиком закрыт мыслями» picture, not a thing to measure.
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
        /// A stronger question than <see cref="IsCovered"/>, and the one the defeat wallpaper asks: is
        /// this point CLOSED — not merely inside somebody's rectangle, but hidden by enough ink that
        /// laying another blob over it would add nothing.
        ///
        /// A detail «под мыслью» is a detail you cannot pick, which is a statement about rectangles and
        /// is what <see cref="IsCovered"/> stays. A screen «целиком закрыт мыслями» is a statement about
        /// pixels, and one 36 %-hatched bottle laid across a cell does not make that cell closed — with
        /// the rectangle test the wallpaper skipped exactly the cells the real thoughts had left most
        /// see-through (Codex review, 2026-08-08).
        /// </summary>
        public bool IsClosed(Vector2 point)
        {
            float open = 1f;
            for (int i = 0; i < _thoughts.Count; i++)
            {
                if (!_thoughts[i].Rect.Contains(point)) continue;
                open *= 1f - _thoughts[i].Ink;
                if (open <= 1f - ClosedEnough) return true;
            }
            return false;
        }

        /// <summary>How much of a point has to be hidden before the wallpaper leaves it alone.</summary>
        private const float ClosedEnough = 0.9f;

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

            // The supply is spent in the wave's own order — weak first, then medium, then strong — so
            // a last, partial wave is the LIGHT end of the composition rather than an arbitrary slice
            // of it. (With the shipped budgets the division is exact and no wave is ever partial; this
            // is what happens when the founder moves the budget slider off a multiple of the wave.)
            int left = BudgetLeft;
            weak = Mathf.Min(weak, left); left -= weak;
            medium = Mathf.Min(medium, left); left -= medium;
            strong = Mathf.Min(strong, left);

            Thought last = null;
            for (int i = 0; i < weak; i++) last = Spawn(ThoughtStrength.Weak);
            for (int i = 0; i < medium; i++) last = Spawn(ThoughtStrength.Medium);
            for (int i = 0; i < strong; i++) last = Spawn(ThoughtStrength.Strong);

            _spentFromBudget += weak + medium + strong;
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
            ArtFitter?.Invoke(thought);

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
            ArtFitter?.Invoke(thought);

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

        /// <summary>
        /// Per-cell «still open» factors, kept between calls — this runs every tick and a fresh
        /// 2304-float array per frame is garbage the cabinet does not need to collect.
        /// </summary>
        private readonly float[] _open = new float[GridCols * GridRows];

        /// <summary>
        /// How much of the screen the thoughts actually HIDE — counted off the ink they paint, not off
        /// the rectangles they are drawn in (Codex review, 2026-08-08).
        ///
        /// The thoughts of the drop are marker hatching: measured over the 25 canvases they paint
        /// 15–64 % of their own rectangle (<see cref="Meditation.View.ArtLibrary.InkShareOf"/>), and
        /// counting a rectangle as closed screen made the defeat threshold a statement about how much
        /// screen the blobs were LAID OVER rather than how much of it the player can still see —
        /// «мысли заполнили экран» is about the second one. Since 2026-08-08 the fit is cover rather
        /// than contain, so those rectangles are half a screen tall on a narrow silhouette and the
        /// difference stopped being cosmetic.
        ///
        /// Layers multiply: a cell under k thoughts is open with probability Π(1 − ink), so it counts
        /// as 1 − Π(1 − ink) hidden. (The same arithmetic used to harden the HUD slots' soft alpha by
        /// stacking copies — 1 − (1 − a)^k — until the row of slots left the game in 2026-08-08.)
        /// Hatching over hatching really does close a gap, and a screen buried five deep still reads
        /// as full. A solid blob
        /// (ink 1, i.e. the greybox stand and the defeat wallpaper) closes its cell outright, so the
        /// number this returns is unchanged everywhere art is not involved.
        /// </summary>
        private float ComputeOverlapPercent()
        {
            if (_thoughts.Count == 0) return 0f;

            for (int i = 0; i < _open.Length; i++) _open[i] = 1f;

            float cellW = ScreenWidth / GridCols;
            float cellH = ScreenHeight / GridRows;

            // Walked thought by thought rather than cell by cell: a thought touches a handful of cells
            // and the field can hold hundreds of them by the time the screen closes.
            for (int i = 0; i < _thoughts.Count; i++)
            {
                Thought thought = _thoughts[i];
                float ink = thought.Ink;
                if (ink <= 0f) continue;

                Rect rect = thought.Rect;
                int firstCol = Mathf.Max(0, Mathf.CeilToInt(rect.xMin / cellW - 0.5f));
                int lastCol = Mathf.Min(GridCols - 1, Mathf.CeilToInt(rect.xMax / cellW - 0.5f) - 1);
                int firstRow = Mathf.Max(0, Mathf.CeilToInt(rect.yMin / cellH - 0.5f));
                int lastRow = Mathf.Min(GridRows - 1, Mathf.CeilToInt(rect.yMax / cellH - 0.5f) - 1);
                if (lastCol < firstCol || lastRow < firstRow) continue;

                float stillOpen = 1f - ink;
                for (int row = firstRow; row <= lastRow; row++)
                {
                    int line = row * GridCols;
                    for (int col = firstCol; col <= lastCol; col++) _open[line + col] *= stillOpen;
                }
            }

            float hidden = 0f;
            for (int i = 0; i < _open.Length; i++) hidden += 1f - _open[i];

            return hidden * 100f / (GridCols * GridRows);
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
