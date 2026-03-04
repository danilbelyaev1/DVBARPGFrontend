using System;
using DVBARPG.Core.Services;

namespace DVBARPG.Net.Mock
{
    public sealed class MockAuthService : IAuthService
    {
        private static readonly Guid DefaultCharacterId = new Guid("11111111-1111-1111-1111-111111111111");
        private static readonly Guid DefaultSeasonId = new Guid("22222222-2222-2222-2222-222222222222");
        private const string DefaultToken = "dev-token";

        public AuthSession Login()
        {
            // Генерируем локальную сессию для dev-режима.
            return new AuthSession
            {
                PlayerId = Guid.NewGuid().ToString("N"),
                Token = DefaultToken,
                CharacterId = DefaultCharacterId,
                SeasonId = DefaultSeasonId
            };
        }
    }
}
