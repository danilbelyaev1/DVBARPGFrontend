using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DVBARPG.UI.LoadingOverlay
{
    /// <summary>
    /// Глобальный лоадер: при любом запросе показывается модалка со спиннером (из префаба).
    /// Ref-count: несколько одновременных запросов — одна модалка, скрывается когда счётчик = 0.
    /// Префаб: Assets/Resources/Prefabs/UI/LoadingOverlay.prefab (или назначить вручную). Message — Text или TextMeshProUGUI.
    /// </summary>
    public sealed class LoadingOverlayService : MonoBehaviour, ILoadingOverlayService
    {
        [Header("Префаб")]
        [Tooltip("Если не назначен — загружается из Resources/Prefabs/UI/LoadingOverlay.")]
        [SerializeField] private GameObject overlayPrefab;

        [Header("Визуал")]
        [Tooltip("Текст по умолчанию при показе.")]
        [SerializeField] private string defaultMessage = "Загрузка...";
        [Tooltip("Скорость вращения спиннера (град/сек).")]
        [SerializeField] private float spinnerSpeed = 360f;

        private int _requestCount;
        private GameObject _root;
        private Transform _spinnerTransform;
        private Text _messageTextLegacy;
        private TMP_Text _messageTmp;
        private bool _built;

        public void BeginRequest(string message = null)
        {
            _requestCount++;
            EnsureBuilt();
            if (_root != null)
            {
                _root.SetActive(true);
                var text = string.IsNullOrWhiteSpace(message) ? defaultMessage : message;
                if (_messageTmp != null) _messageTmp.text = text;
                else if (_messageTextLegacy != null) _messageTextLegacy.text = text;
            }
        }

        public void EndRequest()
        {
            if (_requestCount > 0) _requestCount--;
            if (_requestCount <= 0 && _root != null)
            {
                _requestCount = 0;
                _root.SetActive(false);
            }
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            BuildOverlay();
        }

        private void BuildOverlay()
        {
            var prefab = overlayPrefab != null ? overlayPrefab : Resources.Load<GameObject>("Prefabs/UI/LoadingOverlay");
            if (prefab == null)
            {
                Debug.LogWarning("LoadingOverlayService: префаб не найден. Положи LoadingOverlay.prefab в Assets/Resources/Prefabs/UI/.");
                return;
            }

            _root = Instantiate(prefab, transform);
            _root.name = "LoadingOverlay";

            var canvas = _root.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 9999;

            var spinnerGo = _root.transform.Find("Panel/Center/Spinner");
            if (spinnerGo == null) spinnerGo = _root.transform.Find("Spinner");
            if (spinnerGo == null) spinnerGo = FindChildRecursive(_root.transform, "Spinner");
            _spinnerTransform = spinnerGo;

            var messageGo = _root.transform.Find("Panel/Center/Message");
            if (messageGo == null) messageGo = _root.transform.Find("Message");
            if (messageGo != null)
            {
                _messageTmp = messageGo.GetComponent<TMP_Text>();
                _messageTextLegacy = _messageTmp == null ? messageGo.GetComponent<Text>() : null;
            }
            else
            {
                _messageTmp = _root.GetComponentInChildren<TMP_Text>(true);
                _messageTextLegacy = _messageTmp == null ? _root.GetComponentInChildren<Text>(true) : null;
            }
            var defaultMsg = defaultMessage;
            if (_messageTmp != null) _messageTmp.text = defaultMsg;
            else if (_messageTextLegacy != null) _messageTextLegacy.text = defaultMsg;

            _root.SetActive(false);
        }

        private void Update()
        {
            if (_requestCount > 0 && _spinnerTransform != null)
                _spinnerTransform.Rotate(0f, 0f, -spinnerSpeed * Time.deltaTime, Space.Self);
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
