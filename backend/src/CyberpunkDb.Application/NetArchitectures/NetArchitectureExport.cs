using System.Globalization;
using System.Text;
using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.NetArchitectures;

/// <summary>
/// The Architecture as the GM writes it down: one table per branch in the Core's layout
/// (<c>Floor | On the Other Side of the "Door" | DV</c>, CP:R p.218), then Demons, Watchers and the cost.
/// </summary>
public sealed class NetArchitectureExport(NetArchitectureRuleBook rules)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public string Render(string name, ArchitectureState state, ArchitectureQuote quote, NetIceCatalog ice, bool markdown)
    {
        var difficulty = rules.Difficulty(state.Difficulty);
        var location = rules.Location(state.LocationId);
        var sb = new StringBuilder();

        sb.AppendLine(markdown ? $"# {name}" : name.ToUpperInvariant());
        sb.AppendLine(L.T($"Dificuldade {difficulty.Label} (DV{difficulty.Dv}) · {state.FloorCount} andares · {location.Label} · seed {state.Seed}", $"Difficulty {L.D(difficulty.Label)} (DV{difficulty.Dv}) · {state.FloorCount} floors · {L.D(location.Label)} · seed {state.Seed}"));
        sb.AppendLine();

        foreach (var branch in state.Branches)
        {
            var title = branch.FromFloor is { } from
                ? L.T($"Ramo {branch.Id} (sai depois do andar {from} do tronco)", $"Branch {branch.Id} (leaves after trunk floor {from})")
                : L.T($"Tronco {branch.Id} (Lobby nos andares 1 e 2; o andar {branch.Floors[^1].Depth} é o fundo)", $"Trunk {branch.Id} (Lobby on floors 1 and 2; floor {branch.Floors[^1].Depth} is the bottom)");
            sb.AppendLine(markdown ? $"## {title}" : title);
            var rows = branch.Floors.Select(f => (Floor: f.Depth.ToString(), Door: Door(f, state, ice), Dv: Dv(f))).ToList();
            Table(sb, ["Floor", "On the Other Side of the \"Door\"", "DV"], rows.Select(r => new[] { r.Floor, r.Door, r.Dv }).ToList(), markdown);
            sb.AppendLine();
        }

        if (state.Demons.Count > 0)
        {
            sb.AppendLine(markdown ? "## Demons" : "DEMONS");
            foreach (var d in state.Demons)
            {
                var rule = rules.Demons.FirstOrDefault(x => x.Id == d.Type);
                var nodes = state.Contents.Where(c => c.Function?.DemonId == d.Id).Select(c => state.FloorOf(c.Id)?.Id).ToList();
                sb.AppendLine(L.T($"{(markdown ? "- " : "")}{rule?.Name ?? d.Type} ({d.Id}, andar {d.FloorId}): REZ {rule?.Rez} · Interface {rule?.Interface} · {rule?.NetActions} NET Actions · Combat Number {rule?.CombatNumber}", $"{(markdown ? "- " : "")}{rule?.Name ?? d.Type} ({d.Id}, floor {d.FloorId}): REZ {rule?.Rez} · Interface {rule?.Interface} · {rule?.NetActions} NET Actions · Combat Number {rule?.CombatNumber}")
                              + (nodes.Count > 0 ? L.T($" · opera os Control Nodes de {string.Join(", ", nodes)}", $" · operates the Control Nodes on {string.Join(", ", nodes)}") : ""));
            }
            sb.AppendLine();
        }

        var watchers = Watchers(state);
        if (watchers.Count > 0)
        {
            sb.AppendLine(markdown ? "## Watchers (Going Quiet)" : "WATCHERS (GOING QUIET)");
            foreach (var w in watchers)
                sb.AppendLine(L.T($"{(markdown ? "- " : "")}{w.Name} · {w.Kind} · andar {w.FloorId} · Interface {w.Interface}{(w.PathfinderBonus > 0 ? $" · Pathfinder +{w.PathfinderBonus}" : "")}", $"{(markdown ? "- " : "")}{w.Name} · {w.Kind} · floor {w.FloorId} · Interface {w.Interface}{(w.PathfinderBonus > 0 ? $" · Pathfinder +{w.PathfinderBonus}" : "")}"));
            sb.AppendLine();
        }

        sb.AppendLine(markdown ? "## Custo (CP:R p.217-218)" : "CUSTO (CP:R p.217-218)");
        sb.AppendLine($"{(markdown ? "**Total:** " : "Total: ")}{Eb(quote.Total)} · {(quote.Portable ? "servidor portátil" : "servidor não portátil")}");
        foreach (var w in quote.Warnings) sb.AppendLine($"{(markdown ? "- " : "! ")}{w}");
        if (!string.IsNullOrWhiteSpace(state.Notes))
        {
            sb.AppendLine();
            sb.AppendLine(markdown ? "## Notas" : "NOTAS");
            sb.AppendLine(state.Notes.Trim());
        }
        return sb.ToString();
    }

    /// <summary>Every Watcher of the Architecture: its Demons and the enemy Netrunners jacked in (Going Quiet p.4).</summary>
    public IReadOnlyList<WatcherDto> Watchers(ArchitectureState state) =>
        state.Demons.Select(d =>
            {
                var rule = rules.Demons.FirstOrDefault(x => x.Id == d.Type);
                return new WatcherDto(d.Id, "Demon", $"{rule?.Name ?? d.Type} ({d.Id})", rule?.Interface ?? 0, 0, d.FloorId);
            })
            .Concat(state.Netrunners.Select(n => new WatcherDto(n.Id, "Netrunner", n.Name, n.Interface, n.PathfinderBonus, n.FloorId)))
            .ToList();

    private string Door(NetFloor floor, ArchitectureState state, NetIceCatalog ice)
    {
        var parts = floor.Contents.Select(c => c.Kind switch
        {
            NetContentKind.BlackIce => ice.Find(c.Slug)?.Name ?? c.Slug ?? "Black ICE",
            NetContentKind.Password => "Password",
            NetContentKind.File => c.FileIdea is { } idea ? $"File ({idea})" : "File",
            _ => $"Control Node ({NodeLabel(c)})",
        });
        var text = string.Join(", ", parts);
        if (floor.Table.Length > 0 && floor.Table != NetArchitectureRuleBook.LobbyTable && floor.Table != state.Difficulty.ToString())
            text += " [Boss Floor]";
        var watchers = Watchers(state).Where(w => w.FloorId == floor.Id).Select(w => w.Name).ToList();
        if (watchers.Count > 0) text += L.T($" · aqui: {string.Join(", ", watchers)}", $" · here: {string.Join(", ", watchers)}");
        return text.Length > 0 ? text : "(vazio)";
    }

    private string NodeLabel(NetContent c)
    {
        var f = c.Function;
        if (f is null) return L.T("sem função", "no function");
        if (rules.FindDefense(f.DefenseId) is { } d)
            return d.Name + (f.Loadout.Count > 0 ? $": {string.Join(" + ", f.Loadout)}" : "") + (f.Label is { } extra ? $" — {extra}" : "");
        if (rules.FindDevice(f.DeviceId) is { } device) return f.Label ?? device.Label;
        return f.Label ?? L.T("sem função", "no function");
    }

    /// <summary>"N/A" for Black ICE; the Interface Ability and DV otherwise, as in CP:R p.218 ("Backdoor DV8").</summary>
    private static string Dv(NetFloor floor)
    {
        var dvs = floor.Contents.Where(c => c.Dv is not null).Select(c => c.Kind switch
        {
            NetContentKind.Password => $"(Backdoor DV{c.Dv})",
            NetContentKind.File => $"(Eye-Dee DV{c.Dv})",
            _ => $"(Control DV{c.Dv})",
        }).ToList();
        return dvs.Count > 0 ? string.Join(" ", dvs) : "N/A";
    }

    private static void Table(StringBuilder sb, string[] header, List<string[]> rows, bool markdown)
    {
        if (markdown)
        {
            sb.AppendLine($"| {string.Join(" | ", header)} |");
            sb.AppendLine($"|{string.Join("|", header.Select(_ => "---"))}|");
            foreach (var r in rows) sb.AppendLine($"| {string.Join(" | ", r.Select(c => c.Replace("|", "/")))} |");
            return;
        }
        var widths = header.Select((h, i) => Math.Max(h.Length, rows.Count == 0 ? 0 : rows.Max(r => r[i].Length))).ToArray();
        string Line(string[] cells) => string.Join(" | ", cells.Select((c, i) => i == cells.Length - 1 ? c : c.PadRight(widths[i]))).TrimEnd();
        sb.AppendLine(Line(header));
        sb.AppendLine(string.Join("-+-", widths.Select((w, i) => new string('-', i == widths.Length - 1 ? Math.Max(w, 2) : w))));
        foreach (var r in rows) sb.AppendLine(Line(r));
    }

    private static string Eb(int value) => value.ToString("N0", PtBr) + "eb";
}
