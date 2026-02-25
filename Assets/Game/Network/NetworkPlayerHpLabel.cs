using DVBARPG.Net.Network;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.Game.Network
{
    public sealed class NetworkPlayerHpLabel : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Текст, куда выводится HP.")]
        [SerializeField] private Text targetText;

        private NetworkSessionRunner _net;

        private void OnEnable()
        {
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
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
            if (targetText == null) return;
            // Обновляем HP из серверного снапшота.
            targetText.text = $"HP {snap.Player.Hp}/{snap.Player.MaxHp}";
        }
    }
}
