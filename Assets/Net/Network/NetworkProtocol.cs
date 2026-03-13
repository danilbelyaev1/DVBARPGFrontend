using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace DVBARPG.Net.Network
{
    public static class NetProtocol
    {
        public static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    public sealed class BaseEnvelope
    {
        public string Type { get; set; } = "";
    }

    public sealed class CommandEnvelope
    {
        public string Type { get; set; } = "";
        public System.Guid? SessionId { get; set; }
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
        public int Seq { get; set; }
        public long? ClientTimeMs { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public string SkillId { get; set; }
        public string Slot { get; set; }
        public bool? Enabled { get; set; }
        public string Token { get; set; }
        public System.Guid? CharacterId { get; set; }
        public System.Guid? SeasonId { get; set; }
        public string MapId { get; set; }
        public Dictionary<string, float> StatPatch { get; set; }
        public List<SkillInstance> Skills { get; set; }
        public CombatLoadout CombatLoadout { get; set; }
        public bool? ReplaceSkills { get; set; }
        /// <summary>Индекс дропа для команды pickup (подбор лута).</summary>
        public int? DropIndex { get; set; }
    }

    public sealed class SkillInstance
    {
        public string SkillId { get; set; }
        public int Level { get; set; }
        public JToken Modifiers { get; set; }
    }

    public sealed class CombatLoadout
    {
        public string AttackSkillId { get; set; }
        public string SupportASkillId { get; set; }
        public string SupportBSkillId { get; set; }
        public bool? AttackEnabled { get; set; }
        public bool? SupportAEnabled { get; set; }
        public bool? SupportBEnabled { get; set; }
        public string MovementSlot { get; set; }
    }

    public sealed class HelloEnvelope
    {
        public string Type { get; set; } = "hello";
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
        public System.Guid SessionId { get; set; }
        public int TickRate { get; set; }
    }

    public sealed class ConnectOkEnvelope
    {
        public string Type { get; set; } = "connect_ok";
        public System.Guid? SessionId { get; set; }
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
        public System.Guid CharacterId { get; set; }
        public System.Guid SeasonId { get; set; }
        public string PlayerName { get; set; } = "";
    }

    public sealed class InstanceStartEnvelope
    {
        public string Type { get; set; } = "instance_start";
        public System.Guid? SessionId { get; set; }
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
        public System.Guid InstanceId { get; set; }
        public int Seed { get; set; }
        public string MapId { get; set; } = "";
    }

    public sealed class ErrorEnvelope
    {
        public string Type { get; set; } = "error";
        public System.Guid? SessionId { get; set; }
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public sealed class SnapshotEnvelope
    {
        public string Type { get; set; } = "snapshot";
        public System.Guid? SessionId { get; set; }
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
        public long ServerTimeMs { get; set; }
        public int AckSeq { get; set; }
        public System.Collections.Generic.Dictionary<string, float> Cooldowns { get; set; }
        /// <summary>Сколько XP заработано в этом ране (с начала инстанса).</summary>
        public int RunXpTotal { get; set; }
        /// <summary>Сколько мобов убито в этом инстансе.</summary>
        public int RunKills { get; set; }
        /// <summary>Игрок мёртв — открыто окно лута до LootWindowEndsAtUtc.</summary>
        public bool Paused { get; set; }
        /// <summary>Момент окончания окна подбора лута (UTC).</summary>
        public System.DateTime? LootWindowEndsAtUtc { get; set; }
        /// <summary>Дропы на земле (золото/предметы). Уже подобранные исключены по PickedIndices.</summary>
        public LootDropSnapshot[] LootDrops { get; set; } = System.Array.Empty<LootDropSnapshot>();
        /// <summary>Индексы дропов, которые игрок уже подобрал.</summary>
        public int[] PickedIndices { get; set; } = System.Array.Empty<int>();
        public PlayerSnapshot Player { get; set; } = new();
        public MonsterSnapshot[] Monsters { get; set; } = System.Array.Empty<MonsterSnapshot>();
        public ProjectileSnapshot[] Projectiles { get; set; } = System.Array.Empty<ProjectileSnapshot>();
    }

    /// <summary>Один дроп в снапшоте: золото или предмет (Type = "gold" | "item").</summary>
    public sealed class LootDropSnapshot
    {
        public int Index { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public string Type { get; set; } = "";
        public int GoldAmount { get; set; }
        public int ItemDefinitionId { get; set; }
        public int ItemLevel { get; set; }
        public string Rarity { get; set; } = "common";
    }

    public sealed class NetworkStatsEnvelope
    {
        public string Type { get; set; } = "net_stats";
        public System.Guid? SessionId { get; set; }
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
        public int TicksLastSec { get; set; }
        public int SentSnapshotsLastSec { get; set; }
        public float PacketLossPct { get; set; }
        public float AvgPingMs { get; set; }
    }

    public sealed class PlayerSnapshot
    {
        public System.Guid Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public bool AttackEnabled { get; set; }
        public bool SupportAEnabled { get; set; }
        public bool SupportBEnabled { get; set; }
        public bool MovementActive { get; set; }
        public string MovementSkillId { get; set; }
        public bool AttackAnimTriggered { get; set; }
    }

    public sealed class MonsterSnapshot
    {
        public System.Guid Id { get; set; }
        public string Type { get; set; } = "";
        public string State { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
    }

    public sealed class ProjectileSnapshot
    {
        public System.Guid Id { get; set; }
        public System.Guid OwnerId { get; set; }
        public long SpawnTimeMs { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; }
    }

    public sealed class AckEnvelope
    {
        public string Type { get; set; } = "ack";
        public System.Guid? SessionId { get; set; }
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
    }

    public sealed class UdpEnvelopeBase
    {
        public string Type { get; set; } = "";
        public System.Guid? SessionId { get; set; }
        public int PacketSeq { get; set; }
        public int Ack { get; set; }
        public bool Reliable { get; set; }
    }
}
