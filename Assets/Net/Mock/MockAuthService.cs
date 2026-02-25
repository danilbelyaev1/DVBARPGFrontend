using System;
using DVBARPG.Core.Services;

namespace DVBARPG.Net.Mock
{
    public sealed class MockAuthService : IAuthService
    {
        public AuthSession Login()
        {
            return new AuthSession
            {
                PlayerId = Guid.NewGuid().ToString("N"),
                Token = Guid.NewGuid().ToString("N")
            };
        }
    }
}
