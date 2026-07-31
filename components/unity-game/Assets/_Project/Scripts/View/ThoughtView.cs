using System.Collections.Generic;
using Meditation.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// One thought on screen: rounded placeholder blob, its label, and a row of pips = hits left
    /// (SCREENS.md "Мысли (визуал состояния)"). Hits make it flinch; the last pip pops it.
    /// </summary>
    public sealed class ThoughtView
    {
        private const int MaxPips = 12;
        private const float PipRadius = 9f;
        private const float PipStep = 28f;

        private readonly List<Image> _pips = new List<Image>(MaxPips);
        private readonly Image _root;
        private readonly Text _label;
        private readonly RectTransform _pipRow;

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

            var rowGo = new GameObject("Pips", typeof(RectTransform));
            rowGo.transform.SetParent(_root.transform, false);
            _pipRow = (RectTransform)rowGo.transform;
            _pipRow.anchorMin = new Vector2(0.5f, 0f);
            _pipRow.anchorMax = new Vector2(0.5f, 0f);
            _pipRow.pivot = new Vector2(0.5f, 0.5f);
            _pipRow.sizeDelta = new Vector2(MaxPips * PipStep, PipRadius * 2f);
            _pipRow.anchoredPosition = new Vector2(0f, -18f);

            for (int i = 0; i < MaxPips; i++)
            {
                var pip = Ui.Circle(_pipRow, "Pip" + i, 0f, 0f, PipRadius, Color.black, Color.black);
                pip.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                pip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                pip.gameObject.SetActive(false);
                _pips.Add(pip);
            }
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

            int durability = Mathf.Clamp(thought.Durability, 0, MaxPips);
            int remaining = Mathf.Clamp(thought.HitsRemaining, 0, MaxPips);
            _pipRow.anchoredPosition = new Vector2(0f, -18f);
            for (int i = 0; i < MaxPips; i++)
            {
                bool used = i < durability;
                _pips[i].gameObject.SetActive(used);
                if (!used) continue;
                float x = (i - (durability - 1) * 0.5f) * PipStep;
                _pips[i].rectTransform.anchoredPosition = new Vector2(x, 0f);

                // A spent pip is an EMPTY outlined circle (mock 13), not a faded dot: at museum
                // distance a pale fill reads as "still there, just dimmer".
                bool spent = i >= remaining;
                _pips[i].sprite = spent ? UiSprites.Ring : UiSprites.Circle;
                _pips[i].color = stroke;
            }
        }
    }
}
