using System;

namespace DVBARPG.UI.Common
{
    /// <summary>
    /// Координация HUD-окон:
    /// - левые окна взаимоисключающие.
    /// </summary>
    public static class HudWindowCoordinator
    {
        public static event Action<string> LeftWindowOpened;

        public static void NotifyLeftWindowOpened(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            LeftWindowOpened?.Invoke(sourceId);
        }
    }
}
