using System.Text.Json.Serialization;

namespace CyberpunkDb.Application.Encounters;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PendingStep { Defense, CriticalInjury }

/// <summary>
/// An attack waiting on one step. <see cref="Request"/> holds every die fixed so far (attack, extra, defenses, damage),
/// so the attack resolves the same way when it is finally applied; <see cref="Decided"/> lists the targets whose
/// dodge choice is made.
/// </summary>
public sealed record PendingAttack(int Seq, AttackRequest Request, string TargetId, PendingStep Step, List<string> Decided);

/// <summary>
/// A pending attack as the screens show it: who shoots whom with what, the step, and what the target weighs (the range
/// DV against their Evasion). The attack total stays out until the attack resolves.
/// </summary>
public sealed record PendingView(
    int Seq, string AttackerId, string AttackerName, string Weapon, string TargetId, string TargetName, PendingStep Step,
    int? RangeDv, int? Evasion, int? AttackerCharacterId, int? TargetCharacterId);
