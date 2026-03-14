using System;

namespace DVBARPG.Game.Network
{
    /// <summary>
    /// Состояние завершения забега для экрана результатов. Сбрасывается при входе в Run.
    /// </summary>
    public static class RunResultState
    {
        public static bool IsRunEnded { get; private set; }
        public static bool PlayerDied { get; private set; }
        public static int Kills { get; private set; }

        public static event Action OnRunEnded;

        public static void SetRunEnded(bool playerDied, int kills = 0)
        {
            if (IsRunEnded) return;
            IsRunEnded = true;
            PlayerDied = playerDied;
            Kills = kills;
            OnRunEnded?.Invoke();
        }

        public static void Reset()
        {
            IsRunEnded = false;
            PlayerDied = false;
            Kills = 0;
        }
    }
}
