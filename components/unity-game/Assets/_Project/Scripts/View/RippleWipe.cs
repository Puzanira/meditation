using Meditation.Mechanics;
using Meditation.Tuning;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// The screen transition of the founder's playtest list (2026-09-22, п.8): «круглая рябь» —
    /// a ripple that closes over the frame from the vessel outwards, and opens again on the next screen.
    ///
    /// It replaces the cut to black the flow has used between every pair of screens since it was built.
    /// A black fade is the absence of a transition; this game is about a bucket of water, the level
    /// ends with the bucket in the middle of the frame, and «наезд на ведёрко → рябь» is a sentence
    /// about the same object doing both things.
    ///
    /// The class is the two things the shader cannot own: WHERE the ripple starts — which is a design-px
    /// point on the plate, the level's own vessel, converted into the UV the shader wants — and the
    /// clock. Everything else is <c>Meditation/RippleWipe</c>, and the four knobs the founder can move
    /// are read here every frame so the panel changes the transition while it is running.
    ///
    /// Degrades to the old fade rather than to a magenta screen: with no shader the image is a flat
    /// dark quad whose alpha is the progress, which is exactly what the flow used to do.
    /// </summary>
    public sealed class RippleWipe
    {
        /// <summary>The water — the dark the frame settles to when the ripple is closed.</summary>
        public static readonly Color Water = new Color(0.04f, 0.10f, 0.13f, 1f);

        /// <summary>…and the crest riding inside its front: the drop's own turquoise.</summary>
        public static readonly Color Crest = new Color(0.56f, 0.86f, 0.86f, 1f);

        private static readonly int CentreId = Shader.PropertyToID("_Centre");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
        private static readonly int AmplitudeId = Shader.PropertyToID("_Amplitude");
        private static readonly int WavesId = Shader.PropertyToID("_Waves");
        private static readonly int CrestId = Shader.PropertyToID("_CrestColor");

        private readonly Image _image;
        private readonly Material _material;

        private float _progress;
        private Vector2 _centre = new Vector2(960f, 540f);

        public RippleWipe(Transform parent, string name = "RippleWipe")
        {
            _image = Ui.BoxCentred(parent, name, 960f, 540f, 1920f, 1080f, Water);
            _image.raycastTarget = false;

            // Resources, not Shader.Find: the shader ships inside the game's own Resources folder, and
            // Shader.Find only sees what a build decided to include.
            var shader = Resources.Load<Shader>(LevelCatalog.ArtRoot + "shaders/ripple-wipe");
            if (shader != null)
            {
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _material.SetColor(CrestId, Crest);
                _material.SetFloat(AspectId,
                    DesignStage.DesignWidth / Mathf.Max(1f, DesignStage.DesignHeight));
                _image.material = _material;
            }

            SetProgress(0f);
        }

        /// <summary>The quad itself — the suite measures the composited pixels through it.</summary>
        public Image Image => _image;

        /// <summary>True when the ripple has a shader under it rather than the flat fallback.</summary>
        public bool HasShader => _material != null;

        /// <summary>How closed the ripple is: 0 = the frame is clear, 1 = the frame is water.</summary>
        public float Progress => _progress;

        /// <summary>Where the ripple starts from, design px — the level's vessel.</summary>
        public Vector2 Centre => _centre;

        /// <summary>
        /// Aim it at the vessel of the level that is ending. Design px in, UV out — and the Y axis
        /// flips on the way, because SCREENS counts from the top of the frame and a texture does not.
        /// </summary>
        public void SetCentre(Vector2 designPoint)
        {
            _centre = designPoint;
            if (_material == null) return;

            _material.SetVector(CentreId, new Vector4(
                Mathf.Clamp01(designPoint.x / DesignStage.DesignWidth),
                Mathf.Clamp01(1f - designPoint.y / DesignStage.DesignHeight), 0f, 0f));
        }

        /// <summary>
        /// Move the ripple. The three shape knobs go in on every call rather than at build time: the
        /// founder tunes them on the panel while the transition is running, and a transition that only
        /// reads its numbers once is a transition she has to leave and re-enter to see.
        /// </summary>
        public void SetProgress(float progress01)
        {
            _progress = Mathf.Clamp01(progress01);

            bool up = _progress > 0.0005f;
            if (_image.gameObject.activeSelf != up) _image.gameObject.SetActive(up);

            if (_material == null)
            {
                // No shader: the old fade, which is honest about being the old fade.
                _image.color = new Color(Water.r, Water.g, Water.b, _progress);
                return;
            }

            _image.color = Color.white;
            _material.SetFloat(ProgressId, _progress);
            _material.SetFloat(RingWidthId, Mathf.Clamp(TuningConfig.RippleRingWidth, 0.01f, 0.6f));
            _material.SetFloat(AmplitudeId, Mathf.Clamp(TuningConfig.RippleAmplitude, 0f, 0.3f));
            _material.SetFloat(WavesId, Mathf.Clamp(TuningConfig.RippleWaves, 1f, 12f));
        }

        /// <summary>Put the ripple on top of whatever was just built — it covers the frame or nothing.</summary>
        public void BringToFront() => _image.rectTransform.SetAsLastSibling();

        public void Dispose()
        {
            if (_material != null) Object.DestroyImmediate(_material);
            if (_image != null) Object.Destroy(_image.gameObject);
        }
    }
}
