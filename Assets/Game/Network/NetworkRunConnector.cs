using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    public sealed class NetworkRunConnector : MonoBehaviour
    {
        [Header("Network")]
        [Tooltip("UDP server endpoint.")]
        [SerializeField] private string serverUrl = "udp://127.0.0.1:8081";
        [Tooltip("Server map id.")]
        [SerializeField] private string mapId = "VadimTests";

        private void Start()
        {
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            if (session is NetworkSessionRunner net)
            {
                var auth = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.IProfileService>().CurrentAuth;
                net.Connect(auth, mapId, serverUrl);
            }
        }
    }
}
