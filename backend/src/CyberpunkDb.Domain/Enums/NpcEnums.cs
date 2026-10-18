using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CyberpunkDb.Domain.Enums;

/// <summary>NPC power levels (CP:R p.399; Danger Gal Dossier p.150).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NpcLevel
{
    [Display(Name = "Mook")] Mook,
    [Display(Name = "Lieutenant")] Lieutenant,
    [Display(Name = "Mini-Boss")] MiniBoss,
    [Display(Name = "Boss")] Boss,
}

/// <summary>Validation status stored with a saved NPC. Always recomputed by the server.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NpcStatus
{
    [Display(Name = "Rascunho (validação pendente)")] Draft,
    [Display(Name = "Válido")] Valid,
    [Display(Name = "Válido com exceções do Mestre")] ValidWithExceptions,
}
