using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// MECHANICS §3 — the fast line. A "hit" is a sharp stick movement past the amplitude threshold:
    /// either a fresh deflection out of the dead zone, or a reversal of the current direction.
    /// Direction does not matter, frequency does.
    ///
    /// The gesture-speed gate is what separates shaking from the gaze tilt (SCREENS, "разводка жестов
    /// одного стика"): a slow, smooth tilt moves the gaze and lands no hits.
    /// </summary>
    public sealed class ShakeDetector
    {
        private Vector2 _lastDirection;
        private Vector2 _previousStick;
        private bool _armed;

        /// <summary>Hits registered on the last tick (0 or 1).</summary>
        public int HitsThisTick { get; private set; }

        /// <summary>Hits registered since the last <see cref="Reset"/>.</summary>
        public int TotalHits { get; private set; }

        /// <summary>Gesture speed measured on the last tick, stick units per second (live readout).</summary>
        public float GestureSpeed { get; private set; }

        public void Reset()
        {
            _lastDirection = Vector2.zero;
            _previousStick = Vector2.zero;
            _armed = false;
            HitsThisTick = 0;
            TotalHits = 0;
            GestureSpeed = 0f;
        }

        public void Tick(Vector2 stick, float deltaTime)
        {
            HitsThisTick = 0;
            if (deltaTime <= 0f) return;

            GestureSpeed = (stick - _previousStick).magnitude / deltaTime;
            _previousStick = stick;

            float amplitude = stick.magnitude;
            float threshold = Mathf.Max(0.05f, TuningConfig.ShakeAmplitude);

            // Hysteresis: the stick has to come back towards the centre before a fresh deflection counts.
            if (amplitude < threshold * 0.5f)
            {
                _armed = false;
                _lastDirection = Vector2.zero;
                return;
            }

            if (amplitude < threshold) return;
            if (GestureSpeed < TuningConfig.ShakeGestureSpeed) return;

            Vector2 direction = stick / amplitude;
            if (!_armed)
            {
                _armed = true;
                _lastDirection = direction;
                Register();
                return;
            }

            if (Vector2.Dot(direction, _lastDirection) < 0f)
            {
                _lastDirection = direction;
                Register();
            }
        }

        private void Register()
        {
            HitsThisTick = 1;
            TotalHits++;
        }
    }
}
