using TMPro;
using UnityEngine;

namespace Game.UI.Territory
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class TerritoryClaimPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private float duration = 1.0f;
        [SerializeField] private float flyPixelsValue = 80f;
        [SerializeField] private float peakScale = 1.15f;
        [SerializeField] private float scalePeakTime = 0.25f;
        [SerializeField] private float fadeStartTime = 0.4f;
        [SerializeField] private float screenOffsetY = 60f;
        [SerializeField] private bool showAsPercentage = true;
        [SerializeField] private bool usePlayerColor = true;
        [SerializeField] private Color textColor = Color.white;

        private RectTransform _rectTransform;
        private Transform _trackTarget;
        private Camera _camera;
        private float _elapsed;
        private bool _isPlaying;

        private void EnsureReferences()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Show(Transform trackTarget, int cellsClaimed, uint totalCells, Color playerColor, Camera playerCamera)
        {
            gameObject.SetActive(true);
            EnsureReferences();
    
            _trackTarget = trackTarget;
            _camera = playerCamera;
            _elapsed = 0f;
            _isPlaying = true;

            if (showAsPercentage)
            {
                float pct = (cellsClaimed * 100f) / totalCells;
                if (pct is >= 0.01f and < 0.1f)
                {
                    label.text = $"+{pct:F2}%";
                }
                else if (pct >= 0.1f)
                {
                    label.text = $"+{pct:F1}%";
                }
                else
                {
                    label.text = $"+{cellsClaimed}%";
                }
            }
            else
            {
                label.text = $"+{cellsClaimed}";
            }

            if (usePlayerColor)
            {
                Color bright = Color.Lerp(playerColor, Color.white, 0.35f);
                label.color = bright;
            }
            else
            {
                label.color = textColor;
            }

            _rectTransform.localScale = Vector3.one;
            canvasGroup.alpha = 1f;

            UpdateScreenPosition(0f);

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isPlaying) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / duration);

            UpdateScreenPosition(t);

            float scale;
            if (t < scalePeakTime)
            {
                float st = t / scalePeakTime;
                float eased = 1f - (1f - st) * (1f - st);
                scale = Mathf.Lerp(1f, peakScale, eased);
            }
            else
            {
                float st = (t - scalePeakTime) / (1f - scalePeakTime);
                scale = Mathf.Lerp(peakScale, 1f, st * st);
            }
            _rectTransform.localScale = Vector3.one * scale;

            float alpha;
            if (t < fadeStartTime)
            {
                alpha = 1f;
            }
            else
            {
                float ft = (t - fadeStartTime) / (1f - fadeStartTime);
                alpha = 1f - ft;
            }
            canvasGroup.alpha = alpha;

            if (t >= 1f)
            {
                _isPlaying = false;
                gameObject.SetActive(false);
            }
        }

        private void UpdateScreenPosition(float t)
        {
            if (_trackTarget == null || _camera == null)
            {
                return;
            }

            Vector3 screenPos = _camera.WorldToScreenPoint(_trackTarget.position);

            if (screenPos.z < 0f)
            {
                canvasGroup.alpha = 0f;
                return;
            }

            screenPos.y += screenOffsetY + (flyPixelsValue * t);

            _rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
        }

        public void ForceFinish()
        {
            _isPlaying = false;
            _trackTarget = null;
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public void ResetForPool()
        {
            _isPlaying = false;
            _elapsed = 0f;
            _trackTarget = null;
            canvasGroup.alpha = 0f;
            _rectTransform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        public bool IsPlaying => _isPlaying;
    }
}
