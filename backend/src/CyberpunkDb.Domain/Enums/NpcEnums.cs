using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CyberpunkDb.Domain.Enums;

// CP:R p.399; Danger Gal Dossier p.150
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NpcLevel
{
    [Display(Name = "Mook")] Mook,
    [Display(Name = "Lieutenant")] Lieutenant,
    [Display(Name = "Mini-Boss")] MiniBoss,
    [Display(Name = "Boss")] Boss,
}

// always recomputed by the server
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NpcStatus
{
    [Display(Name = "Rascunho (validação pendente)")] Draft,
    [Display(Name = "Válido")] Valid,
    [Display(Name = "Válido com exceções do Mestre")] ValidWithExceptions,
}
