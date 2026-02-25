using System;

namespace DVBARPG.Core.Services
{
    [Serializable]
    public sealed class AuthSession
    {
        // Идентификатор игрока (локальный, для UI/логики клиента).
        public string PlayerId;
        // Токен авторизации для runtime сервера.
        public string Token;
        // Идентификатор персонажа на сервере.
        public Guid CharacterId;
        // Идентификатор сезона (лиги) на сервере.
        public Guid SeasonId;
    }
}
