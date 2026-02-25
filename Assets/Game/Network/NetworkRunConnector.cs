using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    public sealed class NetworkRunConnector : MonoBehaviour
    {
        [Header("Сеть")]
        [Tooltip("Адрес WebSocket сервера.")]
        [SerializeField] private string serverUrl = "ws://localhost:8080/ws";
        [Tooltip("ID карты на сервере.")]
        [SerializeField] private string mapId = "VadimTests";

        private void Start()
        {
            // При входе в Run автоматически подключаемся к серверу.
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            if (session is NetworkSessionRunner net)
            {
                var auth = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.IProfileService>().CurrentAuth;
                net.Connect(auth, mapId, serverUrl);
            }
        }
    }
}
