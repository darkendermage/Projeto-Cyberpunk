using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog;
using CyberpunkDb.Application.Collections;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Generation;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CyberpunkDb.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(_ => new CategoryRegistry());
        services.AddSingleton<ISearchTextBuilder>(sp => sp.GetRequiredService<CategoryRegistry>());
        services.AddScoped<CatalogService>();
        services.AddScoped<ItemWriteService>();
        services.AddScoped<CollectionsService>();
        services.AddSingleton(_ => NpcRuleBook.LoadEmbedded());
        services.AddSingleton<NpcValidator>();
        services.AddSingleton<NpcGenerator>();
        services.AddSingleton<NpcCatalogCache>();
        services.AddScoped<NpcCatalogProvider>();
        services.AddScoped<NpcService>();
        services.AddValidatorsFromAssemblyContaining<GearListUpsertValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
