using System;

namespace DVBARPG.UI.Common
{
    public enum HudWindowGroup
    {
        Left,
        Right
    }

    /// <summary>
    /// Координация HUD-окон по группам (left/right).
    /// </summary>
    public static class HudWindowCoordinator
    {
        public static event Action<HudWindowGroup, string> WindowOpened;
        public static event Action<string> LeftWindowOpened;

        public static void NotifyLeftWindowOpened(string sourceId)
        {
            NotifyWindowOpened(HudWindowGroup.Left, sourceId);
        }

        public static void NotifyWindowOpened(HudWindowGroup group, string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            WindowOpened?.Invoke(group, sourceId);
            if (group == HudWindowGroup.Left)
            {
                LeftWindowOpened?.Invoke(sourceId);
            }
        }
    }
}
