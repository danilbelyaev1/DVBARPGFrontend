using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Net.Commands;
using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.Game.Network
{
    /// <summary>
    /// Подписывается на RunEnded от сессии и обновляет RunResultState. Предоставляет метод для досрочного выхода (finish).
    /// </summary>
    public sealed class RunEndController : MonoBehaviour
    {
        [Header("Игрок")]
        [Tooltip("Объект игрока (скрывается при завершении забега). Если не задан — ищется NetworkPlayerReplicator.")]
        [SerializeField] private GameObject playerObject;

        private NetworkSessionRunner _net;

        private void OnEnable()
        {
            var session = GameRoot.Instance?.Services?.Get<ISessionService>();
            _net = session as NetworkSessionRunner;
            if (_net != null)
            {
                _net.RunEnded += OnRunEnded;
            }
        }

        private void OnDisable()
        {
            if (_net != null)
            {
                _net.RunEnded -= OnRunEnded;
            }
        }

        private void OnRunEnded(bool playerDied)
        {
            RunResultState.SetRunEnded(playerDied, RunResultState.Kills);
            HidePlayer();
        }

        private void HidePlayer()
        {
            var go = playerObject != null ? playerObject : FindPlayerGameObject();
            if (go != null)
            {
                go.SetActive(false);
            }
        }

        private static GameObject FindPlayerGameObject()
        {
            var replicator = Object.FindFirstObjectByType<DVBARPG.Game.Player.NetworkPlayerReplicator>();
            return replicator != null ? replicator.gameObject : null;
        }

        /// <summary>
        /// Досрочное завершение забега. Отправляет команду finish на сервер и помечает забег как завершённый (не смерть).
        /// </summary>
        public void RequestFinishRun()
        {
            var session = GameRoot.Instance?.Services?.Get<ISessionService>();
            if (session == null || !session.IsConnected) return;
            session.Send(new CmdFinish());
        }
    }
}
