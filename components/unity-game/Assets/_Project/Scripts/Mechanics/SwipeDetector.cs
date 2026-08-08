using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// MECHANICS §3 — the fast line, on the cabinet's TWO HEIGHT SENSORS (founder, 2026-08-07: «мы
    /// действительно хотим отгонять на датчики движения»). A hand passing over a sensor moves its
    /// reading; one such pass is one hit, and the frequency of the passes is the frequency of the hits,
    /// exactly as the joystick shake used to work.
    ///
    /// Why a stroke and not a raw «value changed»: the sensor reports a POSITION (0..1), so a hand held
    /// above it is a constant, a hand slowly lowered is a ramp, and only a real pass is both quick and
    /// long. So a hit needs both — <see cref="TuningConfig.SwipeSharpness"/> units per second of speed
    /// AND <see cref="TuningConfig.SwipeAmplitude"/> of travel accumulated inside ONE direction — and
    /// the next hit needs the hand to turn around first. That last rule is what keeps «одно движение =
    /// один удар»: without it a long steady rise would tick off a hit every amplitude of travel, and
    /// holding the keyboard's Q would drum by itself.
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
                // Too slow to be a pass — and that also ENDS the current stroke, so the hand may come
                // back over the sensor from the same side and still be heard.
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
            if (channel.Spent || channel.Travel < Mathf.Max(0.01f, TuningConfig.SwipeAmplitude)) return speed;

            channel.Spent = true;
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
