using Meditation.Mechanics;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Game
{
    /// <summary>
    /// S1 «Титул». Level 1's street behind the title with a few thoughts drifting across it, the two
    /// pictogram cards, and the line that says how to begin. The game starts on two turns of the
    /// dynamo — «тематично: игра начинается с медленного кручения» (SCREENS S1).
    /// </summary>
    public sealed class TitleScreen : GameScreen
    {
        /// <summary>Two full turns of the crank start the game (SCREENS S1, walkthrough frame 1).</summary>
        public const float StartDegrees = 720f;

        private const int DriftingThoughts = 4;

        private readonly Image[] _thoughts = new Image[DriftingThoughts];
        private readonly Vector2[] _positions = new Vector2[DriftingThoughts];
        private readonly Vector2[] _velocities = new Vector2[DriftingThoughts];

        private float _turned;

        public TitleScreen(GameFlow flow) : base(flow, "TitleScreen")
        {
            LevelDefinition level = LevelCatalog.At(0);

            var background = Ui.NewImage(Root, "TitleBackground");
            background.sprite = ArtLibrary.Get(level.BackgroundSprite);
            background.color = background.sprite != null ? Color.white : LevelOneData.Sky;
            Ui.Place(background.rectTransform, 960f, 540f, 1920f, 1080f);

            BuildDriftingThoughts(level);

            // A soft veil between the street and the type: the plate is a full-contrast picture, and
            // the title has to be the first thing read from a museum distance.
            Ui.BoxCentred(Root, "TitleVeil", 960f, 540f, 1920f, 1080f, new Color(0.05f, 0.05f, 0.1f, 0.42f));

            Ui.Label(Root, "TitleText", GameTexts.Title, 960f, 300f, 1800f, 150f,
                GameTexts.TitleSize, new Color(0.98f, 0.97f, 0.93f), TextAnchor.MiddleCenter,
                FontStyle.Bold);

            BuildCard("CrankCard", 740f, GameTexts.CrankCardTitle, GameTexts.CrankCardSubtitle,
                HintCard.ToneColour(HintTone.Crank));
            BuildCard("ShakeCard", 1180f, GameTexts.ShakeCardTitle, GameTexts.ShakeCardSubtitle,
                HintCard.ToneColour(HintTone.Shake));

            StartHint = Ui.Label(Root, "StartHint", GameTexts.TitleStartHint, 960f, 900f, 1400f, 60f,
                GameTexts.TitleStartHintSize, new Color(0.93f, 0.92f, 0.88f));

            // How much of the two turns is done — the only feedback the title needs.
            Progress = Ui.Ring(Root, "StartProgress", 960f, 980f, 34f, HintCard.ToneColour(HintTone.Crank));
            Progress.fillAmount = 0f;
        }

        public Text StartHint { get; }

        public Image Progress { get; }

        /// <summary>Degrees turned so far, capped at the two turns that start the game.</summary>
        public float Turned => _turned;

        private void BuildDriftingThoughts(LevelDefinition level)
        {
            for (int i = 0; i < DriftingThoughts; i++)
            {
                string key = level.ThoughtSprites[i % level.ThoughtSprites.Length];
                Vector2 size = ArtLibrary.FitThought(key, (ThoughtStrength)(i % 3));

                Image blob = Ui.NewImage(Root, "TitleThought" + i);
                blob.sprite = ArtLibrary.Get(key);
                blob.preserveAspect = true;
                blob.color = new Color(1f, 1f, 1f, 0.75f);

                _positions[i] = new Vector2(260f + i * 470f, 190f + (i % 2) * 520f);
                // Slow, unhurried, and never straight up or down — the title breathes rather than scrolls.
                _velocities[i] = new Vector2(i % 2 == 0 ? 14f : -11f, i % 3 == 0 ? 7f : -6f);
                Ui.Place(blob.rectTransform, _positions[i].x, _positions[i].y, size.x, size.y);
                _thoughts[i] = blob;
            }
        }

        /// <summary>The cards are near-white; their icons are cut out of that same paper.</summary>
        private static readonly Color CardPaper = new Color(1f, 1f, 1f, 0.95f);

        private void BuildCard(string name, float centreX, string title, string subtitle, Color ink)
        {
            // SCREENS S1: карточки 360×240, y=520 (top edge) — so the centre sits at 640.
            Image card = Ui.Rounded(Root, name, centreX, 640f, 360f, 240f, CardPaper, ink, 4f, 16);

            // Labels are placed in the card's own space: the icons take the upper half of the card in
            // the mock, the words the lower one.
            Text titleLabel = Ui.Label(card.transform, name + "Title", title, centreX, 690f, 340f, 50f,
                GameTexts.CardTitleSize, new Color(0.2f, 0.2f, 0.2f), TextAnchor.MiddleCenter,
                FontStyle.Bold);
            titleLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            titleLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            titleLabel.rectTransform.anchoredPosition = new Vector2(0f, -50f);

            Text subtitleLabel = Ui.Label(card.transform, name + "Subtitle", subtitle, centreX, 736f,
                340f, 40f, GameTexts.CardSubtitleSize, new Color(0.4f, 0.4f, 0.4f));
            subtitleLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            subtitleLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            subtitleLabel.rectTransform.anchoredPosition = new Vector2(0f, -96f);

            // The hand each card is about, drawn as the mock draws it: a crank dial with its handle,
            // a stick with shake lines. Icons, not glyphs — the cabinet has no font for these.
            BuildCardIcon(card.transform, name, ink);
        }

        /// <summary>The pictograms of walkthrough frame 1: a crank ring with a handle, a joystick.</summary>
        private static void BuildCardIcon(Transform card, string name, Color ink)
        {
            bool crank = name.StartsWith("Crank");

            var icon = new GameObject(name + "Icon", typeof(RectTransform));
            icon.transform.SetParent(card, false);
            var host = (RectTransform)icon.transform;
            host.anchorMin = new Vector2(0.5f, 0.5f);
            host.anchorMax = new Vector2(0.5f, 0.5f);
            host.pivot = new Vector2(0.5f, 0.5f);
            host.sizeDelta = new Vector2(160f, 120f);
            host.anchoredPosition = new Vector2(0f, 46f);

            if (crank)
            {
                // The hole is painted in the card's own paper: a transparent "fill" simply lets the
                // ring's own disc show through, which drew a solid green blob instead of a crank.
                Image ring = Ui.Circle(host, "CrankRing", 0f, 0f, 46f, CardPaper, ink, 8f);
                ring.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                ring.rectTransform.anchoredPosition = Vector2.zero;

                Image handle = Ui.BoxCentred(host, "CrankHandle", 0f, 0f, 14f, 34f, ink);
                handle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                handle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                handle.rectTransform.anchoredPosition = new Vector2(38f, 27f);
                return;
            }

            Image stick = Ui.BoxCentred(host, "StickShaft", 0f, 0f, 18f, 74f, ink);
            stick.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            stick.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            stick.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            Image knob = Ui.Circle(host, "StickKnob", 0f, 0f, 24f, ink, ink);
            knob.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knob.rectTransform.anchoredPosition = new Vector2(0f, 38f);

            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -52f : 52f;
                float y = i % 2 == 0 ? 20f : -12f;
                Image dash = Ui.BoxCentred(host, "ShakeLine" + i, 0f, 0f, 26f, 6f, ink);
                dash.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                dash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dash.rectTransform.anchoredPosition = new Vector2(x, y);
            }
        }

        protected override void Tick(float deltaTime, in Hands hands)
        {
            for (int i = 0; i < _thoughts.Length; i++)
            {
                _positions[i] += _velocities[i] * deltaTime;

                // Wrap around the frame with a generous margin, so a blob never blinks at an edge.
                if (_positions[i].x < -300f) _positions[i].x = 2220f;
                if (_positions[i].x > 2220f) _positions[i].x = -300f;
                if (_positions[i].y < -260f) _positions[i].y = 1340f;
                if (_positions[i].y > 1340f) _positions[i].y = -260f;

                Ui.MoveTo(_thoughts[i].rectTransform, _positions[i]);
            }

            // Direction does not matter — turning the handle is turning the handle.
            _turned = Mathf.Min(StartDegrees, _turned + Mathf.Abs(hands.CrankDelta));
            Progress.fillAmount = _turned / StartDegrees;

            if (_turned >= StartDegrees) Flow.StartRun();
        }

        public override string Readout()
        {
            return "экран: титул\n" +
                   "оборотов: " + (_turned / 360f).ToString("0.00") + " / 2\n" +
                   "старт по двум оборотам динамо";
        }
    }
}
