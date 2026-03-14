using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    /// <summary>
    /// При выгрузке сцены Run отключает UDP-сессию, чтобы при следующем входе в Run создавался новый инстанс.
    /// </summary>
    public sealed class RunSceneCleanup : MonoBehaviour
    {
        private void OnDestroy()
        {
            var session = GameRoot.Instance?.Services?.Get<ISessionService>();
            if (session != null && session.IsConnected)
            {
                session.Disconnect();
            }
        }
    }
}
