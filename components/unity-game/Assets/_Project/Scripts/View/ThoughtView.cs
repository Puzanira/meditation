using Meditation.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// One thought on screen: rounded placeholder blob, its label, and a row of pips = hits left
    /// (SCREENS.md "Мысли (визуал состояния)"). Hits make it flinch; the last pip pops it.
    ///
    /// This is the preview stand's greybox blob. The game draws thoughts as the drop's turquoise
    /// silhouettes instead — see <see cref="ArtThoughtView"/>; both share <see cref="PipRow"/>.
    /// </summary>
    public sealed class ThoughtView
    {
        private readonly Image _root;
        private readonly Text _label;
        private readonly PipRow _pips;

        private int _lastHitsTaken;
        private float _flinch;
        private Vector2 _flinchOffset;

        public ThoughtView(Transform parent)
        {
            _root = Ui.Rounded(parent, "Thought", 0f, 0f, 280f, 220f, Color.white, Color.black, 4f, 60);
            _label = Ui.Label(_root.transform, "Label", "", 0f, 0f, 400f, 60f, 32, Color.black);
            Ui.Place(_label.rectTransform, 0f, 0f, 400f, 60f);
            _label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _label.rectTransform.anchoredPosition = new Vector2(0f, 10f);

            _pips = new PipRow(_root.transform);
        }

        public RectTransform Rect => _root.rectTransform;

        public GameObject GameObject => _root.gameObject;

        public bool Active => _root.gameObject.activeSelf;

        public void SetActive(bool active) => _root.gameObject.SetActive(active);

        public void Bind(Thought thought, float deltaTime, bool desaturated = false)
        {
            Color fill = LevelOneData.ThoughtFill(thought.Label);
            Color stroke = LevelOneData.ThoughtStroke(thought.Label);
            if (desaturated)
            {
                // Frame 17's band, not a grey wash: LevelOneData.Faded keeps the pastel readable.
                fill = LevelOneData.Faded(fill);
                stroke = LevelOneData.Faded(stroke);
            }

            // The walkthrough draws S/M blobs with rx=60 and the big ones with rx=80.
            Sprite shape = UiSprites.RoundedOf(thought.Size.x >= 400f ? 80 : 60);
            _root.color = stroke;
            _root.sprite = shape;
            var innerImage = _root.transform.GetChild(0).GetComponent<Image>();
            if (innerImage != null)
            {
                innerImage.color = fill;
                innerImage.sprite = shape;
            }

            _root.rectTransform.sizeDelta = thought.Size;
            _label.text = thought.Label;
            _label.color = new Color(stroke.r * 0.6f, stroke.g * 0.6f, stroke.b * 0.6f, 1f);
            _label.fontSize = thought.Size.x >= 400f ? 34 : 30;

            if (thought.HitsTaken > _lastHitsTaken)
            {
                _flinch = 0.08f;
                _flinchOffset = new Vector2(Random.Range(-6f, 6f), Random.Range(-6f, 6f));
            }
            _lastHitsTaken = thought.HitsTaken;

            if (_flinch > 0f) _flinch = Mathf.Max(0f, _flinch - deltaTime);
            Vector2 offset = _flinch > 0f ? _flinchOffset : Vector2.zero;
            Ui.MoveTo(_root.rectTransform, thought.Position + offset);

            _pips.Set(thought.Durability, thought.HitsRemaining, stroke);
        }
    }
}
