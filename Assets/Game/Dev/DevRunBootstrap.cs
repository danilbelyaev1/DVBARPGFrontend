using DVBARPG.Core.Services;
using UnityEngine;

namespace DVBARPG.Game.Dev
{
    public sealed class DevRunBootstrap : MonoBehaviour
    {
        [Header("Dev-старт")]
        [Tooltip("Автоматически логинит и выбирает класс при запуске сцены Run.")]
        [SerializeField] private bool autoLoginInRun = true;
        [Tooltip("Если включено — работает только в редакторе.")]
        [SerializeField] private bool onlyInEditor = true;
        [Tooltip("Класс по умолчанию при автологине.")]
        [SerializeField] private string defaultClassId = "Melee";

        private void Awake()
        {
            if (!autoLoginInRun) return;
            if (onlyInEditor && !Application.isEditor) return;

            var profile = DVBARPG.Core.GameRoot.Instance.Services.Get<IProfileService>();
            if (profile.CurrentAuth == null)
            {
                var auth = DVBARPG.Core.GameRoot.Instance.Services.Get<IAuthService>();
                var session = auth.Login();
                profile.SetAuth(session);
            }

            if (string.IsNullOrEmpty(profile.SelectedClassId))
            {
                profile.SetSelectedClass(defaultClassId);
            }
        }
    }
}
