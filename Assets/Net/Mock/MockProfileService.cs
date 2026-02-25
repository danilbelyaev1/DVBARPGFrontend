using DVBARPG.Core.Services;

namespace DVBARPG.Net.Mock
{
    public sealed class MockProfileService : IProfileService
    {
        public AuthSession CurrentAuth { get; private set; }
        public string SelectedClassId { get; private set; }

        public void SetAuth(AuthSession session)
        {
            // Сохраняем сессию в памяти (dev-режим).
            CurrentAuth = session;
        }

        public void SetSelectedClass(string classId)
        {
            // Выбранный класс для запуска забега.
            SelectedClassId = classId;
        }
    }
}
