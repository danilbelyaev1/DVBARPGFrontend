using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Dev
{
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private Text contentText;

        private void Update()
        {
            var state = GameRoot.Instance?.Services?.Get<SessionState>();
            if (contentText == null || state == null) return;
            contentText.text =
                $"seasonId: {state.SeasonId}\n" +
                $"characterId: {state.CharacterId}\n" +
                $"mapId: {state.MapId}\n" +
                $"last API error: {state.LastApiError}";
        }
    }
}
