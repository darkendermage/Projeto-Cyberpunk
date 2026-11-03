using System.Text.Json;
using System.Text.RegularExpressions;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>
/// Improvement Points (CP:R p.408–410): earning them, raising skills and Role Abilities, and taking up a new Role
/// (multiclassing, CP:R p.142). Every spend is checked against the cost table and logged as progression.
/// </summary>
public sealed partial class ProgressionOperations(CharacterRuleBook rules)
{
    public const string SpendAction = "spend-ip";
    public const string RefundAction = "refund-ip";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CharacterSheet AwardIp(CharacterSheet sheet, AwardIpRequest request, CharacterChange change)
    {
        RequireFinalized(sheet);
        if (request.Amount == 0) throw CharacterChange.Refuse("amount", L.T("Informe quantos IP.", "Enter how many IP."));
        if (string.IsNullOrWhiteSpace(request.Reason)) throw CharacterChange.Refuse("reason", L.T("Diga de onde vieram os IP (sessão, missão…).", "Say where the IP came from (session, mission…)."));
        var after = sheet.Progress.Ip + request.Amount;
        if (after < 0) throw CharacterChange.Refuse("amount", L.T("O personagem não tem IP suficientes para essa correção.", "The character doesn't have enough IP for that correction."));
        change.Log(CharacterEventKind.Progression, "award-ip", request.Reason.Trim(), request.Amount,
            sheet.Progress.Ip.ToString(), after.ToString(), manual: true);
        return sheet with
        {
            Progress = sheet.Progress with { Ip = after, IpEarned = sheet.Progress.IpEarned + Math.Max(0, request.Amount) },
        };
    }

    /// <summary>Raises a skill one level: 20 IP × the next level, 40 × for skills marked (x2) (CP:R p.410).</summary>
    public CharacterSheet ImproveSkill(CharacterSheet sheet, ImproveSkillRequest request, CharacterChange change)
    {
        RequireFinalized(sheet);
        var key = SkillKey.Normalize(request.SkillKey);
        var (id, spec) = SkillKey.Parse(key);
        var def = rules.FindSkill(id) ?? throw CharacterChange.Refuse("skillKey", L.T($"Perícia desconhecida: {id}.", $"Unknown skill: {id}."));
        if (def.Specialized && spec is null) throw CharacterChange.Refuse("skillKey", L.T($"{def.Name} precisa de especialização.", $"{def.Name} needs a specialization."));

        var level = sheet.Skills.GetValueOrDefault(key);
        var max = rules.Document.Progression.MaxRank.Value;
        if (level >= max) throw CharacterChange.Refuse("skillKey", L.T($"{def.Name} já está no nível máximo ({max}).", $"{def.Name} is already at the maximum level ({max})."));
        var cost = rules.SkillImprovementCost(def, level + 1);
        sheet = Spend(sheet, cost, $"{def.Name}{(spec is null ? "" : $" ({spec})")} {level} → {level + 1}", change, new ProgressionStep(SkillStep, key, level, level + 1, cost));
        return sheet with { Skills = new Dictionary<string, int>(sheet.Skills, StringComparer.OrdinalIgnoreCase) { [key] = level + 1 } };
    }

    /// <summary>Raises a Role Ability one Rank: 60 IP × the next Rank (CP:R p.410).</summary>
    public CharacterSheet ImproveRole(CharacterSheet sheet, ImproveRoleRequest request, CharacterChange change)
    {
        RequireFinalized(sheet);
        var index = sheet.Roles.FindIndex(r => r.Role.Equals(request.Role, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw CharacterChange.Refuse("role", L.T($"O personagem não tem a Role {request.Role}.", $"The character doesn't have the {request.Role} Role."));
        var role = sheet.Roles[index];
        var max = rules.Document.Progression.MaxRank.Value;
        if (role.Rank >= max) throw CharacterChange.Refuse("role", L.T($"{role.Role} já está no Rank máximo ({max}).", $"{role.Role} is already at the maximum Rank ({max})."));
        var cost = rules.RoleImprovementCost(role.Rank + 1);
        sheet = Spend(sheet, cost, $"{rules.Role(role.Role).Ability.Name} Rank {role.Rank} → {role.Rank + 1}", change, new ProgressionStep(RoleStep, role.Role, role.Rank, role.Rank + 1, cost));
        var roles = sheet.Roles.ToList();
        roles[index] = role with { Rank = role.Rank + 1, IpInvested = role.IpInvested + cost };
        return sheet with { Roles = roles };
    }

    /// <summary>
    /// Takes up a new Role at Rank 1, bought with IP, once the current Role has at least 4 Ranks (CP:R p.142). The new
    /// Role becomes the current one; earlier Roles keep their Ranks and features.
    /// </summary>
    public CharacterSheet AddRole(CharacterSheet sheet, AddRoleRequest request, CharacterChange change)
    {
        RequireFinalized(sheet);
        var info = rules.FindRole(request.Role) ?? throw CharacterChange.Refuse("role", L.T($"Role desconhecida: {request.Role}.", $"Unknown Role: {request.Role}."));
        if (sheet.Roles.Any(r => r.Role.Equals(info.Id, StringComparison.OrdinalIgnoreCase)))
            throw CharacterChange.Refuse("role", L.T($"O personagem já tem a Role {info.Id}.", $"The character already has the {info.Id} Role."));
        var p = rules.Document.Progression;
        if (sheet.PrimaryRole is { } current && current.Rank < p.MulticlassMinRank.Value)
            throw CharacterChange.Refuse("role",
                L.T($"{current.Role} precisa de Rank {p.MulticlassMinRank.Value} antes de trocar de Role ({CharacterRuleBook.Page(p.MulticlassMinRank.Reference)}).", $"{current.Role} needs Rank {p.MulticlassMinRank.Value} before changing Role ({CharacterRuleBook.Page(p.MulticlassMinRank.Reference)})."));

        var rank = p.MulticlassNewRoleRank.Value;
        var cost = rules.RoleImprovementCost(rank);
        sheet = Spend(sheet, cost, $"nova Role {info.Id} ({info.Ability.Name} Rank {rank})", change);
        change.Log(CharacterEventKind.Progression, "multiclass",
            L.T($"Agora é visto como {info.Id}; {sheet.PrimaryRole?.Role} mantém Rank e habilidade ({CharacterRuleBook.Page(p.MulticlassMinRank.Reference)}).", $"Now seen as {info.Id}; {sheet.PrimaryRole?.Role} keeps its Rank and ability ({CharacterRuleBook.Page(p.MulticlassMinRank.Reference)})."));
        return sheet with
        {
            Roles = [new CharacterRole { Role = info.Id, Rank = rank, IpInvested = cost, AcquiredAt = change.Now }, .. sheet.Roles],
        };
    }

    private static CharacterSheet Spend(CharacterSheet sheet, int cost, string what, CharacterChange change, ProgressionStep? step = null)
    {
        if (sheet.Progress.Ip < cost)
            throw CharacterChange.Refuse("ip", L.T($"{what} custa {cost} IP; o personagem tem {sheet.Progress.Ip}.", $"{what} costs {cost} IP; the character has {sheet.Progress.Ip}."));
        change.Log(CharacterEventKind.Progression, SpendAction, $"{what} ({cost} IP).", -cost,
            sheet.Progress.Ip.ToString(), (sheet.Progress.Ip - cost).ToString(), payload: step is null ? null : JsonSerializer.Serialize(step, Json));
        return sheet with { Progress = sheet.Progress with { Ip = sheet.Progress.Ip - cost } };
    }

    // ------------------------------------------------------------ undo

    private const string SkillStep = "skill";
    private const string RoleStep = "role";

    /// <summary>
    /// What an IP spend changed: a skill (key) or a Role (id) going from one level to the next, and its cost. Read from
    /// the entry's payload, or, for entries written before payloads existed, from its description.
    /// </summary>
    public ProgressionStep? StepOf(string action, string? payload, string description)
    {
        if (action != SpendAction) return null;
        if (payload is not null)
        {
            try
            {
                if (JsonSerializer.Deserialize<ProgressionStep>(payload, Json) is { Kind: SkillStep or RoleStep } step) return step;
            }
            catch (JsonException) { /* fall back to the text */ }
        }
        if (RoleRaise().Match(description) is { Success: true } r)
        {
            var role = rules.Roles.FirstOrDefault(x => x.Ability.Name.Equals(r.Groups["name"].Value, StringComparison.OrdinalIgnoreCase));
            return role is null ? null : new ProgressionStep(RoleStep, role.Id, int.Parse(r.Groups["from"].Value), int.Parse(r.Groups["to"].Value), int.Parse(r.Groups["cost"].Value));
        }
        if (SkillRaise().Match(description) is { Success: true } s)
        {
            var def = rules.Skills.FirstOrDefault(x => x.Name.Equals(s.Groups["name"].Value, StringComparison.OrdinalIgnoreCase));
            var spec = s.Groups["spec"].Success ? s.Groups["spec"].Value : null;
            if (def is null || def.Specialized != (spec is not null)) return null;
            return new ProgressionStep(SkillStep, SkillKey.Normalize(SkillKey.Make(def.Id, spec)), int.Parse(s.Groups["from"].Value), int.Parse(s.Groups["to"].Value), int.Parse(s.Groups["cost"].Value));
        }
        return null;
    }

    /// <summary>A step can be undone while it is still the latest one: the skill or Rank is at the level it reached.</summary>
    public bool CanUndo(CharacterSheet sheet, ProgressionStep step) => step.Kind switch
    {
        SkillStep => sheet.Skills.GetValueOrDefault(step.Key) == step.To,
        RoleStep => sheet.Roles.Any(r => r.Role.Equals(step.Key, StringComparison.OrdinalIgnoreCase) && r.Rank == step.To),
        _ => false,
    };

    /// <summary>Puts the skill or Rank back one step and returns its IP; the refund points at the spend it reverses.</summary>
    public CharacterSheet Undo(CharacterSheet sheet, ProgressionStep step, long eventId, string description, CharacterChange change)
    {
        RequireFinalized(sheet);
        if (!CanUndo(sheet, step))
            throw CharacterChange.Refuse("eventId", L.T("Desfaça antes o passo mais recente dessa perícia ou Role.", "Undo the latest step of that skill or Role first."));
        if (step.Kind == SkillStep)
        {
            var skills = new Dictionary<string, int>(sheet.Skills, StringComparer.OrdinalIgnoreCase);
            if (step.From <= 0) skills.Remove(step.Key);
            else skills[step.Key] = step.From;
            sheet = sheet with { Skills = skills };
        }
        else
        {
            var roles = sheet.Roles.Select(r => r.Role.Equals(step.Key, StringComparison.OrdinalIgnoreCase)
                ? r with { Rank = step.From, IpInvested = Math.Max(0, r.IpInvested - step.Cost) } : r).ToList();
            sheet = sheet with { Roles = roles };
        }
        var what = description.TrimEnd('.');
        change.Log(CharacterEventKind.Progression, RefundAction, L.T($"Desfeito: {what}. {step.Cost} IP devolvidos.", $"Undone: {what}. {step.Cost} IP returned."), step.Cost,
            sheet.Progress.Ip.ToString(), (sheet.Progress.Ip + step.Cost).ToString(), payload: JsonSerializer.Serialize(new UndoMark(eventId), Json));
        return sheet with { Progress = sheet.Progress with { Ip = sheet.Progress.Ip + step.Cost } };
    }

    /// <summary>The spend a refund entry reverses.</summary>
    public static long? UndoneId(string payload)
    {
        try { return JsonSerializer.Deserialize<UndoMark>(payload, Json)?.Undoes; }
        catch (JsonException) { return null; }
    }

    private sealed record UndoMark(long Undoes);

    // "Combat Awareness Rank 4 → 5 (300 IP)."
    [GeneratedRegex(@"^(?<name>.+) Rank (?<from>\d+) → (?<to>\d+) \((?<cost>\d+) IP\)\.$")]
    private static partial Regex RoleRaise();

    // "Martial Arts (Gun Fu) 0 → 1 (40 IP)." or "Handgun 6 → 7 (140 IP)."
    [GeneratedRegex(@"^(?<name>.+?)(?: \((?<spec>[^()]+)\))? (?<from>\d+) → (?<to>\d+) \((?<cost>\d+) IP\)\.$")]
    private static partial Regex SkillRaise();

    private static void RequireFinalized(CharacterSheet sheet)
    {
        if (!sheet.Creation.Finalized) throw CharacterChange.Refuse("creation", L.T("Improvement Points são usados depois da criação.", "Improvement Points are used after creation."));
    }
}

/// <summary>What one IP spend changed: a skill (key) or a Role (id) from one level to the next, and what it cost.</summary>
public sealed record ProgressionStep(string Kind, string Key, int From, int To, int Cost);
