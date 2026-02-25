using DVBARPG.Core.Services;

namespace DVBARPG.Net.Mock
{
    public sealed class MockProfileService : IProfileService
    {
        public AuthSession CurrentAuth { get; private set; }
        public string SelectedClassId { get; private set; }

        public void SetAuth(AuthSession session)
        {
            CurrentAuth = session;
        }

        public void SetSelectedClass(string classId)
        {
            SelectedClassId = classId;
        }
    }
}
