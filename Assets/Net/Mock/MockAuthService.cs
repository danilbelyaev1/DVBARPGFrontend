using System;
using DVBARPG.Core.Services;

namespace DVBARPG.Net.Mock
{
    public sealed class MockAuthService : IAuthService
    {
        public AuthSession Login()
        {
            // Генерируем локальную сессию для dev-режима.
            return new AuthSession
            {
                PlayerId = Guid.NewGuid().ToString("N"),
                Token = Guid.NewGuid().ToString("N"),
                CharacterId = Guid.NewGuid(),
                SeasonId = Guid.NewGuid()
            };
        }
    }
}
