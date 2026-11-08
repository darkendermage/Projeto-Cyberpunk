using System.Text.Json.Serialization;

namespace CyberpunkDb.Application.Encounters;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PendingStep { Defense, CriticalInjury }

// Request holds every die fixed so far, so it resolves the same way when applied. Decided = targets that
// already chose about dodging.
public sealed record PendingAttack(int Seq, AttackRequest Request, string TargetId, PendingStep Step, List<string> Decided);

// The attack total stays hidden until it resolves.
public sealed record PendingView(
    int Seq, string AttackerId, string AttackerName, string Weapon, string TargetId, string TargetName, PendingStep Step,
    int? RangeDv, int? Evasion, int? AttackerCharacterId, int? TargetCharacterId);
