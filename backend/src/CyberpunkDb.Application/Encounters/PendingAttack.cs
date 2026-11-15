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
    int? RangeDv, int? Evasion, int? AttackerCharacterId, int? TargetCharacterId,
    // already inside Evasion (house rule, -1 per dodge this Round)
    int DodgePenalty = 0,
    // REF 8+ can dodge; a Kendo fighter may Cut the Bullet (its Resolution base) even without it
    bool CanDodge = true, int? CutTheBullet = null);
