using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.UI.Run
{
    /// <summary>
    /// Обновляет PlayerXpBar во время рана, используя progression из профиля и runXpTotal из снапшотов.
    /// </summary>
    public sealed class RunXpBar : MonoBehaviour
    {
        [SerializeField] private PlayerXpBar xpBar;

        private NetworkSessionRunner _net;
        private int _baseXpTotal;
        private int _level;
        private int _xpCurrentLevelBase;
        private int _xpNextLevelTotal;
        private bool _initialized;

        private void OnEnable()
        {
            if (xpBar == null)
            {
                xpBar = GetComponent<PlayerXpBar>();
            }

            TryInitFromProfile();

            var session = GameRoot.Instance.Services.Get<ISessionService>();
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
                _net = null;
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                TryInitFromProfile();
            }
        }

        private void OnSnapshot(SnapshotEnvelope snap)
        {
            if (!_initialized)
            {
                TryInitFromProfile();
            }

            // XP, заработанный в текущем ране.
            var sessionXp = snap.RunXpTotal;
            var totalXp = _baseXpTotal + sessionXp;
            ApplyXp(totalXp);
        }

        private void TryInitFromProfile()
        {
            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            var progression = profile?.Progression;
            if (progression == null) return;

            _level = progression.Level;
            _baseXpTotal = progression.XpTotal;
            _xpCurrentLevelBase = progression.XpCurrentLevelBase;
            _xpNextLevelTotal = progression.XpNextLevelTotal;

            ApplyXp(_baseXpTotal);
            _initialized = true;
        }

        private void ApplyXp(int totalXp)
        {
            if (xpBar == null) return;

            var baseXp = _xpCurrentLevelBase;
            var nextTotal = _xpNextLevelTotal;
            var required = Mathf.Max(1, nextTotal - baseXp);
            var inside = Mathf.Clamp(totalXp - baseXp, 0, required);

            xpBar.UpdateXp(_level, inside, required);
        }
    }
}

