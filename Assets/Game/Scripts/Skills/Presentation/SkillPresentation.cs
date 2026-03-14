using System.Collections.Generic;
using UnityEngine;

namespace DVBARPG.Game.Skills.Presentation
{
    [CreateAssetMenu(menuName = "DVBARPG/Skills/Skill Presentation", fileName = "SkillPresentation_")]
    public sealed class SkillPresentation : ScriptableObject
    {
        [Header("SkillId")]
        [Tooltip("SkillId из сервера/каталога.")]
        [SerializeField] private string skillId = "";

        [Header("Анимация")]
        [Tooltip("Trigger в Animator для запуска анимации. Если пусто — используется маппинг в PlayerAbilityAnimationDriver.")]
        [SerializeField] private string animationTrigger = "";

        [Header("VFX")]
        [Tooltip("Список VFX по событиям.")]
        [SerializeField] private List<SkillVfxBinding> vfx = new();

        public string SkillId => skillId;
        public string AnimationTrigger => animationTrigger;
        public IReadOnlyList<SkillVfxBinding> Vfx => vfx;

        public void Initialize(string id)
        {
            skillId = id ?? "";
        }
    }
}
