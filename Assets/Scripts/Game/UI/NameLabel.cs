using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class NameLabel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshPro label;

        [Header("Layout")]
        [Tooltip("World-space units above the player pivot")]
        [SerializeField] private float heightOffset = 1.4f;

        [Header("Style")]
        [SerializeField] private float fontSize = 3f;
        [SerializeField] private Color localPlayerColor = new Color(1f, 0.92f, 0.016f); // gold
        [SerializeField] private Color remotePlayerColor = Color.white;

        private const string LocalCameraTag = "LocalPlayerCamera";

        private Camera _cam;
        private bool _initialized;

        public void Setup(string playerName, bool isLocal)
        {
            _cam = FindActiveLocalCamera();

            transform.localPosition = new Vector3(0f, heightOffset, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (label != null)
            {
                label.text = playerName;
                label.fontSize = fontSize;
                label.color = isLocal ? localPlayerColor : remotePlayerColor;
                label.alignment = TextAlignmentOptions.Center;
                label.sortingOrder = 10;
                label.ForceMeshUpdate();
            }

            _initialized = true;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void ResetForPool()
        {
            if (label != null)
            {
                label.text = "";
            }
            _initialized = false;
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            if (_cam == null || !_cam.enabled || !_cam.gameObject.activeInHierarchy)
            {
                _cam = FindActiveLocalCamera();
                if (_cam == null)
                {
                    return;
                }
            }

            transform.rotation = _cam.transform.rotation;
        }

        private static Camera FindActiveLocalCamera()
        {
            
            var candidates = GameObject.FindGameObjectsWithTag(LocalCameraTag);
            foreach (var go in candidates)
            {
                if (!go.activeInHierarchy)
                {
                    continue;
                }

                var cam = go.GetComponent<Camera>();
                if (cam != null && cam.enabled)
                    return cam;
            }
            return null;
        }
    }
}