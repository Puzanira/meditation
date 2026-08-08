using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// MECHANICS §3 — the fast line, on the cabinet's TWO HEIGHT SENSORS (founder, 2026-08-07: «мы
    /// действительно хотим отгонять на датчики движения»), rebuilt on 2026-08-08 around her own
    /// sentence about it: «вторая рука и ритмичность не нужна, хаотичные махания должны быстро
    /// отгонять мысли».
    ///
    /// What that sentence rejected was the SIZE of a hit, not the idea of one. The first version asked
    /// for a fifth of the sensor's whole range inside one stroke, which on the keyboard emulation
    /// (1.25 ед/с up, 2.0 ед/с of spring-back) is 160 ms of a held Q in one direction and 100 ms in the
    /// other: a player who jerks the key the way you jerk a hand — 60 ms here, 60 ms there — never
    /// finished a stroke and landed NOTHING. The game asked for rhythm because the threshold asked for
    /// patience, and «хаотично» is the opposite of patient.
    ///
    /// So a hit is now a SHORT quick shift of the reading: <see cref="TuningConfig.SwipeSharpness"/>
    /// units per second of speed and only <see cref="TuningConfig.SwipeAmplitude"/> of travel — a
    /// twelfth of the range instead of a fifth — with <see cref="TuningConfig.SwipeCooldownMs"/> as the
    /// floor between two hits of one sensor, so a noisy line cannot machine-gun. Measured on the
    /// package's own keyboard emulation (SwipeAndThoughtTests): one hand jerking one sensor lands
    /// **9.33 hits a second**, i.e. a level-1 weak thought (three hits) every third of a second.
    ///
    /// **What did NOT go, and why.** A movement lands ONE hit and then has to start over — turn around,
    /// or stop and set off again — and that single rule is the whole of «неподвижная рука и зажатая
    /// клавиша ударов не дают» (the invariant of this increment, founder's own «не трогать»). The
    /// sensor reports a POSITION: a hand held above it is a constant, a hand slowly lowered is a ramp,
    /// and a key held down is a ramp at exactly the speed of a real pass. Nothing in the SIGNAL tells
    /// those apart from a wave except that a wave keeps changing its mind. Drop that rule and a held Q
    /// drums by itself at twelve hits a second — which is not a stricter reading of «хаотичные
    /// махания», it is the game playing itself. What the founder asked for was frequency, and
    /// frequency is bought by the threshold: a jerked hand turns around every 60 ms, so it is charged
    /// for none of this and a resting one is charged for all of it.
    ///
    /// The two sensors are independent lines that add up: waving over both is twice the отгон, and one
    /// hand over one sensor is the game as designed. Nothing here reads a key — the emulation of the
    /// sensors from Q/A and E/D lives in the arcade-controls package (HeightSimulator), and this class
    /// only ever sees two numbers.
    /// </summary>
    public sealed class SwipeDetector
    {
        /// <summary>One sensor's own stroke bookkeeping.</summary>
        private struct Channel
        {
            public float Previous;
            public float Travel;
            public int Direction;
            public bool Spent;
            public bool Started;

            /// <summary>Seconds since this sensor's last hit — the cooldown's clock.</summary>
            public float SinceHit;
        }

        private Channel _a;
        private Channel _b;

        /// <summary>Hits registered on the last tick (0…2 — one per sensor).</summary>
        public int HitsThisTick { get; private set; }

        /// <summary>Hits registered since the last <see cref="Reset"/>.</summary>
        public int TotalHits { get; private set; }

        /// <summary>Fastest sensor movement on the last tick, sensor units per second (live readout).</summary>
        public float GestureSpeed { get; private set; }

        public void Reset()
        {
            _a = new Channel();
            _b = new Channel();
            HitsThisTick = 0;
            TotalHits = 0;
            GestureSpeed = 0f;
        }

        /// <param name="heightA">ArcadeInput.HeightA.Value, 0..1.</param>
        /// <param name="heightB">ArcadeInput.HeightB.Value, 0..1.</param>
        public void Tick(float heightA, float heightB, float deltaTime)
        {
            HitsThisTick = 0;
            if (deltaTime <= 0f) return;

            float speedA = Tick(ref _a, heightA, deltaTime, out bool hitA);
            float speedB = Tick(ref _b, heightB, deltaTime, out bool hitB);

            GestureSpeed = Mathf.Max(speedA, speedB);
            if (hitA) Register();
            if (hitB) Register();
        }

        /// <summary>One sensor, one frame. Returns its speed; <paramref name="hit"/> is its stroke landing.</summary>
        private static float Tick(ref Channel channel, float value, float deltaTime, out bool hit)
        {
            hit = false;
            channel.SinceHit += deltaTime;

            // The first frame is a reading, not a movement: whatever the sensor happens to report when
            // a level opens would otherwise look like a jump from zero and land a free hit.
            if (!channel.Started)
            {
                channel.Started = true;
                channel.Previous = value;
                return 0f;
            }

            float delta = value - channel.Previous;
            channel.Previous = value;

            float speed = Mathf.Abs(delta) / deltaTime;
            if (speed < Mathf.Max(0.01f, TuningConfig.SwipeSharpness))
            {
                // Too slow to be a pass — and that also ENDS the current movement, so a hand that
                // pauses over the sensor and sets off again the same way is heard as a new wave.
                channel.Travel = 0f;
                channel.Direction = 0;
                return speed;
            }

            int direction = delta > 0f ? 1 : -1;
            if (direction != channel.Direction)
            {
                channel.Direction = direction;
                channel.Travel = 0f;
                channel.Spent = false;
            }

            channel.Travel += Mathf.Abs(delta);
            if (channel.Spent || channel.Travel < Mathf.Max(0.005f, TuningConfig.SwipeAmplitude)) return speed;

            // The rate floor. It is not what stops a held key (that is the line above — one movement,
            // one hit); it is what stops a jittering ANALOG line, where every frame can be its own
            // turn-around, from reading as a drum roll the player never played.
            if (channel.SinceHit < Mathf.Max(0f, TuningConfig.SwipeCooldownMs) * 0.001f) return speed;

            channel.Spent = true;
            channel.SinceHit = 0f;
            hit = true;
            return speed;
        }

        private void Register()
        {
            HitsThisTick++;
            TotalHits++;
        }
    }
}
