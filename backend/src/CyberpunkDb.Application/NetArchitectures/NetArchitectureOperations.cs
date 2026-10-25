using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NetArchitectures.Generation;
using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NetArchitectures;

/// <summary>
/// The GM's edits to a saved Architecture. Each one is checked against the rules here, so the page never needs to
/// know them: Boss Floors come from one Difficulty Rating up, swapped Programs keep the same cost (CP:R p.211), Active
/// Defenses keep a Demon and Demons stay within one per six floors (CP:R p.213, p.218).
/// </summary>
public sealed class NetArchitectureOperations(NetArchitectureRuleBook rules, NetArchitectureGenerator generator)
{
    public void RerollFloor(ArchitectureState state, string floorId, Random rng)
    {
        var (branch, index, floor) = Locate(state, floorId);
        if (floor.Locked) throw RequestValidationException.For("floor", $"O andar {floor.Id} está travado. Destrave para rolar de novo.");
        var table = floor.Table.Length > 0 ? floor.Table : generator.TableFor(state, floor);
        branch.Floors[index] = NetArchitectureGenerator.WithRow(floor, table, generator.RollRow(state, table, floor.Id, rng));
        generator.Furnish(state, rng);
    }

    public void SetFloor(ArchitectureState state, string floorId, string table, int roll, Random rng)
    {
        var (branch, index, floor) = Locate(state, floorId);
        var natural = generator.TableFor(state, floor);
        var allowed = new List<string> { natural };
        if (natural != NetArchitectureRuleBook.LobbyTable && NetArchitectureRuleBook.Harder(state.Difficulty) is { } boss)
            allowed.Add(boss.ToString());
        if (!allowed.Contains(table))
            throw RequestValidationException.For("table", natural == NetArchitectureRuleBook.LobbyTable
                ? $"O andar {floor.Id} é do Lobby e usa só a tabela do Lobby ({rules.Document.LobbyTable.Source})."
                : $"Use a tabela {natural} ou a {string.Join("", allowed.Skip(1))} como Boss Floor ({rules.Document.BossFloor.Source}).");
        branch.Floors[index] = NetArchitectureGenerator.WithRow(floor, table, rules.Row(table, roll));
        generator.Furnish(state, rng);
    }

    public void SwapIce(ArchitectureState state, string contentId, string slug, NetIceCatalog ice)
    {
        var (floor, index, content) = LocateContent(state, contentId);
        if (content.Kind != NetContentKind.BlackIce) throw RequestValidationException.For("content", "Só dá para trocar uma Black ICE por outra.");
        var current = ice.Find(content.Slug) ?? throw RequestValidationException.For("slug", $"{content.Slug} não está no catálogo.");
        var next = ice.Find(slug) ?? throw RequestValidationException.For("slug", $"Black ICE '{slug}' não está no catálogo.");
        if (next.Price != current.Price)
            throw RequestValidationException.For("slug",
                $"{next.Name} custa {next.Price}eb e {current.Name} custa {current.Price}eb: a troca precisa ser por um Program de mesmo custo ({rules.Document.SameCostSwap.Source}).");
        floor.Contents[index] = content with { Slug = next.Slug };
    }

    public void SetNode(ArchitectureState state, string contentId, SetNodeRequest r)
    {
        var (floor, index, content) = LocateContent(state, contentId);
        if (content.Kind != NetContentKind.ControlNode) throw RequestValidationException.For("content", "Só Control Nodes têm função.");
        if (r.DemonId is { } demonId && state.Demons.All(d => d.Id != demonId))
            throw RequestValidationException.For("demonId", $"Demon {demonId} não existe nesta Arquitetura.");

        NodeFunction function;
        if (r.DefenseId is { } defenseId)
        {
            var defense = rules.FindDefense(defenseId) ?? throw RequestValidationException.For("defenseId", $"Defesa '{defenseId}' não existe.");
            var loadout = (r.Loadout ?? []).Distinct().ToList();
            if (loadout.Any(l => !defense.LoadoutOptions.Contains(l)))
                throw RequestValidationException.For("loadout", $"{defense.Name} só aceita: {string.Join("; ", defense.LoadoutOptions)}.");
            if (defense.LoadoutPicks > 0 && loadout.Count != defense.LoadoutPicks)
                throw RequestValidationException.For("loadout", $"{defense.Name} leva {defense.LoadoutPicks} item(ns) do armamento (CP:R p.{defense.Page}).");
            if (defense.RequiresDemon && r.DemonId is null)
                throw RequestValidationException.For("demonId", $"{defense.Name} é uma Active Defense e só funciona com um Demon no Control Node ({rules.Document.ActiveNeedsDemon.Source}).");
            function = new NodeFunction { DefenseId = defense.Id, Loadout = loadout, Label = Clean(r.Label), DemonId = r.DemonId };
        }
        else if (r.DeviceId is { } deviceId)
        {
            if (rules.FindDevice(deviceId) is null) throw RequestValidationException.For("deviceId", $"Dispositivo '{deviceId}' não existe.");
            function = new NodeFunction { DeviceId = deviceId, Label = Clean(r.Label), DemonId = r.DemonId };
        }
        else if (Clean(r.Label) is { } label)
            function = new NodeFunction { Label = label, DemonId = r.DemonId };
        else
            throw RequestValidationException.For("function", "Escolha uma defesa, um dispositivo ou descreva o que o Control Node controla.");

        floor.Contents[index] = content with { Function = function };
    }

    public void AddDemon(ArchitectureState state, DemonRequest r)
    {
        var rule = rules.Demon(r.Type);
        var max = rules.MaxDemons(state.FloorCount);
        if (state.Demons.Count >= max)
            throw RequestValidationException.For("demon", $"Com {state.FloorCount} andares cabem no máximo {max} Demon(s) ({rules.Document.FloorsPerDemon.Source}).");
        var floor = r.FloorId is null ? null : Locate(state, r.FloorId).Floor;
        var id = $"D{Enumerable.Range(1, 99).First(n => state.Demons.All(d => d.Id != $"D{n}"))}";
        state.Demons.Add(new NetDemon { Id = id, Type = rule.Id, FloorId = floor?.Id });
        NetArchitectureGenerator.AssignDemons(state);
    }

    public void UpdateDemon(ArchitectureState state, string demonId, DemonRequest r)
    {
        var index = state.Demons.FindIndex(d => d.Id == demonId);
        if (index < 0) throw new NotFoundException($"Demon {demonId} não encontrado.");
        var rule = rules.Demon(r.Type);
        var floor = r.FloorId is null ? null : Locate(state, r.FloorId).Floor;
        state.Demons[index] = state.Demons[index] with { Type = rule.Id, FloorId = floor?.Id ?? state.Demons[index].FloorId };
    }

    /// <summary>Removes a Demon; its nodes go to the other Demons, unless an Active Defense would be left without one.</summary>
    public void RemoveDemon(ArchitectureState state, string demonId)
    {
        var demon = state.Demons.FirstOrDefault(d => d.Id == demonId) ?? throw new NotFoundException($"Demon {demonId} não encontrado.");
        var orphanActive = state.Contents.Any(c => c.Function?.DemonId == demonId && rules.FindDefense(c.Function.DefenseId)?.RequiresDemon == true);
        if (orphanActive && state.Demons.Count == 1)
            throw RequestValidationException.For("demon",
                $"{demon.Id} opera uma Active Defense, que só funciona com Demon ({rules.Document.ActiveNeedsDemon.Source}). Troque a defesa antes de remover o Demon.");
        state.Demons.Remove(demon);
        foreach (var floor in state.Floors)
            for (var i = 0; i < floor.Contents.Count; i++)
                if (floor.Contents[i].Function is { } f && f.DemonId == demonId)
                    floor.Contents[i] = floor.Contents[i] with { Function = f with { DemonId = null } };
        NetArchitectureGenerator.AssignDemons(state);
    }

    public void AddNetrunner(ArchitectureState state, NetrunnerRequest r)
    {
        var watcher = Netrunner(state, $"N{Enumerable.Range(1, 99).First(n => state.Netrunners.All(w => w.Id != $"N{n}"))}", r);
        state.Netrunners.Add(watcher);
    }

    public void UpdateNetrunner(ArchitectureState state, string id, NetrunnerRequest r)
    {
        var index = state.Netrunners.FindIndex(w => w.Id == id);
        if (index < 0) throw new NotFoundException($"Netrunner {id} não encontrado.");
        state.Netrunners[index] = Netrunner(state, id, r);
    }

    public void RemoveNetrunner(ArchitectureState state, string id)
    {
        if (state.Netrunners.RemoveAll(w => w.Id == id) == 0) throw new NotFoundException($"Netrunner {id} não encontrado.");
    }

    public void LockFloor(ArchitectureState state, string floorId, bool locked)
    {
        var (branch, index, floor) = Locate(state, floorId);
        branch.Floors[index] = floor with { Locked = locked };
    }

    // ------------------------------------------------------------------ helpers

    private NetWatcher Netrunner(ArchitectureState state, string id, NetrunnerRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw RequestValidationException.For("name", "Dê um nome ao Netrunner.");
        if (r.Interface is < 1 or > 10) throw RequestValidationException.For("interface", "Interface vai de 1 a 10.");
        if (r.PathfinderBonus is < 0 or > 10) throw RequestValidationException.For("pathfinderBonus", "O bônus de Pathfinder vai de 0 a 10.");
        var floor = r.FloorId is null ? state.Main.Floors[^1] : Locate(state, r.FloorId).Floor;
        return new NetWatcher { Id = id, Name = r.Name.Trim()[..Math.Min(r.Name.Trim().Length, 60)], Interface = r.Interface, PathfinderBonus = r.PathfinderBonus, FloorId = floor.Id };
    }

    private static (NetBranch Branch, int Index, NetFloor Floor) Locate(ArchitectureState state, string floorId)
    {
        foreach (var branch in state.Branches)
        {
            var index = branch.Floors.FindIndex(f => f.Id == floorId);
            if (index >= 0) return (branch, index, branch.Floors[index]);
        }
        throw new NotFoundException($"Andar {floorId} não encontrado.");
    }

    private static (NetFloor Floor, int Index, NetContent Content) LocateContent(ArchitectureState state, string contentId)
    {
        var floor = state.FloorOf(contentId) ?? throw new NotFoundException($"Conteúdo {contentId} não encontrado.");
        var index = floor.Contents.FindIndex(c => c.Id == contentId);
        return (floor, index, floor.Contents[index]);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(text.Trim().Length, 120)];
}
