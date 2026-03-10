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
        public RuntimeSkillSnapshot[] ServerSkills { get; private set; } = System.Array.Empty<RuntimeSkillSnapshot>();

        public void SetAuth(AuthSession session)
        {
            // Сохраняем сессию в памяти (dev-режим).
            CurrentAuth = session;
            UnityEngine.Debug.Log($"[DevDebug] SetAuth: tokenLen={session?.Token?.Length ?? 0} characterId={session?.CharacterId} seasonId={session?.SeasonId}");
        }

        public void SetSelectedClass(string classId)
        {
            // Выбранный класс для запуска забега.
            SelectedClassId = classId;
        }

        public void SetSelectedCharacter(string characterId)
        {
            SelectedCharacterId = characterId;
            UnityEngine.Debug.Log($"[DevDebug] SetSelectedCharacter: {characterId}");
        }

        public void SetCurrentSeason(string seasonId)
        {
            CurrentSeasonId = seasonId;
            UnityEngine.Debug.Log($"[DevDebug] SetCurrentSeason: {seasonId}");
        }

        public void SetCharacters(RuntimeCharacterSummary[] characters)
        {
            Characters = characters ?? System.Array.Empty<RuntimeCharacterSummary>();
            UnityEngine.Debug.Log($"[DevDebug] SetCharacters: count={Characters.Length}");
        }

        public void SetServerLoadout(RuntimeLoadout loadout)
        {
            ServerLoadout = loadout;
            UnityEngine.Debug.Log($"[DevDebug] SetServerLoadout: attack={loadout?.AttackSkillId} " +
                                  $"supportA={loadout?.SupportASkillId} supportB={loadout?.SupportBSkillId} " +
                                  $"movement={loadout?.MovementSkillId}");
        }

        public void SetBaseMoveSpeed(float moveSpeed)
        {
            BaseMoveSpeed = moveSpeed;
            UnityEngine.Debug.Log($"[DevDebug] SetBaseMoveSpeed: {moveSpeed}");
        }

        public void SetServerSkills(RuntimeSkillSnapshot[] skills)
        {
            ServerSkills = skills ?? System.Array.Empty<RuntimeSkillSnapshot>();
            UnityEngine.Debug.Log($"[DevDebug] SetServerSkills: count={ServerSkills.Length}");
        }
    }
}
