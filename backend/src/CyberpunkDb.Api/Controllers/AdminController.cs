using System.Text;
using System.Text.Json;
using CyberpunkDb.Application.Catalog;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

/// <summary>
/// Catalog maintenance: create/edit/delete items and import/export data.
/// There is no authentication yet (local single-user tool); put this behind auth before exposing it publicly.
/// </summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminController(ItemWriteService writer) : ControllerBase
{
    private const long MaxImportBytes = 10 * 1024 * 1024;

    [HttpPost("items")]
    public async Task<ActionResult<ItemDto>> Create(ItemUpsertDto dto, CancellationToken ct)
    {
        var item = await writer.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(CatalogController.GetItem), "Catalog", new { id = item.Id }, item);
    }

    [HttpPut("items/{id:int}")]
    public Task<ItemDto> Update(int id, ItemUpsertDto dto, CancellationToken ct) => writer.UpdateAsync(id, dto, ct);

    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await writer.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Upserts items by slug. Body: JSON array in the export format.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ImportResult> Import([FromBody] JsonElement body, CancellationToken ct)
    {
        if (body.ValueKind != JsonValueKind.Array)
            throw RequestValidationException.For("body", "Envie uma lista JSON de itens (o mesmo formato da exportação).");
        List<ItemUpsertDto> rows;
        try
        {
            rows = body.Deserialize<List<ItemUpsertDto>>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException ex)
        {
            throw RequestValidationException.For("body", $"JSON em formato inesperado: {ex.Message}");
        }
        if (rows.Count > 5000) throw RequestValidationException.For("body", "Máximo de 5000 itens por importação.");
        return await writer.ImportAsync(rows, ct);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? category, [FromQuery] string format = "json", CancellationToken ct = default)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
        var name = string.IsNullOrWhiteSpace(category) ? "catalogo" : category;

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(category))
                throw RequestValidationException.For("category", "A exportação CSV é feita por categoria.");
            var csv = await writer.ExportCsvAsync(category, ct);
            // BOM so Excel opens accents correctly.
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv; charset=utf-8", $"{name}-{stamp}.csv");
        }

        var rows = await writer.ExportAsync(category, ct);
        var json = JsonSerializer.SerializeToUtf8Bytes(rows, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        return File(json, "application/json", $"{name}-{stamp}.json");
    }
}
