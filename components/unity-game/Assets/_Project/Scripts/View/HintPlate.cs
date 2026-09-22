using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// A teaching SENTENCE, drawn in the drop's own hint style.
    ///
    /// It began (2026-08-19) as the words for the one beat the drop has no picture for: the отгон moved
    /// to the height sensors, «ТРЯСИ» never arrived and would be the wrong verb anyway, and the founder
    /// playing the build could not tell what the arrow at the bottom edge was asking her to do. That was
    /// blocker Б1 of the design gate on 2026-08-08 — the beat was drawn with <see cref="HintCard"/>, the
    /// greybox STAND's white paper (luminance 253.6), while the three beats around it carried the drop's
    /// dark slab. Four hints of one lesson have to look like four hints of one lesson.
    ///
    /// Since the founder's playtest of 2026-09-22 it carries ALL FOUR of them. She dictated a sentence
    /// per beat — «Наводи джойстиком на объект», «Замечай детали вокруг…» — because the drawn buttons
    /// name the HAND in one word and never say what the game is asking for. So the class had to grow two
    /// things a one-word label does not need:
    ///
    ///   · **wrapping.** A 63-character sentence is not a label, and <see cref="MaxWidth"/> is what
    ///     keeps a teaching plate a thing that can be PLACED — a single-line plate for that sentence
    ///     would be wider than the frame has clear space anywhere on it.
    ///   · **sentence case, and no разрядка.** The tracking (<see cref="Set"/>) is the drop's own
    ///     setting for НАВОДИ / КРУТИ РУЧКУ / ТАЩИ and it is still what those are drawn in — but caps
    ///     with letter-spacing over a whole sentence shouts, and the founder's brief for these lines is
    ///     «в тоне медитации». The слab, the white ink and the 2 px rule — the three things Б1 was
    ///     actually about — are unchanged.
    ///
    /// …plus an optional second line underneath, smaller and dimmer. It carried the PC bracket until
    /// 2026-09-22, when every keyboard hint left the game («она играется на автомате» — founder); the
    /// plate kept the form, because a quiet second line under an instruction is a typographic device
    /// and the day a screen needs one again it should not be re-invented. Nothing passes one now, and
    /// the plate is one line tall when nothing does.
    ///
    /// <see cref="HintCard"/> stays exactly as it is and stays in use — on the stand, whose whole
    /// composition is greybox and where a white paper card is the right paper.
    /// </summary>
    public sealed class HintPlate
    {
        /// <summary>
        /// The dark slab of the drop's hint buttons, RGB(35, 52, 65) — measured off НАВОДИ / КРУТИ
        /// РУЧКУ / ТАЩИ by the design skeptic, 2026-08-08.
        /// </summary>
        /// <remarks>
        /// OPAQUE since the skeptic's return of 2026-09-22. It shipped at α 0.93 on the argument that
        /// a flat rectangle on a photographic plate reads as a sticker — and the four teaching plates
        /// then read as four different plates, because each one stands on a different picture: the
        /// отгон's lay on the bright sky of the набережная and came out visibly lighter than the three
        /// beside it. Seven per cent of the plate underneath is not «less of a sticker», it is the
        /// plate's own colour being decided by whatever happens to be behind it. Same fix the fill bar
        /// had on 2026-08-08, and for the same reason: a widget that has to be one thing in four places
        /// cannot be translucent.
        /// </remarks>
        public static readonly Color Slab = new Color(35f / 255f, 52f / 255f, 65f / 255f, 1f);

        /// <summary>
        /// A one-line plate, design px — the height the drop's drawn buttons were placed at.
        ///
        /// It was <c>ButtonHint.ButtonHeight</c> until 2026-09-22, so that the beat's words and the
        /// beat's button would be the same height. The buttons are off the teaching screens now and
        /// the class with them; the NUMBER stays, because it is the designer's — the drop's buttons
        /// are all 264 px tall and are drawn at a third of that, which is the scale the title's НАЧАТЬ
        /// was measured at on <c>Title/превью.png</c>. A plate whose height came from the drawn set is
        /// a plate that still belongs to this drop.
        /// </summary>
        public const float PlateHeight = 88f;

        /// <summary>The rule around it, design px — the drop's buttons are outlined at 2, not at 4.</summary>
        public const float BorderWidth = 2f;

        /// <summary>The sentence, at a size that survives being read across a room.</summary>
        public const int FontSize = 30;

        /// <summary>…and the PC bracket under it, at the ratio the title sets (40 pt against 26).</summary>
        public const int BracketFontSize = 21;

        /// <summary>
        /// How wide a plate is allowed to get, design px.
        ///
        /// Not a typographic number — a PLACEMENT one. <see cref="HintPlacement.Beside"/> has to find a
        /// rectangle this size that covers no detail, no vessel and no HUD widget, on a frame that is
        /// 1920 wide and already full of drawn art; every 100 px of plate is a place it can no longer
        /// stand. 560 fits the longest of the founder's four sentences onto three lines.
        /// </summary>
        public const float MaxWidth = 560f;

        /// <summary>Height of one line of the sentence, design px.</summary>
        public const float LineHeight = 42f;

        /// <summary>…and of the bracket's own line, when there is one.</summary>
        public const float BracketHeight = 30f;

        /// <summary>
        /// The tracking, as a character — see <see cref="Set"/>. UGUI's <see cref="Text"/> has no
        /// letter-spacing, so it has to be IN the string («р а з р я д к а», which is how Russian
        /// typography has always written it).
        /// </summary>
        private const char Tracking = ' ';

        /// <summary>
        /// Mean advance of a Cyrillic lower-case glyph at <see cref="FontSize"/>, design px.
        ///
        /// Measured against the set rather than guessed: 14.5 was an eyeball and it under-counted by a
        /// whole line — the отгон's 63-character sentence was given a two-line plate and UGUI laid it
        /// out in three (101 px of text in an 81 px box). The estimate has to ERR HIGH, because the
        /// number it feeds is the rectangle the placement search is told to find room for, and a plate
        /// that turns out taller than the rectangle that was cleared for it is a plate standing on
        /// something.
        /// </summary>
        private const float Advance = 19f;

        /// <summary>…and of a capital with <see cref="Set"/>'s tracking, for the label form.</summary>
        private const float TrackedAdvance = 33f;

        private const float PadX = 30f;
        private const float PadY = 20f;

        private readonly Image _plate;
        private readonly Image _fill;
        private readonly Text _label;
        private readonly Text _bracket;

        private Vector2 _centre;

        public HintPlate(Transform parent, string name = "HintPlate")
        {
            _plate = Ui.BoxCentred(parent, name, 960f, 540f, MaxWidth, PlateHeight, Color.white);

            _fill = Ui.NewImage(_plate.transform, name + "_Fill");
            _fill.color = Slab;
            RectTransform fill = _fill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0.5f, 0.5f);
            fill.offsetMin = new Vector2(BorderWidth, BorderWidth);
            fill.offsetMax = new Vector2(-BorderWidth, -BorderWidth);

            _label = Ui.Label(_plate.transform, name + "Text", "", 0f, 0f, MaxWidth - PadX * 2f,
                PlateHeight, FontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            _bracket = Ui.Label(_plate.transform, name + "Bracket", "", 0f, 0f, MaxWidth - PadX * 2f,
                BracketHeight, BracketFontSize, BracketInk, TextAnchor.MiddleCenter);
            _bracket.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bracket.verticalOverflow = VerticalWrapMode.Overflow;

            Hide();
        }

        /// <summary>The bracket's ink — the plate's white, held back so it reads as a footnote.</summary>
        public static readonly Color BracketInk = new Color(1f, 1f, 1f, 0.62f);

        public RectTransform Rect => _plate.rectTransform;

        /// <summary>The lettering — the suite reads WHICH line is up, and how it is set.</summary>
        public Text Label => _label;

        /// <summary>The PC bracket under it; its object is off when the line has none.</summary>
        public Text Bracket => _bracket;

        /// <summary>The slab itself, without the rule — what the pixel guard of Б1 measures.</summary>
        public Image Fill => _fill;

        /// <summary>The 2 px rule around it, carrying the colour of the hand this hint is about.</summary>
        public Image Plate => _plate;

        public bool IsShown => _plate.gameObject.activeSelf;

        // ---- how big a line makes the plate -----------------------------------------------------

        /// <summary>How many lines this sentence wraps to inside <see cref="MaxWidth"/>.</summary>
        public static int LinesFor(string text)
        {
            int n = text == null ? 0 : text.Length;
            if (n == 0) return 1;
            float inner = MaxWidth - PadX * 2f;
            return Mathf.Clamp(Mathf.CeilToInt(n * Advance / inner), 1, 4);
        }

        /// <summary>
        /// How wide this line makes the plate — known before it is shown, so it can be placed.
        /// A short line keeps its own width; anything past <see cref="MaxWidth"/> wraps instead.
        /// </summary>
        public static float WidthFor(string text)
        {
            int n = text == null ? 0 : text.Length;
            return Mathf.Clamp(n * Advance + PadX * 2f, 260f, MaxWidth);
        }

        /// <summary>…and how tall: the wrapped lines, plus the bracket's own when there is one.</summary>
        public static float HeightFor(string text, bool withBracket)
        {
            float body = LinesFor(text) * LineHeight + PadY * 2f;
            if (withBracket) body += BracketHeight;
            return Mathf.Max(PlateHeight, body);
        }

        /// <summary>The plate this line would occupy, design px.</summary>
        public static Vector2 SizeFor(string text, bool withBracket = false) =>
            new Vector2(WidthFor(text), HeightFor(text, withBracket));

        /// <summary>The design-space rectangle this line would occupy at <paramref name="centre"/>.</summary>
        public static Rect RectFor(string text, Vector2 centre, bool withBracket = false)
        {
            Vector2 size = SizeFor(text, withBracket);
            return new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);
        }

        /// <summary>
        /// A short LABEL as the drop sets its buttons: caps, with the tracking between the glyphs.
        ///
        /// Kept, and kept public, although the four teaching lines no longer go through it: this is the
        /// drop's setting for a one-word hint, and the day «ТРЯСИ» arrives as a string rather than as a
        /// PNG it is the function that has to draw it. <see cref="TrackedWidthFor"/> is its width.
        /// </summary>
        public static string Set(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string caps = text.ToUpperInvariant();
            var builder = new System.Text.StringBuilder(caps.Length * 2);
            for (int i = 0; i < caps.Length; i++)
            {
                if (i > 0) builder.Append(Tracking);
                builder.Append(caps[i]);
            }

            return builder.ToString();
        }

        /// <summary>Width of a <see cref="Set"/> label, design px — unwrapped, because a label is one line.</summary>
        public static float TrackedWidthFor(string text) =>
            (text == null ? 0 : text.Length) * TrackedAdvance + PadX * 2f;

        /// <summary>Design-space rect of the plate, for the overlap checks that place it.</summary>
        public Rect CardRect
        {
            get
            {
                Vector2 size = _plate.rectTransform.sizeDelta;
                Vector2 pos = _plate.rectTransform.anchoredPosition;
                return new Rect(pos.x - size.x * 0.5f, -pos.y - size.y * 0.5f, size.x, size.y);
            }
        }

        public void Hide() => _plate.gameObject.SetActive(false);

        /// <summary>
        /// Put the line up at <paramref name="centre"/>. No arrow — it never had one, because the beats
        /// this plate served drew their own; since 2026-09-22 there are no arrows on a teaching screen
        /// at all, so the method name is now simply what a hint is.
        /// </summary>
        /// <param name="bracket">
        /// An optional footnote line under the sentence, or null — which is what every caller passes
        /// since the PC brackets were withdrawn on 2026-09-22. A null bracket is not a special case:
        /// the plate is sized for one line and the second row is simply not built.
        /// </param>
        public void ShowCardOnly(string text, HintTone tone, Vector2 centre, string bracket = null)
        {
            _plate.gameObject.SetActive(true);

            bool hasBracket = !string.IsNullOrEmpty(bracket);
            Vector2 size = SizeFor(text, hasBracket);
            _plate.rectTransform.sizeDelta = size;
            Ui.MoveTo(_plate.rectTransform, centre);
            _centre = centre;

            _plate.color = HintCard.ToneColour(tone);
            _fill.color = Slab;

            // The sentence sits in the plate minus its padding, minus the bracket's band at the bottom.
            float bracketBand = hasBracket ? BracketHeight : 0f;
            RectTransform labelRt = _label.rectTransform;
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(size.x - PadX * 2f, size.y - PadY * 2f - bracketBand);
            labelRt.anchoredPosition = new Vector2(0f, bracketBand * 0.5f);

            _label.text = text ?? "";
            _label.color = Color.white;

            _bracket.gameObject.SetActive(hasBracket);
            if (hasBracket)
            {
                RectTransform bracketRt = _bracket.rectTransform;
                bracketRt.anchorMin = new Vector2(0.5f, 0.5f);
                bracketRt.anchorMax = new Vector2(0.5f, 0.5f);
                bracketRt.pivot = new Vector2(0.5f, 0.5f);
                bracketRt.sizeDelta = new Vector2(size.x - PadX * 2f, BracketHeight);
                bracketRt.anchoredPosition =
                    new Vector2(0f, -(size.y * 0.5f - PadY * 0.5f - BracketHeight * 0.5f));
                _bracket.text = bracket;
                _bracket.color = BracketInk;
            }
        }

        /// <summary>Where the plate stands right now, design px.</summary>
        public Vector2 Centre => _centre;
    }
}
