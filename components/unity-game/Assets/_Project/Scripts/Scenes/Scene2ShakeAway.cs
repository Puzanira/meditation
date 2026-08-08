using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;

namespace Meditation.Scenes
{
    /// <summary>
    /// Scenette 2 «Отгон взмахами» (MECHANICS §7.2): thoughts only, no collecting. Waves roll in, each
    /// blob carries its pips, and swipes over the height sensors knock them out by the toughness of
    /// their type — walkthrough frames 7–8, without the collecting hand.
    ///
    /// The scenette moved to the sensors with the game (founder, 2026-08-07) and by the same code: the
    /// stand judges RULES, and a stand whose отгон lives on a controller the game no longer uses would
    /// be teaching a rule the game does not have. The scene FILE keeps its name — a rename would touch
    /// Build Settings and every GUID for nothing the founder can see.
    /// </summary>
    [AddComponentMenu("Meditation/Scene 2 — Shake Away")]
    public sealed class Scene2ShakeAway : PreviewSceneController
    {
        /// <summary>Card position of walkthrough frame 7 (card rect 1030,380 · 480×110).</summary>
        private static readonly Vector2 HintCentre = new Vector2(1270f, 435f);

        /// <summary>Opening staging, straight off frames 7/15/16 of the mock.</summary>
        private static readonly Vector2 DishesAt = new Vector2(640f, 470f);
        private static readonly Vector2 BillAt = new Vector2(1470f, 820f);
        private static readonly Vector2 QuestionsAt = new Vector2(760f, 170f);

        /// <summary>Half of an M blob (280×220) plus a margin — the arrow stops at its edge.</summary>
        private const float BlobStandoff = 160f;

        private readonly ThoughtField _field = new ThoughtField(4242);
        private int _popped;
        private Thought _hintTarget;

        public ThoughtField Field => _field;

        public int Popped => _popped;

        protected override string Title => "Сценка 2 · Отгон взмахами";

        /// <summary>
        /// What the stand's card says now. «Тряси джойстик!» is a WITHDRAWN line (<see cref="Game.GameTexts"/>)
        /// and, since 2026-08-07, also a false one: the отгон is on the height sensors. The registry
        /// governs what the GAME renders — it renders no strings at all — and the stand is a dev
        /// instrument whose one job is to say which controller a rule belongs to.
        /// </summary>
        private const string HintLine = "Маши над датчиком!";

        protected override StageOptions Options => new StageOptions
        {
            ShowDetails = false, ShowThoughts = true
        };

        protected override IList<TuningParam> Parameters() => TuningCatalog.ShakeAway();

        protected override void OnBuilt()
        {
            _field.Popped += OnPopped;

            // All three toughness classes on screen from the first second, in the mock's own places
            // and sizes: гора посуды M (4 pips), счёт ЖКХ L, клубок ? S. The label cycle assigns the
            // names in this exact order, which is why the strengths are spawned in it.
            Thought dishes = _field.Spawn(ThoughtStrength.Medium);
            dishes.Position = DishesAt;
            Thought bill = _field.Spawn(ThoughtStrength.Strong);
            bill.Position = BillAt;
            Thought questions = _field.Spawn(ThoughtStrength.Weak);
            questions.Position = QuestionsAt;

            _hintTarget = dishes;
            _field.ResetWaveTimer(TuningConfig.WaveIntervalSeconds);

            // Стрелка останавливается у габарита блоба, а не на его подписи (макет 7).
            Composition.ShowHint(HintLine, HintTone.Swipe, HintCentre, dishes.Position, BlobStandoff);
        }

        protected override void Tick(float deltaTime)
        {
            _field.Tick(deltaTime, Stick, Hits, true);
            Composition.SyncThoughts(_field.Thoughts, deltaTime);
            Composition.SetPeak(_field.OverlapPercent >= TuningConfig.LossOverlapPercent);

            // The card teaches until its thought is gone — then the screen is the founder's again.
            if (_hintTarget != null)
                Composition.ShowHint(HintLine, HintTone.Swipe, HintCentre,
                    _hintTarget.Position, BlobStandoff);
        }

        private void OnPopped(Thought thought)
        {
            _popped++;
            if (thought != _hintTarget) return;
            _hintTarget = null;
            Composition.HideHint();
        }

        protected override string Readout()
        {
            string targeting;
            switch (TuningConfig.Targeting)
            {
                case HitTargeting.AllOnScreen: targeting = "A: все"; break;
                case HitTargeting.StickDirection: targeting = "C: по прицелу"; break;
                default: targeting = "B: ближняя"; break;
            }

            return HandsReadout() +
                   "мыслей на экране: " + _field.Thoughts.Count + "\n" +
                   "отбито: " + _popped + "\n" +
                   "перекрытие: " + _field.OverlapPercent.ToString("0") + " %\n" +
                   "волна через: " + _field.SecondsToNextWave.ToString("0.0") + " с\n" +
                   "таргетинг: " + targeting;
        }
    }
}
