using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// The game's teaching hint since the drop of 2026-08-07: a DRAWN button from
    /// <c>арт/кнопки/</c> standing beside the thing it teaches, with an arrow to it.
    ///
    /// Not <see cref="HintCard"/> with a sprite bolted on. The card was a white plate whose width was
    /// computed from the string on it, whose border took the colour of the hand, and whose 44 pt line
    /// came verbatim from the text registry — every one of those decisions is now the designer's, baked
    /// into a PNG, and «поверх ничего не писать» is the rule for this drop. What is left of the card is
    /// where it stands and what it points at, and that is all this class is.
    ///
    /// One beat has no button at all: «отгони мысль» — the drop ships НАВОДИ, КРУТИ РУЧКУ and ТАЩИ, and
    /// the fourth («ТРЯСИ») is still with the designer. Until it arrives that beat is
    /// <see cref="ShowArrowOnly"/>: an arrow at the joystick with no words, wobbling the way the hand
    /// is meant to (SCREENS §Обучение п.3, walkthrough Э6).
    /// </summary>
    public sealed class ButtonHint
    {
        /// <summary>
        /// Height every hint button is drawn at, design px.
        ///
        /// The drop's buttons are «в 2–3× игрового размера» and all four are 264 px tall (the title's
        /// НАЧАТЬ is 176), so one height and each button's own aspect is enough — 88 px is a third of
        /// 264, which is the scale the title's button was measured at on <c>Title/превью.png</c>.
        /// Deriving the width from the sprite rather than fixing it is what keeps КРУТИ РУЧКУ (5.3:1)
        /// and ТАЩИ (2.9:1) from being stretched to the same box.
        /// </summary>
        public const float ButtonHeight = 88f;

        private readonly Image _button;
        private readonly HintArrow _arrow;

        private Vector2 _centre;
        private Vector2 _size;
        private float _standoff;
        private Color _ink;

        public ButtonHint(Transform parent, string name = "ButtonHint")
        {
            _button = Ui.NewImage(parent, name);
            _button.preserveAspect = true;
            _button.raycastTarget = false;
            _arrow = new HintArrow(parent, name + "Arrow");
            Hide();
        }

        public RectTransform Rect => _button.rectTransform;

        /// <summary>The drawn button — the suite asserts WHICH picture is up, since there is no text.</summary>
        public Image Button => _button;

        /// <summary>The stroke itself — the отгон beat is nothing else, so it has to be measurable.</summary>
        public HintArrow Arrow => _arrow;

        public bool IsShown => _button.gameObject.activeSelf || _arrow.IsShown;

        /// <summary>Design-space rectangle of the button, for the overlap checks that place it.</summary>
        public Rect ButtonRect => RectFor(_button.sprite, _centre);

        /// <summary>The rectangle this sprite would occupy at <paramref name="centre"/>, design px.</summary>
        public static Rect RectFor(Sprite sprite, Vector2 centre)
        {
            Vector2 size = SizeOf(sprite);
            return new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);
        }

        /// <summary>A button's drawn size: the common height, its own aspect.</summary>
        public static Vector2 SizeOf(Sprite sprite)
        {
            if (sprite == null || sprite.rect.height < 1f) return new Vector2(320f, ButtonHeight);
            return new Vector2(ButtonHeight * sprite.rect.width / sprite.rect.height, ButtonHeight);
        }

        public void Hide()
        {
            _button.gameObject.SetActive(false);
            _arrow.Hide();
        }

        /// <param name="sprite">A button of <c>арт/кнопки/</c> — the text is already in the picture.</param>
        /// <param name="centre">Where the button stands, design px — beside the target, never on it.</param>
        /// <param name="target">What the arrow points at, design px.</param>
        /// <param name="standoff">How far short of the target the arrowhead stops (mock 4: ~50 px).</param>
        public void Show(Sprite sprite, HintTone tone, Vector2 centre, Vector2 target,
            float standoff = 50f)
        {
            _ink = HintCard.ToneColour(tone);
            _centre = centre;
            _size = SizeOf(sprite);
            _standoff = standoff;

            _button.gameObject.SetActive(true);
            _button.sprite = sprite;
            // A missing button must not be an invisible hint: it falls back to a flat plate in the
            // hand's own colour, which is wrong-looking enough to be noticed and still teaches WHERE.
            _button.color = sprite != null ? Color.white : _ink;
            Ui.Place(_button.rectTransform, centre.x, centre.y, _size.x, _size.y);

            PointAt(target);
        }

        /// <summary>
        /// The beat with no button: an arrow and nothing else, from <paramref name="from"/> towards the
        /// joystick. Used for «отгони мысль» until the designer sends «ТРЯСИ».
        /// </summary>
        public void ShowArrowOnly(HintTone tone, Vector2 from, Vector2 target)
        {
            _ink = HintCard.ToneColour(tone);
            _centre = from;
            _size = Vector2.zero;
            _standoff = 0f;

            _button.gameObject.SetActive(false);
            _arrow.Draw(from, target, _ink);
        }

        /// <summary>
        /// Walk the button to a new place without changing which picture it is.
        ///
        /// «ТАЩИ» needs this and the others do not: it is the only hint whose subject MOVES. Placed once
        /// beside the detail's starting position it was left behind within a second — the detail drove
        /// out from under its own label, and the plate stayed sitting across the progress ring, which is
        /// the only feedback the handle has (design gate, 2026-08-07). SCREENS §Обучение п.2 says the
        /// hint stands «рядом с ЕДУЩЕЙ деталью».
        /// </summary>
        public void MoveTo(Vector2 centre)
        {
            if (!_button.gameObject.activeSelf) return;
            _centre = centre;
            Ui.Place(_button.rectTransform, centre.x, centre.y, _size.x, _size.y);
        }

        /// <summary>
        /// Re-aim without moving the button. The thing being taught MOVES on the «тащи» beat — the
        /// detail travels to the vessel and flies home when it slips — and an arrow pointing at an
        /// empty road is what the design gate caught the first time round.
        /// </summary>
        public void PointAt(Vector2 target)
        {
            if (!_button.gameObject.activeSelf) return;

            Vector2 from = EdgePointTowards(_centre, _size, target);
            Vector2 approach = target - from;
            float travel = Mathf.Max(20f, approach.magnitude - Mathf.Max(0f, _standoff));
            _arrow.Draw(from, from + approach.normalized * travel, _ink);
        }

        /// <summary>Start the arrow on the button's border, not in the middle of the drawn word.</summary>
        private static Vector2 EdgePointTowards(Vector2 centre, Vector2 size, Vector2 target)
        {
            Vector2 direction = target - centre;
            if (direction.sqrMagnitude < 1f) return centre;

            float halfW = Mathf.Max(1f, size.x * 0.5f);
            float halfH = Mathf.Max(1f, size.y * 0.5f);
            float scaleX = Mathf.Abs(direction.x) > 0.001f ? halfW / Mathf.Abs(direction.x) : float.MaxValue;
            float scaleY = Mathf.Abs(direction.y) > 0.001f ? halfH / Mathf.Abs(direction.y) : float.MaxValue;
            return centre + direction * Mathf.Min(scaleX, scaleY);
        }
    }
}
