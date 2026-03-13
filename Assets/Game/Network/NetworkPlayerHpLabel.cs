using DVBARPG.Net.Network;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.Game.Network
{
    public sealed class NetworkPlayerHpLabel : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Слайдер HP: значение от 0 до 1 (Hp/MaxHp).")]
        [SerializeField] private Slider hpSlider;
        [Tooltip("Опционально: текст вида «HP x/y».")]
        [SerializeField] private Text targetText;

        private NetworkSessionRunner _net;

        private void Awake()
        {
            if (hpSlider != null)
            {
                hpSlider.minValue = 0f;
                hpSlider.maxValue = 1f;
                hpSlider.value = 0f;
            }
        }

        private void OnEnable()
        {
            var root = DVBARPG.Core.GameRoot.Instance;
            if (root == null || root.Services == null) return;
            if (!root.Services.TryGet<DVBARPG.Core.Services.ISessionService>(out var session)) return;
            _net = session as NetworkSessionRunner;
            if (_net != null)
            {
                _net.Snapshot += OnSnapshot;
            }
        }

        private void OnDisable()
        {
            if (_net != null)
            {
                _net.Snapshot -= OnSnapshot;
            }
        }

        private void OnSnapshot(SnapshotEnvelope snap)
        {
            int hp = snap.Player.Hp;
            int maxHp = snap.Player.MaxHp;
            float normalized = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;

            if (hpSlider != null)
                hpSlider.value = normalized;

            if (targetText != null)
                targetText.text = $"HP {hp}/{maxHp}";
        }
    }
}
