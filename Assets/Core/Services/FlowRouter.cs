using DVBARPG.Core;
using UnityEngine.SceneManagement;

namespace DVBARPG.Core.Services
{
    public enum FlowRoute
    {
        Login,
        CharacterSelect,
        Hub,
        RunLoading,
        Run
    }

    public sealed class FlowRouter
    {
        public void GoTo(FlowRoute route)
        {
            SceneManager.LoadScene(GetSceneName(route));
        }

        public string GetSceneName(FlowRoute route)
        {
            switch (route)
            {
                case FlowRoute.Login: return "Login";
                case FlowRoute.CharacterSelect: return "CharacterSelect";
                case FlowRoute.Hub:
                    if (GameRoot.Instance?.Services != null)
                    {
                        var reg = GameRoot.Instance.Services;
                        if (reg.TryGet<SessionState>(out var session))
                        {
                            reg.TryGet<CampaignState>(out var campaign);
                            return ActHubResolver.ResolveHubSceneName(session, campaign);
                        }
                    }
                    return ActHubResolver.GetHubSceneName(1);
                case FlowRoute.RunLoading: return "RunLoading";
                case FlowRoute.Run: return "Run";
                default: return "Login";
            }
        }
    }
}
