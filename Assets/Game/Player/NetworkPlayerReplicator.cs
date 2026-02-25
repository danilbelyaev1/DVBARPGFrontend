using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.Game.Player
{
    public sealed class NetworkPlayerReplicator : MonoBehaviour
    {
        private NetworkSessionRunner _net;
        private float _lastLog;

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
            var pos = new Vector3(snap.Player.X, 0f, snap.Player.Y);
            transform.position = pos;

            if (Time.unscaledTime - _lastLog > 1f)
            {
                Debug.Log($"NetworkPlayerReplicator: snapshot pos {pos.x:0.00},{pos.z:0.00}");
                _lastLog = Time.unscaledTime;
            }
        }
    }
}
