using System;
using UnityEngine;

namespace DVBARPG.Game.Skills.Presentation
{
    [Serializable]
    public sealed class SkillVfxBinding
    {
        [Header("Событие")]
        [Tooltip("Когда спавнить VFX.")]
        [SerializeField] private SkillVfxEventType eventType = SkillVfxEventType.CastStart;

        [Header("Префаб")]
        [Tooltip("Префаб VFX для создания.")]
        [SerializeField] private Transform prefab;

        [Header("Сокет")]
        [Tooltip("Имя сокета, к которому будет привязан VFX.")]
        [SerializeField] private string socketName = "";
        [Tooltip("Привязать объект к сокету (локальные координаты).")]
        [SerializeField] private bool attachToSocket = true;
        [Tooltip("Использовать поворот сокета (если не привязан, применяется к миру).")]
        [SerializeField] private bool useSocketRotation = true;

        [Header("Трансформ")]
        [Tooltip("Локальное смещение относительно сокета.")]
        [SerializeField] private Vector3 localOffset = Vector3.zero;
        [Tooltip("Локальные углы (в градусах) относительно сокета.")]
        [SerializeField] private Vector3 localEuler = Vector3.zero;
        [Tooltip("Локальный масштаб объекта.")]
        [SerializeField] private Vector3 localScale = Vector3.one;

        [Header("Время жизни")]
        [Tooltip("Секунд до авто-удаления. 0 = не удалять.")]
        [SerializeField] private float lifetimeSec = 0f;

        public SkillVfxEventType EventType => eventType;
        public Transform Prefab => prefab;
        public string SocketName => socketName;
        public bool AttachToSocket => attachToSocket;
        public bool UseSocketRotation => useSocketRotation;
        public Vector3 LocalOffset => localOffset;
        public Vector3 LocalEuler => localEuler;
        public Vector3 LocalScale => localScale;
        public float LifetimeSec => lifetimeSec;
    }
}
