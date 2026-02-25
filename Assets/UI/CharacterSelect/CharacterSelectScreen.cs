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
        [Header("Кнопки")]
        [Tooltip("Кнопка выбора класса Melee.")]
        [SerializeField] private Button meleeButton;
        [Tooltip("Кнопка выбора класса Ranged.")]
        [SerializeField] private Button rangedButton;
        [Tooltip("Кнопка выбора класса Mage.")]
        [SerializeField] private Button mageButton;

        [Header("Данные классов")]
        [Tooltip("ScriptableObject для класса Melee.")]
        [SerializeField] private ClassData meleeData;
        [Tooltip("ScriptableObject для класса Ranged.")]
        [SerializeField] private ClassData rangedData;
        [Tooltip("ScriptableObject для класса Mage.")]
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
