using System.Text.Json.Serialization;
using CyberpunkDb.Api.Desktop;
using CyberpunkDb.Api.Infrastructure;
using CyberpunkDb.Application;
using CyberpunkDb.Infrastructure;
using CyberpunkDb.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Single-file executable: configuration is embedded (bundle/appsettings*.json).
// Physical files with the same name next to the .exe and environment variables still win.
if (EmbeddedBundle.Open("bundle/appsettings.json") is { } embeddedSettings)
    builder.Configuration.Sources.Insert(0, new Microsoft.Extensions.Configuration.Json.JsonStreamConfigurationSource { Stream = embeddedSettings });
if (EmbeddedBundle.Open("bundle/appsettings.Desktop.json") is { } embeddedDesktop)
    builder.Configuration.AddJsonStream(embeddedDesktop);
builder.Configuration.AddJsonFile("appsettings.Desktop.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

var desktop = builder.Configuration.GetValue<bool>("Desktop:Enabled");
if (desktop)
{
    DesktopLauncher.ConfigureConsole();
    DesktopLauncher.WaitForPreviousInstance(builder.Configuration);
    AppUpdater.CleanupOldBinaries(Environment.ProcessPath);
    if (await DesktopLauncher.TryReuseRunningInstanceAsync(builder.Configuration)) return;
}

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SupportNonNullableReferenceTypes());
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddSingleton(new AppUpdater(AppUpdaterOptions.FromConfiguration(builder.Configuration)));
builder.Services.AddApplication();
if (EmbeddedBundle.HasSeed) builder.Services.AddSingleton<ISeedSource, EmbeddedSeedSource>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Frontend embedded in the executable (desktop build) replaces the wwwroot folder.
if (EmbeddedBundle.HasWebRoot) app.Environment.WebRootFileProvider = new EmbeddedWebRootProvider();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "online", version = AppVersion.Current }));
app.MapGet("/api/app/update", (AppUpdater updater, CancellationToken ct) => updater.CheckAsync(ct));
// A JSON body is required on purpose: it forces a CORS preflight, so other sites cannot trigger it.
app.MapPost("/api/app/update", async (ApplyUpdateRequest body, AppUpdater updater, HttpContext http, CancellationToken ct) =>
{
    await updater.ApplyAsync(body.Version, ct);
    http.Response.OnCompleted(() =>
    {
        DesktopLauncher.RestartAfterUpdate(app, updater.Options.ExePath!);
        return Task.CompletedTask;
    });
    return Results.Ok(new { restarting = true });
});
app.Map("/api/{**rest}", () => Results.Problem(statusCode: 404, title: "Rota da API não encontrada."));

// Built frontend served by the API itself (wwwroot folder or embedded bundle).
app.UseDefaultFiles();
app.UseStaticFiles();
if (app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists) app.MapFallbackToFile("index.html");

try
{
    using (var scope = app.Services.CreateScope())
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(app.Lifetime.ApplicationStopping);

    if (desktop) DesktopLauncher.OpenBrowserWhenStarted(app);
    await app.RunAsync();
}
catch (Exception ex) when (desktop)
{
    DesktopLauncher.ReportFatal(ex, app.Configuration);
}

public partial class Program;
