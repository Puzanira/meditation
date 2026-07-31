using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>Which hand a hint is about — the mock's colour language (walkthrough frames 4, 7, 9).</summary>
    public enum HintTone
    {
        /// <summary>Динамо / сбор — зелёный #1a6e46.</summary>
        Crank = 0,
        /// <summary>Тряска — красный #8f2f2c.</summary>
        Shake = 1,
        /// <summary>Взгляд — синий #39587a.</summary>
        Gaze = 2
    }

    /// <summary>
    /// The teaching card of the mock: a white rounded card (rx=18) with a 4 px coloured border and
    /// 44 pt text, standing NEXT TO the thing it talks about, with a curved arrow pointing at it.
    /// Colour says which hand: green = crank, red = shake, blue = gaze.
    ///
    /// The stand had a grey centred line instead; the colour pairing is a through-line of the mock,
    /// so it is rebuilt here rather than approximated.
    /// </summary>
    public sealed class HintCard
    {
        private const int ArrowSegments = 7;

        private static readonly Color Paper = new Color(1f, 1f, 1f, 0.97f);

        private readonly Image _card;
        private readonly Text _label;
        private readonly RectTransform _arrow;
        private readonly Image[] _segments = new Image[ArrowSegments];
        private readonly Image[] _head = new Image[2];

        public HintCard(Transform parent)
        {
            _card = Ui.Rounded(parent, "HintCard", 960f, 540f, 480f, 110f, Paper, Color.black, 4f, 18);
            _label = Ui.Label(_card.transform, "HintText", "", 0f, 0f, 460f, 100f, 44, Color.black);
            RectTransform labelRt = _label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(16f, 8f);
            labelRt.offsetMax = new Vector2(-16f, -8f);

            var arrowGo = new GameObject("HintArrow", typeof(RectTransform));
            arrowGo.transform.SetParent(parent, false);
            _arrow = (RectTransform)arrowGo.transform;
            _arrow.anchorMin = Vector2.zero;
            _arrow.anchorMax = Vector2.one;
            _arrow.offsetMin = Vector2.zero;
            _arrow.offsetMax = Vector2.zero;

            for (int i = 0; i < ArrowSegments; i++)
                _segments[i] = Ui.Segment(_arrow, "ArrowSeg" + i, Color.black, 6f);
            for (int i = 0; i < 2; i++)
                _head[i] = Ui.Segment(_arrow, "ArrowHead" + i, Color.black, 6f);

            Hide();
        }

        public RectTransform Rect => _card.rectTransform;

        public Text Label => _label;

        public bool IsShown => _card.gameObject.activeSelf;

        /// <summary>Design-space rect of the card, for overlap checks in tests.</summary>
        public Rect CardRect
        {
            get
            {
                Vector2 size = _card.rectTransform.sizeDelta;
                Vector2 pos = _card.rectTransform.anchoredPosition;
                return new Rect(pos.x - size.x * 0.5f, -pos.y - size.y * 0.5f, size.x, size.y);
            }
        }

        public void Hide()
        {
            _card.gameObject.SetActive(false);
            _arrow.gameObject.SetActive(false);
        }

        /// <param name="text">Exact string from the walkthrough's text registry.</param>
        /// <param name="cardCentre">Card centre in design px — next to the target, never over it.</param>
        /// <param name="target">What the arrow points at, design px.</param>
        /// <param name="standoff">
        /// How far short of the target's centre the arrowhead stops. The mock's arrow ends on the
        /// approach (frame 4: ~50 px away) — it must never cross the object or its caption.
        /// </param>
        public void Show(string text, HintTone tone, Vector2 cardCentre, Vector2 target,
            float standoff = 50f)
        {
            Color ink = ToneColour(tone);

            _card.gameObject.SetActive(true);
            _arrow.gameObject.SetActive(true);

            float width = Mathf.Clamp(text.Length * 24f + 60f, 320f, 760f);
            _card.rectTransform.sizeDelta = new Vector2(width, 110f);
            Ui.MoveTo(_card.rectTransform, cardCentre);
            _card.color = ink;
            Image fill = _card.transform.GetChild(0).GetComponent<Image>();
            if (fill != null) fill.color = Paper;

            _label.text = text;
            _label.color = ink;

            Vector2 from = EdgePointTowards(cardCentre, width, target);
            Vector2 approach = target - from;
            float travel = Mathf.Max(20f, approach.magnitude - Mathf.Max(0f, standoff));
            Vector2 to = from + approach.normalized * travel;
            DrawArrow(from, to, ink);
        }

        /// <summary>Start the arrow on the card's border, not in the middle of the text.</summary>
        private static Vector2 EdgePointTowards(Vector2 centre, float width, Vector2 target)
        {
            Vector2 direction = target - centre;
            if (direction.sqrMagnitude < 1f) return centre;

            float halfW = width * 0.5f;
            const float halfH = 55f;
            float scaleX = Mathf.Abs(direction.x) > 0.001f ? halfW / Mathf.Abs(direction.x) : float.MaxValue;
            float scaleY = Mathf.Abs(direction.y) > 0.001f ? halfH / Mathf.Abs(direction.y) : float.MaxValue;
            return centre + direction * Mathf.Min(scaleX, scaleY);
        }

        /// <summary>A quadratic bend, drawn as short straight segments — the mock's curved arrow.</summary>
        private void DrawArrow(Vector2 from, Vector2 to, Color colour)
        {
            Vector2 straight = to - from;
            Vector2 perpendicular = new Vector2(-straight.y, straight.x).normalized;
            Vector2 control = (from + to) * 0.5f + perpendicular * (straight.magnitude * 0.18f);

            Vector2 previous = from;
            Vector2 last = from;
            for (int i = 0; i < ArrowSegments; i++)
            {
                float t = (i + 1f) / ArrowSegments;
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
                Vector2 barb = new Vector2(
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

        public static Color ToneColour(HintTone tone)
        {
            switch (tone)
            {
                case HintTone.Crank: return Mechanics.LevelOneData.Hex("1a6e46");
                case HintTone.Shake: return Mechanics.LevelOneData.Hex("8f2f2c");
                default: return Mechanics.LevelOneData.Hex("39587a");
            }
        }
    }
}
