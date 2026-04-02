using UnityEngine;

namespace DVBARPG.Core
{
    /// <summary>
    /// Глобальная настройка логгера клиента.
    /// </summary>
    public static class ClientLogSilencer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureClientLogLevel()
        {
            Debug.unityLogger.logEnabled = true;
            // Включаем полный поток логов для диагностики.
            Debug.unityLogger.filterLogType = LogType.Log;
        }
    }
}
