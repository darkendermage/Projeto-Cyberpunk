using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CyberpunkDb.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NightMarketStatus
{
    [Display(Name = "Aberto")] Open,
    [Display(Name = "Encerrado")] Closed,
}
