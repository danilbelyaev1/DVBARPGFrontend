using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;

namespace DVBARPG.UI.Lobby
{
    /// <summary>
    /// Привязывает PlayerXpBar к progression из профиля (для лобби/меню).
    /// </summary>
    public sealed class ProfileXpBar : MonoBehaviour
    {
        [SerializeField] private DVBARPG.UI.Run.PlayerXpBar xpBar;
        private bool _initialized;

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (!_initialized)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            if (xpBar == null)
            {
                xpBar = GetComponent<DVBARPG.UI.Run.PlayerXpBar>();
            }

            if (xpBar == null) return;

            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            var progression = profile?.Progression;
            if (progression == null) return;

            var level = progression.Level;
            var total = progression.XpTotal;
            var baseXp = progression.XpCurrentLevelBase;
            var nextTotal = progression.XpNextLevelTotal;
            var required = Mathf.Max(1, nextTotal - baseXp);
            var inside = Mathf.Clamp(total - baseXp, 0, required);

            xpBar.UpdateXp(level, inside, required);
            _initialized = true;
        }
    }
}

