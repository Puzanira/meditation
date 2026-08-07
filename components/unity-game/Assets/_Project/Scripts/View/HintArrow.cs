using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// The curved arrow the teaching hints point with: a quadratic bend drawn as short straight
    /// segments, with two barbs at the far end.
    ///
    /// Its own class because there are now two things that point: the greybox stand's white
    /// <see cref="HintCard"/> (which still writes the mock's own words, because the stand is where the
    /// mechanics are judged without a picture) and the game's <see cref="ButtonHint"/>, whose «card» is
    /// a drawn button from the art drop. The arrow is the part that is identical, and the beat that
    /// has no button at all — «отгони мысль», for which the drop ships no «ТРЯСИ» — is nothing BUT an
    /// arrow, so it had to be usable on its own.
    /// </summary>
    public sealed class HintArrow
    {
        private const int Segments = 7;

        private readonly RectTransform _root;
        private readonly Image[] _segments = new Image[Segments];
        private readonly Image[] _head = new Image[2];

        public HintArrow(Transform parent, string name = "HintArrow")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = (RectTransform)go.transform;
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            for (int i = 0; i < Segments; i++)
                _segments[i] = Ui.Segment(_root, name + "Seg" + i, Color.black, 6f);
            for (int i = 0; i < 2; i++)
                _head[i] = Ui.Segment(_root, name + "Head" + i, Color.black, 6f);

            Hide();
        }

        public RectTransform Rect => _root;

        /// <summary>
        /// The stroke last drawn, design px. The root is stretched over the whole canvas (the segments
        /// carry the geometry), so «how long is this arrow and where does it start» cannot be read off a
        /// rect transform — and those are exactly the two questions the design gate asked of the shake
        /// beat, whose arrow ran 1047 px from the middle of a drawn thought to the bottom of the frame.
        /// </summary>
        public Vector2 From { get; private set; }

        /// <summary>…and where its head is.</summary>
        public Vector2 To { get; private set; }

        /// <summary>One drawn segment, so a test can prove the stroke has pixels and not only a state.</summary>
        public RectTransform FirstSegment => _segments[0].rectTransform;

        public bool IsShown => _root.gameObject.activeSelf;

        public void SetActive(bool on) => _root.gameObject.SetActive(on);

        public void Hide() => _root.gameObject.SetActive(false);

        /// <summary>Draw the arrow from <paramref name="from"/> to <paramref name="to"/>, design px.</summary>
        public void Draw(Vector2 from, Vector2 to, Color colour)
        {
            _root.gameObject.SetActive(true);
            From = from;
            To = to;

            Vector2 straight = to - from;
            Vector2 perpendicular = new Vector2(-straight.y, straight.x).normalized;
            Vector2 control = (from + to) * 0.5f + perpendicular * (straight.magnitude * 0.18f);

            Vector2 previous = from;
            Vector2 last = from;
            for (int i = 0; i < Segments; i++)
            {
                float t = (i + 1f) / Segments;
                Vector2 point = Bezier(from, control, to, t);
                _segments[i].color = colour;
                Ui.StretchLine(_segments[i].rectTransform, previous, point);
                last = previous;
                previous = point;
            }

            Vector2 incoming = (to - last).normalized;
            for (int i = 0; i < 2; i++)
            {
                float angle = (i == 0 ? 150f : -150f) * Mathf.Deg2Rad;
                var barb = new Vector2(
                    incoming.x * Mathf.Cos(angle) - incoming.y * Mathf.Sin(angle),
                    incoming.x * Mathf.Sin(angle) + incoming.y * Mathf.Cos(angle));
                _head[i].color = colour;
                Ui.StretchLine(_head[i].rectTransform, to, to + barb * 26f);
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float inv = 1f - t;
            return inv * inv * a + 2f * inv * t * control + t * t * b;
        }
    }
}
