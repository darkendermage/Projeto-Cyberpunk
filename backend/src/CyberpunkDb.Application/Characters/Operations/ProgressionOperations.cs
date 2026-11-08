using System.Text.Json;
using System.Text.RegularExpressions;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Operations;

// Improvement Points (CP:R p.408-410) and multiclassing (p.142). Every spend is logged.
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

    // 20 IP x next level, 40 x for (x2) skills (CP:R p.410)
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

    // 60 IP x next Rank (CP:R p.410)
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

    // Tech: each Rank into two different specialties, max Rank in each (CP:R p.147).
    // Solo: points = Rank, Fumble Recovery is 0 or 4 (p.146).
    public CharacterSheet SetRoleSplit(CharacterSheet sheet, RoleSplitRequest request, CharacterChange change)
    {
        var index = sheet.Roles.FindIndex(r => r.Role.Equals(request.Role, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw CharacterChange.Refuse("role", L.T($"O personagem não tem a Role {request.Role}.", $"The character doesn't have the {request.Role} Role."));
        var role = sheet.Roles[index];
        var rule = rules.FindRole(role.Role)?.Ability.Split
                   ?? throw CharacterChange.Refuse("role", L.T($"A habilidade de {role.Role} não se divide.", $"{role.Role}'s ability isn't divided."));
        var keys = rule.Keys.Select(k => k.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var split = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in request.Split ?? [])
        {
            if (!keys.Contains(key)) throw CharacterChange.Refuse("split", L.T($"Parte desconhecida: {key}.", $"Unknown part: {key}."));
            if (value < 0) throw CharacterChange.Refuse("split", L.T("Os pontos não podem ser negativos.", "Points can't be negative."));
            if (rule.PerKeyMax == "rank" && value > role.Rank)
                throw CharacterChange.Refuse("split", L.T($"{key} vai até o Rank ({role.Rank}) ({rule.Source}).", $"{key} goes up to the Rank ({role.Rank}) ({rule.Source})."));
            var allowed = rule.Allowed.FirstOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
            if (allowed is not null && !allowed.Contains(value))
                throw CharacterChange.Refuse("split", L.T($"{key} aceita só {string.Join(" ou ", allowed)} ({rule.Source}).", $"{key} only takes {string.Join(" or ", allowed)} ({rule.Source})."));
            if (value > 0) split[rule.Keys.First(k => k.Id.Equals(key, StringComparison.OrdinalIgnoreCase)).Id] = value;
        }
        var total = rule.TotalFor(role.Rank);
        if (split.Values.Sum() > total)
            throw CharacterChange.Refuse("split", L.T($"São {total} pontos para dividir no Rank {role.Rank} ({rule.Source}).", $"There are {total} points to divide at Rank {role.Rank} ({rule.Source})."));
        change.Log(CharacterEventKind.Progression, "role-split", L.T($"{rules.Role(role.Role).Ability.Name}: {Describe(split)}.", $"{rules.Role(role.Role).Ability.Name}: {Describe(split)}."), null, null, null);
        var roles = sheet.Roles.ToList();
        roles[index] = role with { Split = split };
        return sheet with { Roles = roles };
    }

    private static string Describe(Dictionary<string, int> split) =>
        split.Count == 0 ? "—" : string.Join(", ", split.Select(kv => $"{kv.Key} {kv.Value}"));

    // Needs 4+ Ranks in the current Role (CP:R p.142). The new one becomes current; old ones keep their Ranks.
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

    // undo

    private const string SkillStep = "skill";
    private const string RoleStep = "role";

    // Read from the payload, or from the description for old entries written before payloads existed.
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

    // only while it's still the latest step
    public bool CanUndo(CharacterSheet sheet, ProgressionStep step) => step.Kind switch
    {
        SkillStep => sheet.Skills.GetValueOrDefault(step.Key) == step.To,
        RoleStep => sheet.Roles.Any(r => r.Role.Equals(step.Key, StringComparison.OrdinalIgnoreCase) && r.Rank == step.To),
        _ => false,
    };

    // the refund points at the spend it reverses
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

public sealed record ProgressionStep(string Kind, string Key, int From, int To, int Cost);
