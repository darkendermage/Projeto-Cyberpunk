using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.NetArchitectures;

/// <summary>
/// The "Home Security 2045" price of an Architecture (CP:R p.217-218): floors by band, Passwords/Files/Control Nodes by
/// DV, Black ICE at catalog price doubled or tripled when they share a floor, Demons, and Defenses by the DV to counter
/// them. Civilian devices on a Control Node cost nothing extra.
/// </summary>
public sealed class NetArchitecturePricing(NetArchitectureRuleBook rules)
{
    public ArchitectureQuote Quote(ArchitectureState state, NetIceCatalog ice)
    {
        var p = rules.Document.Pricing;
        var lines = new List<CostLine>();
        var warnings = new List<string>();
        var floors = state.FloorCount;
        var band = rules.Band(floors);

        lines.Add(new("Andares", $"{floors} andares ({(band.Portable ? "servidor portátil" : "não portátil")})", floors, band.CostPerFloor,
            floors * band.CostPerFloor, p.Floors.Source));
        if (floors is < 3 or > 18) warnings.Add(L.T($"Uma Arquitetura comprada tem de 3 a 18 andares ({p.Floors.Source}).", $"A purchased Architecture has 3 to 18 floors ({p.Floors.Source})."));

        foreach (var floor in state.Floors)
        {
            var ices = floor.Contents.Where(c => c.Kind == NetContentKind.BlackIce).ToList();
            if (ices.Count > 3) warnings.Add(L.T($"O andar {floor.Id} tem {ices.Count} Black ICE; o livro só prevê até 3 por andar ({p.IceMultiplier.Source}).", $"Floor {floor.Id} has {ices.Count} Black ICE; the book only covers up to 3 per floor ({p.IceMultiplier.Source})."));
            var multiplier = p.IceMultiplier.Value.ElementAtOrDefault(ices.Count - 1);
            if (multiplier == 0) multiplier = ices.Count;
            foreach (var c in ices)
            {
                var info = ice.Find(c.Slug);
                if (info?.Price is not { } price)
                {
                    warnings.Add(L.T($"{info?.Name ?? c.Slug} não tem preço no catálogo.", $"{info?.Name ?? c.Slug} has no catalog price."));
                    continue;
                }
                var suffix = ices.Count > 1 ? L.T($" (×{multiplier}: {ices.Count} no andar)", $" (×{multiplier}: {ices.Count} on the floor)") : "";
                lines.Add(new("Black ICE", $"{floor.Id}: {info.Name}{suffix}", 1, price * multiplier, price * multiplier, p.IceMultiplier.Source));
            }

            foreach (var c in floor.Contents.Where(c => c.Kind != NetContentKind.BlackIce))
            {
                var label = c.Kind switch { NetContentKind.Password => "Password", NetContentKind.File => "File", _ => "Control Node" };
                if (c.Dv is not { } dv || rules.FeatureCost(dv) is not { } cost)
                {
                    warnings.Add(L.T($"{floor.Id}: {label} sem DV com preço no livro (DV6, 8, 10 ou 12).", $"{floor.Id}: {label} with no DV priced in the book (DV6, 8, 10 or 12)."));
                    continue;
                }
                lines.Add(new("Features", $"{floor.Id}: {label} DV{dv}", 1, cost, cost, p.Features.Source));

                if (c.Function?.DefenseId is { } defenseId)
                {
                    if (rules.FindDefense(defenseId) is not { } defense)
                        warnings.Add(L.T($"{floor.Id}: defesa '{defenseId}' não existe.", $"{floor.Id}: defense '{defenseId}' doesn't exist."));
                    else if (rules.DefenseCost(defense.CounterDv) is { } dcost)
                        lines.Add(new("Defesas", L.T($"{floor.Id}: {defense.Name} (DV{defense.CounterDv} para neutralizar)", $"{floor.Id}: {defense.Name} (DV{defense.CounterDv} to neutralize)"), 1, dcost, dcost, p.Defenses.Source));
                }
            }
        }

        foreach (var demon in state.Demons)
        {
            var rule = rules.Demons.FirstOrDefault(d => d.Id == demon.Type);
            if (rule is null) { warnings.Add(L.T($"Demon '{demon.Type}' não existe.", $"Demon '{demon.Type}' doesn't exist.")); continue; }
            lines.Add(new("Demons", $"{rule.Name} ({demon.Id})", 1, rule.Cost, rule.Cost, rules.Document.Demons.Source));
        }

        var nodes = state.Contents.Count(c => c.Kind == NetContentKind.ControlNode);
        if (band.MaxControlNodes is { } maxNodes && nodes > maxNodes)
            warnings.Add(L.T($"{nodes} Control Nodes passam do limite de {maxNodes} para {floors} andares ({p.Floors.Source}).", $"{nodes} Control Nodes go over the limit of {maxNodes} for {floors} floors ({p.Floors.Source})."));
        var maxDemons = rules.MaxDemons(floors);
        if (state.Demons.Count > maxDemons)
            warnings.Add(L.T($"{state.Demons.Count} Demons passam do limite de {maxDemons} para {floors} andares ({rules.Document.FloorsPerDemon.Source}).", $"{state.Demons.Count} Demons go over the limit of {maxDemons} for {floors} floors ({rules.Document.FloorsPerDemon.Source})."));
        foreach (var c in state.Contents.Where(c => c.Function?.DefenseId is not null))
            if (rules.FindDefense(c.Function!.DefenseId)?.RequiresDemon == true && !state.Demons.Any(d => d.Id == c.Function.DemonId))
                warnings.Add(L.T($"{state.FloorOf(c.Id)?.Id}: {rules.FindDefense(c.Function.DefenseId)!.Name} é uma Active Defense e precisa de um Demon no Control Node ({rules.Document.ActiveNeedsDemon.Source}).", $"{state.FloorOf(c.Id)?.Id}: {rules.FindDefense(c.Function.DefenseId)!.Name} is an Active Defense and needs a Demon on the Control Node ({rules.Document.ActiveNeedsDemon.Source})."));

        return new ArchitectureQuote(lines, lines.Sum(l => l.Total), band.Portable, band.MaxControlNodes, nodes, maxDemons, warnings);
    }
}
