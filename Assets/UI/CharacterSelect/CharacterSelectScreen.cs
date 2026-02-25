using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DVBARPG.UI.CharacterSelect
{
    public sealed class CharacterSelectScreen : MonoBehaviour
    {
        [SerializeField] private Button meleeButton;
        [SerializeField] private Button rangedButton;
        [SerializeField] private Button mageButton;

        [SerializeField] private ClassData meleeData;
        [SerializeField] private ClassData rangedData;
        [SerializeField] private ClassData mageData;

        private void Awake()
        {
            if (meleeButton != null) meleeButton.onClick.AddListener(() => OnSelect(meleeData));
            if (rangedButton != null) rangedButton.onClick.AddListener(() => OnSelect(rangedData));
            if (mageButton != null) mageButton.onClick.AddListener(() => OnSelect(mageData));
        }

        private void OnDestroy()
        {
            if (meleeButton != null) meleeButton.onClick.RemoveAllListeners();
            if (rangedButton != null) rangedButton.onClick.RemoveAllListeners();
            if (mageButton != null) mageButton.onClick.RemoveAllListeners();
        }

        private void OnSelect(ClassData data)
        {
            if (data == null) return;

            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            profile.SetSelectedClass(data.Id.ToString());

            SceneManager.LoadScene("Run");
        }
    }
}
