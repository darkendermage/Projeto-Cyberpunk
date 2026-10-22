using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CyberpunkDb.Domain.Enums;

/// <summary>Whether a saved Night Market is still taking purchases.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NightMarketStatus
{
    [Display(Name = "Aberto")] Open,
    [Display(Name = "Encerrado")] Closed,
}
