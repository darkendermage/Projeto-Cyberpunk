namespace CyberpunkDb.Api.Table;

// Table:PlayerPort is what players reach through the tunnel and only serves the table. Without it, the table
// is GM-only.
public sealed class TableOptions
{
    public int? PlayerPort { get; set; }

    public bool IsPlayerSurface(HttpContext context) => PlayerPort is { } port && context.Connection.LocalPort == port;
}
