using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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
        public int Seq { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public string? SkillId { get; set; }
        public string? Token { get; set; }
        public System.Guid? CharacterId { get; set; }
        public System.Guid? SeasonId { get; set; }
        public string? MapId { get; set; }
    }

    public sealed class HelloEnvelope
    {
        public string Type { get; set; } = "hello";
        public System.Guid SessionId { get; set; }
        public int TickRate { get; set; }
    }

    public sealed class ConnectOkEnvelope
    {
        public string Type { get; set; } = "connect_ok";
        public System.Guid CharacterId { get; set; }
        public System.Guid SeasonId { get; set; }
        public string PlayerName { get; set; } = "";
    }

    public sealed class InstanceStartEnvelope
    {
        public string Type { get; set; } = "instance_start";
        public System.Guid InstanceId { get; set; }
        public int Seed { get; set; }
        public string MapId { get; set; } = "";
    }

    public sealed class ErrorEnvelope
    {
        public string Type { get; set; } = "error";
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public sealed class SnapshotEnvelope
    {
        public string Type { get; set; } = "snapshot";
        public long ServerTimeMs { get; set; }
        public int AckSeq { get; set; }
        public PlayerSnapshot Player { get; set; } = new();
        public MonsterSnapshot[] Monsters { get; set; } = System.Array.Empty<MonsterSnapshot>();
        public ProjectileSnapshot[] Projectiles { get; set; } = System.Array.Empty<ProjectileSnapshot>();
    }

    public sealed class PlayerSnapshot
    {
        public System.Guid Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
    }

    public sealed class MonsterSnapshot
    {
        public System.Guid Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
    }

    public sealed class ProjectileSnapshot
    {
        public System.Guid Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; }
    }
}
