using System;

namespace DVBARPG.Core.Services
{
    [Serializable]
    public sealed class AuthSession
    {
        public string PlayerId;
        public string Token;
    }
}
