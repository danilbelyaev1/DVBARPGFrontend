using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    public sealed class NetworkRunConnector : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "ws://localhost:8080/ws";
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
