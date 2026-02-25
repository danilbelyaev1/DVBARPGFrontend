using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Core.Simulation;
using UnityEngine;

namespace DVBARPG.Game.Player
{
    public sealed class LocalPlayerMover : MonoBehaviour, ILocalMover
    {
        [SerializeField] private string entityId = "local-player";

        public string EntityId => entityId;

        private void Start()
        {
            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            if (profile.CurrentAuth != null && !string.IsNullOrEmpty(profile.CurrentAuth.PlayerId))
            {
                entityId = profile.CurrentAuth.PlayerId;
            }

            var session = GameRoot.Instance.Services.Get<ISessionService>();
            session.RegisterLocalMover(this);
        }

        public void ApplyMove(Vector3 delta)
        {
            transform.position += delta;
        }
    }
}
