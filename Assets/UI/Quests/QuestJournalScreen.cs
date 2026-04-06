using DVBARPG.Core;
using DVBARPG.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Quests
{
    public sealed class QuestJournalScreen : MonoBehaviour
    {
        [SerializeField] private Text mainQuestsText;
        [SerializeField] private Text sideQuestsText;

        private void OnEnable()
        {
            Render();
        }

        private void Render()
        {
            var state = GameRoot.Instance?.Services?.Get<CampaignState>();
            if (state == null || state.Quests == null)
            {
                Set(mainQuestsText, "No quests.");
                Set(sideQuestsText, "No quests.");
                return;
            }

            var main = "";
            var side = "";
            foreach (var quest in state.Quests)
            {
                if (quest == null) continue;
                var line = $"- {quest.Title} [{quest.Status}] {quest.ShortObjective}\n";
                if (string.Equals(quest.Category, "main", System.StringComparison.OrdinalIgnoreCase))
                {
                    main += line;
                }
                else
                {
                    side += line;
                }
            }

            Set(mainQuestsText, string.IsNullOrWhiteSpace(main) ? "No main quests." : main);
            Set(sideQuestsText, string.IsNullOrWhiteSpace(side) ? "No side quests." : side);
        }

        private static void Set(Text label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
