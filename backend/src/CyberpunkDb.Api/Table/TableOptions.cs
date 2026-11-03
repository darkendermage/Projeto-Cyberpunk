namespace CyberpunkDb.Api.Table;

/// <summary>
/// Table:PlayerPort is the port the players reach (through the tunnel). Requests that arrive on it only see the virtual
/// table; the GM uses the other port, which serves the whole app. Without it, the table runs only for the GM.
/// </summary>
public sealed class TableOptions
{
    public int? PlayerPort { get; set; }

    /// <summary>Whether this request arrived on the players' port.</summary>
    public bool IsPlayerSurface(HttpContext context) => PlayerPort is { } port && context.Connection.LocalPort == port;
}
