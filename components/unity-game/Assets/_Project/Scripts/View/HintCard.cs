using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>Which hand a hint is about — the mock's colour language (walkthrough frames 4, 7, 9).</summary>
    public enum HintTone
    {
        /// <summary>Динамо / сбор — зелёный #1a6e46.</summary>
        Crank = 0,
        /// <summary>
        /// Отгон (взмах над датчиком) — бирюза дропа #8fd7d6.
        ///
        /// Was the mock's brick red #8f2f2c, and the mock was greybox: a white card with a word on it,
        /// standing on flat rectangles. This beat is now the ONLY hint with no button — the drop ships
        /// no «ТРЯСИ» — so its arrow is all the player sees of it, and a red stroke belongs to nothing
        /// in the frame. The drop's own three hint buttons are painted in exactly this turquoise
        /// (measured off навoди / крути ручку / тащи: 45 756 px of #8fd7d6, their single dominant
        /// colour), so the arrow reads as the fourth button's line rather than as an alarm.
        /// </summary>
        Swipe = 1,
        /// <summary>Взгляд — синий #39587a.</summary>
        Gaze = 2
    }

    /// <summary>
    /// The teaching card of the mock: a white rounded card (rx=18) with a 4 px coloured border and
    /// 44 pt text, standing NEXT TO the thing it talks about, with a curved arrow pointing at it.
    /// Colour says which controller: green = crank, turquoise = the sensors' отгон, blue = the aim.
    ///
    /// The stand had a grey centred line instead; the colour pairing is a through-line of the mock,
    /// so it is rebuilt here rather than approximated.
    /// </summary>
    public sealed class HintCard
    {
        private static readonly Color Paper = new Color(1f, 1f, 1f, 0.97f);

        private readonly Image _card;
        private readonly Text _label;
        private readonly HintArrow _arrow;

        private Vector2 _centre;
        private float _width;
        private float _standoff;
        private Color _ink;

        public HintCard(Transform parent)
        {
            _card = Ui.Rounded(parent, "HintCard", 960f, 540f, 480f, 110f, Paper, Color.black, 4f, 18);
            _label = Ui.Label(_card.transform, "HintText", "", 0f, 0f, 460f, 100f, 44, Color.black);
            RectTransform labelRt = _label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(16f, 8f);
            labelRt.offsetMax = new Vector2(-16f, -8f);

            _arrow = new HintArrow(parent);
            Hide();
        }

        public RectTransform Rect => _card.rectTransform;

        public Text Label => _label;

        public bool IsShown => _card.gameObject.activeSelf;

        /// <summary>The card is one line tall, whatever the string (mock 4/7/9).</summary>
        public const float CardHeight = 110f;

        /// <summary>How wide this string makes the card — known before it is shown, so it can be placed.</summary>
        public static float WidthFor(string text) =>
            Mathf.Clamp((text == null ? 0 : text.Length) * 24f + 60f, 320f, 760f);

        /// <summary>The design-space rectangle this string would occupy at <paramref name="centre"/>.</summary>
        public static Rect RectFor(string text, Vector2 centre)
        {
            float width = WidthFor(text);
            return new Rect(centre.x - width * 0.5f, centre.y - CardHeight * 0.5f, width, CardHeight);
        }

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
            _arrow.Hide();
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

            float width = WidthFor(text);
            _card.rectTransform.sizeDelta = new Vector2(width, CardHeight);
            Ui.MoveTo(_card.rectTransform, cardCentre);
            _card.color = ink;
            Image fill = _card.transform.GetChild(0).GetComponent<Image>();
            if (fill != null) fill.color = Paper;

            _label.text = text;
            _label.color = ink;

            _centre = cardCentre;
            _width = width;
            _standoff = standoff;
            _ink = ink;

            PointAt(target);
        }

        /// <summary>
        /// Re-aim the arrow without moving the card.
        ///
        /// The teaching arrow points at a THING, and on beat 1 that thing moves: a detail that slips off
        /// the thread flies back home across the frame, and the frame the gate looked at had the arrow
        /// pointing into an empty road while the dandelion was 280 px away. The card stays where it was
        /// placed — it is the arrow that follows.
        /// </summary>
        public void PointAt(Vector2 target)
        {
            if (!IsShown) return;

            Vector2 from = EdgePointTowards(_centre, _width, target);
            Vector2 approach = target - from;
            float travel = Mathf.Max(20f, approach.magnitude - Mathf.Max(0f, _standoff));
            _arrow.Draw(from, from + approach.normalized * travel, _ink);
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

        public static Color ToneColour(HintTone tone)
        {
            switch (tone)
            {
                case HintTone.Crank: return Mechanics.LevelOneData.Hex("1a6e46");
                case HintTone.Swipe: return Mechanics.LevelOneData.Hex("8fd7d6");
                default: return Mechanics.LevelOneData.Hex("39587a");
            }
        }
    }
}
