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
            SceneManager.LoadScene("CharacterSelect");
        }
    }
}
