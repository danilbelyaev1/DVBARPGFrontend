using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DVBARPG.Core;
using DVBARPG.Core.Services;

namespace DVBARPG.UI.Login
{
    public sealed class LoginScreen : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Кнопка 'Play' для входа.")]
        [SerializeField] private Button playButton;

        private void Awake()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
            }
        }

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayClicked);
            }
        }

        private void OnPlayClicked()
        {
            var auth = GameRoot.Instance.Services.Get<IAuthService>();
            var profile = GameRoot.Instance.Services.Get<IProfileService>();

            var session = auth.Login();
            profile.SetAuth(session);

            SceneManager.LoadScene("CharacterSelect");
        }
    }
}
