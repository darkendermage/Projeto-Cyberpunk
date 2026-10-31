using System.Text.Json;
using System.Text.Json.Serialization;

namespace CyberpunkDb.Application.Npcs.Compendium;

/// <summary>The loaded <c>npc-compendium.json</c>: named NPCs of the books, indexed by id. Singleton.</summary>
public sealed class NpcCompendium
{
    private const string ResourceName = "CyberpunkDb.Application.Npcs.npc-compendium.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, CompendiumNpc> _byId;

    public NpcCompendium(IReadOnlyList<CompendiumNpc> all)
    {
        All = all;
        _byId = all.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CompendiumNpc> All { get; }

    public CompendiumNpc? Find(string id) => _byId.GetValueOrDefault(id);

    public static NpcCompendium LoadEmbedded()
    {
        using var stream = typeof(NpcCompendium).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Recurso {ResourceName} não encontrado.");
        return new NpcCompendium(JsonSerializer.Deserialize<List<CompendiumNpc>>(stream, JsonOptions)
                                 ?? throw new InvalidOperationException("npc-compendium.json vazio."));
    }
}
