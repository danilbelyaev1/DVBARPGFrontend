using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.CharacterSelect
{
    public sealed class SelectedClassDebugLabel : MonoBehaviour
    {
        [SerializeField] private Text targetText;

        private void Start()
        {
            if (targetText == null) return;

            var profile = GameRoot.Instance.Services.Get<IProfileService>();
            var selected = profile.SelectedClassId;
            targetText.text = string.IsNullOrEmpty(selected) ? "Class: <none>" : $"Class: {selected}";
        }
    }
}
