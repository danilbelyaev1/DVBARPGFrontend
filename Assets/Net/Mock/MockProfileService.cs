using DVBARPG.Core.Services;

namespace DVBARPG.Net.Mock
{
    public sealed class MockProfileService : IProfileService
    {
        public AuthSession CurrentAuth { get; private set; }
        public string SelectedClassId { get; private set; }
        public string SelectedCharacterId { get; private set; }
        public string CurrentSeasonId { get; private set; }
        public RuntimeCharacterSummary[] Characters { get; private set; } = System.Array.Empty<RuntimeCharacterSummary>();
        public RuntimeLoadout ServerLoadout { get; private set; }
        public float BaseMoveSpeed { get; private set; }

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

        public void SetSelectedCharacter(string characterId)
        {
            SelectedCharacterId = characterId;
        }

        public void SetCurrentSeason(string seasonId)
        {
            CurrentSeasonId = seasonId;
        }

        public void SetCharacters(RuntimeCharacterSummary[] characters)
        {
            Characters = characters ?? System.Array.Empty<RuntimeCharacterSummary>();
        }

        public void SetServerLoadout(RuntimeLoadout loadout)
        {
            ServerLoadout = loadout;
        }

        public void SetBaseMoveSpeed(float moveSpeed)
        {
            BaseMoveSpeed = moveSpeed;
        }
    }
}
