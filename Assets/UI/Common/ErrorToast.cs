using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DVBARPG.UI.Common
{
    public sealed class ErrorToast : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private float autoHideSeconds = 3f;

        private float _hideAt;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;
            if (Time.unscaledTime >= _hideAt)
            {
                root.SetActive(false);
            }
        }

        public void ShowErrorCode(string errorCode)
        {
            var mapper = GameRoot.Instance?.Services?.Get<IErrorMapper>();
            var text = mapper != null ? mapper.Map(errorCode) : errorCode;
            Show(text);
        }

        public void Show(string message)
        {
            if (messageText != null) messageText.text = message;
            if (root != null) root.SetActive(true);
            _hideAt = Time.unscaledTime + Mathf.Max(1f, autoHideSeconds);
        }
    }
}
