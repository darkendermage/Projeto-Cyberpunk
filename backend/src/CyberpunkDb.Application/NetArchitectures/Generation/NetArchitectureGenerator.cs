using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.NetArchitectures.Generation;

/// <summary>
/// Builds a NET Architecture following CP:R p.210-211: Difficulty Rating (Step 0), floors and branches (Step 1),
/// Lobby and Body tables (Step 2), then Control Node functions, File contents and Demons for the location (Step 3),
/// plus the Going Quiet Watchers. The same seed and options always give the same Architecture.
/// </summary>
public sealed class NetArchitectureGenerator(NetArchitectureRuleBook rules)
{
    private const int MaxRerolls = 500;

    public ArchitectureState Generate(GenerateArchitectureRequest request)
    {
        var seed = request.Seed ?? Random.Shared.Next(1, int.MaxValue);
        var rng = new Random(seed);
        var g = rules.Document.Generator;
        var difficulty = request.Difficulty ?? Weighted(g.DifficultyWeights.Value.ToList(), kv => kv.Value, rng).Key;
        var location = rules.Location(request.LocationId);

        var dice = rules.Document.Shape.FloorDice.Value;
        var floors = request.Floors ?? Enumerable.Range(0, dice.Count).Sum(_ => rng.Next(1, dice.Sides + 1));
        if (floors is < 3 or > 18)
            throw RequestValidationException.For("floors", L.T($"Uma Arquitetura tem de 3 a 18 andares ({rules.Document.Shape.FloorDice.Source}).", $"An Architecture has 3 to 18 floors ({rules.Document.Shape.FloorDice.Source})."));
        var branches = request.Branches ?? RollBranches(rng);
        if (branches is < 0 or > 8) throw RequestValidationException.For("branches", L.T("Use de 0 a 8 ramos.", "Use 0 to 8 branches."));

        var state = new ArchitectureState
        {
            Seed = seed,
            Difficulty = difficulty,
            LocationId = location.Id,
            RespectNodeLimit = request.RespectNodeLimit ?? g.RespectNodeLimit.Value,
            Branches = Shape(floors, branches, rng),
        };
        Fill(state, rng);
        Furnish(state, rng);

        var wantsNetrunner = request.EnemyNetrunner ?? rng.NextDouble() < g.EnemyNetrunnerChance.Value.GetValueOrDefault(difficulty);
        if (wantsNetrunner)
            state.Netrunners.Add(new NetWatcher
            {
                Id = "N1",
                Name = L.T("Netrunner de plantão", "Netrunner on duty"),
                Interface = rules.Difficulty(difficulty).FightingChance,
                FloorId = state.Main.Floors[^1].Id,
            });
        return state;
    }

    /// <summary>Rolls every unlocked floor again on the same shape, then gives new Control Nodes and Files their purpose.</summary>
    public void Refill(ArchitectureState state, Random rng)
    {
        Fill(state, rng);
        Furnish(state, rng);
    }

    // ------------------------------------------------------------------ Step 1: shape

    /// <summary>d10 until a roll under 7: each success is a branch (CP:R p.210).</summary>
    private int RollBranches(Random rng)
    {
        var shape = rules.Document.Shape;
        var count = 0;
        while (count < 8 && rng.Next(1, shape.BranchDie.Value + 1) >= shape.BranchOn.Value) count++;
        return count;
    }

    /// <summary>
    /// Splits the floors between the main branch and the side branches: the Lobby stays on the main branch, no branch
    /// starts before the third floor and the main branch is strictly the longest, so the bottom is well defined.
    /// </summary>
    public List<NetBranch> Shape(int total, int branches, Random rng)
    {
        var shape = rules.Document.Shape;
        var firstFloor = shape.FirstBranchFloor.Value;       // first depth a branch floor can have
        var minMain = firstFloor + 1;                        // a branch floor must stay above the bottom
        while (branches > 0 && total - branches < minMain) branches--;

        var main = branches == 0 ? total : minMain;
        var lengths = Enumerable.Repeat(1, branches).ToList();
        for (var extra = total - main - branches; extra > 0; extra--)
        {
            if (branches == 0 || rng.NextDouble() < 0.5) main++;
            else lengths[rng.Next(branches)]++;
        }
        // A branch must end above the main branch's bottom: give its excess floors back to the main branch.
        for (var i = 0; i < lengths.Count; i++)
            while (lengths[i] > main - firstFloor) { lengths[i]--; main++; }

        var result = new List<NetBranch> { Branch("A", null, 0, main) };
        for (var i = 0; i < lengths.Count; i++)
        {
            var from = rng.Next(firstFloor - 1, main - lengths[i]);  // splits after main floor `from`
            result.Add(Branch(((char)('B' + i)).ToString(), from, from, lengths[i]));
        }
        return result;
    }

    private static NetBranch Branch(string id, int? from, int startDepth, int length) => new()
    {
        Id = id,
        FromFloor = from,
        Floors = Enumerable.Range(startDepth + 1, length).Select(depth => new NetFloor { Id = $"{id}{depth}", BranchId = id, Depth = depth }).ToList(),
    };

    // ------------------------------------------------------------------ Step 2: fill

    /// <summary>The table a floor rolls on: the Lobby for the first floors of the main branch, the difficulty's otherwise.</summary>
    public string TableFor(ArchitectureState state, NetFloor floor) =>
        floor.BranchId == state.Main.Id && floor.Depth <= rules.Document.Shape.LobbyFloors.Value
            ? NetArchitectureRuleBook.LobbyTable
            : state.Difficulty.ToString();

    private void Fill(ArchitectureState state, Random rng)
    {
        // Unlocked floors are rolled from scratch: their old rows must not block the re-roll rules.
        foreach (var branch in state.Branches)
            for (var i = 0; i < branch.Floors.Count; i++)
                if (!branch.Floors[i].Locked) branch.Floors[i] = branch.Floors[i] with { Roll = null, Contents = [] };

        foreach (var branch in state.Branches)
            for (var i = 0; i < branch.Floors.Count; i++)
            {
                var floor = branch.Floors[i];
                if (floor.Locked) continue;
                var table = TableFor(state, floor);
                var row = RollRow(state, table, floor.Id, rng);
                branch.Floors[i] = WithRow(floor, table, row);
            }
    }

    /// <summary>
    /// Rolls a row, re-rolling Black ICE and Password rows already in the Architecture (CP:R p.210) and, when asked,
    /// Control Nodes past the purchase limit (CP:R p.217).
    /// </summary>
    public TableRow RollRow(ArchitectureState state, string table, string floorId, Random rng)
    {
        var rows = rules.Table(table);
        var used = state.Floors.Where(f => f.Id != floorId && f.Table == table && f.Roll is not null).Select(f => f.Roll!.Value).ToHashSet();
        var nodeLimit = state.RespectNodeLimit ? rules.Band(state.FloorCount).MaxControlNodes : null;
        var nodes = state.Floors.Where(f => f.Id != floorId).SelectMany(f => f.Contents).Count(c => c.Kind == NetContentKind.ControlNode);

        for (var attempt = 0; attempt < MaxRerolls; attempt++)
        {
            var row = table == NetArchitectureRuleBook.LobbyTable
                ? rows[rng.Next(rows.Count)]
                : rules.Row(table, rng.Next(1, 7) + rng.Next(1, 7) + rng.Next(1, 7));
            if (rules.Unique(row) && used.Contains(row.Roll)) continue;
            if (nodeLimit is { } max && nodes + row.Contents.Count(c => c.Kind == NetContentKind.ControlNode) > max) continue;
            return row;
        }
        // Every row is taken (only possible with a GM-built Architecture): fall back to a File, which may repeat.
        return rows.First(r => r.Contents.All(c => c.Kind == NetContentKind.File));
    }

    public static NetFloor WithRow(NetFloor floor, string table, TableRow row) => floor with
    {
        Table = table,
        Roll = row.Roll,
        Contents = row.Contents.Select((c, i) => new NetContent { Id = $"{floor.Id}-{i + 1}", Kind = c.Kind, Slug = c.Slug, Dv = c.Dv }).ToList(),
    };

    // ------------------------------------------------------------------ Step 3: fit to the world

    /// <summary>
    /// Gives every File a content and every Control Node a function that fits the location, then makes sure Demons are
    /// there to run Active Defenses (CP:R p.211) without going past one per six floors (CP:R p.218).
    /// </summary>
    public void Furnish(ArchitectureState state, Random rng)
    {
        var location = rules.Location(state.LocationId);
        var generic = rules.Location("generic");
        var ideas = location.FileIdeas.Length > 0 ? location.FileIdeas : generic.FileIdeas;
        var maxDemons = rules.MaxDemons(state.FloorCount);

        foreach (var branch in state.Branches)
            foreach (var floor in branch.Floors)
                for (var i = 0; i < floor.Contents.Count; i++)
                {
                    var c = floor.Contents[i];
                    if (c.Kind == NetContentKind.File && c.FileIdea is null)
                        floor.Contents[i] = c with { FileIdea = ideas[rng.Next(ideas.Length)] };
                    else if (c.Kind == NetContentKind.ControlNode && c.Function is null)
                        floor.Contents[i] = c with { Function = PickFunction(location, rng, allowActive: maxDemons > 0) };
                }

        var nodes = state.Contents.Where(c => c.Kind == NetContentKind.ControlNode).ToList();
        var needsDemon = nodes.Any(n => rules.FindDefense(n.Function?.DefenseId)?.RequiresDemon == true);
        if (state.Demons.Count == 0 && nodes.Count > 0 && (needsDemon || rng.NextDouble() < location.DemonChance))
        {
            var perDemon = Math.Max(1, rules.Document.Generator.NodesPerDemon.Value);
            var count = Math.Clamp((int)Math.Ceiling(nodes.Count / (double)perDemon), 1, maxDemons);
            var weights = rules.Document.Generator.DemonWeights.Value.GetValueOrDefault(state.Difficulty) ?? new() { ["imp"] = 1 };
            for (var i = 0; i < count; i++)
                state.Demons.Add(new NetDemon { Id = $"D{i + 1}", Type = Weighted(weights.ToList(), kv => kv.Value, rng).Key });
        }
        AssignDemons(state);
    }

    /// <summary>Hands every unassigned Control Node to a Demon (round robin) and seats each Demon by its first node.</summary>
    public static void AssignDemons(ArchitectureState state)
    {
        if (state.Demons.Count == 0) return;
        var next = 0;
        foreach (var floor in state.Floors)
            for (var i = 0; i < floor.Contents.Count; i++)
            {
                var c = floor.Contents[i];
                if (c.Kind != NetContentKind.ControlNode || c.Function is null) continue;
                if (c.Function.DemonId is { } d && state.Demons.Any(x => x.Id == d)) continue;
                floor.Contents[i] = c with { Function = c.Function with { DemonId = state.Demons[next++ % state.Demons.Count].Id } };
            }
        for (var i = 0; i < state.Demons.Count; i++)
        {
            var demon = state.Demons[i];
            if (demon.FloorId is not null && state.Floor(demon.FloorId) is not null) continue;
            var home = state.Floors.FirstOrDefault(f => f.Contents.Any(c => c.Function?.DemonId == demon.Id)) ?? state.Main.Floors[^1];
            state.Demons[i] = demon with { FloorId = home.Id };
        }
    }

    private NodeFunction PickFunction(LocationRule location, Random rng, bool allowActive)
    {
        if (rng.NextDouble() < location.DefenseShare)
        {
            var options = rules.Defenses.Where(d => allowActive || !d.RequiresDemon).ToList();
            var defense = location.Defenses.Count > 0
                ? Weighted(options, d => location.Defenses.GetValueOrDefault(d.Id), rng)
                : options[rng.Next(options.Count)];
            return new NodeFunction { DefenseId = defense.Id, Loadout = PickLoadout(defense, rng) };
        }
        var device = location.Devices.Count > 0
            ? Weighted(rules.Devices.ToList(), d => location.Devices.GetValueOrDefault(d.Id), rng)
            : rules.Devices[rng.Next(rules.Devices.Count)];
        return new NodeFunction { DeviceId = device.Id };
    }

    public static List<string> PickLoadout(DefenseRule defense, Random rng) =>
        defense.LoadoutOptions.OrderBy(_ => rng.Next()).Take(defense.LoadoutPicks).ToList();

    private static T Weighted<T>(IReadOnlyList<T> items, Func<T, double> weight, Random rng)
    {
        var weights = items.Select(i => Math.Max(0, weight(i))).ToArray();
        var total = weights.Sum();
        if (total <= 0) return items[rng.Next(items.Count)];
        var roll = rng.NextDouble() * total;
        for (var i = 0; i < items.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return items[i];
        }
        return items[^1];
    }
}
