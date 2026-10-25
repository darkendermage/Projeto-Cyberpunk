using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Creation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Collections;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.NetArchitectures;
using CyberpunkDb.Application.NetArchitectures.Generation;
using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Application.NightMarkets;
using CyberpunkDb.Application.NightMarkets.Generation;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Application.NightMarkets.Trading;
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
        services.AddSingleton(sp => CharacterRuleBook.LoadEmbedded(sp.GetRequiredService<NpcRuleBook>()));
        services.AddSingleton<CharacterCalculator>();
        services.AddSingleton<Characters.Roles.RoleModuleRegistry>();
        services.AddSingleton<CharacterValidator>();
        services.AddSingleton<ProgressionOperations>();
        services.AddSingleton<IDiceRoller, RandomDiceRoller>();
        services.AddSingleton<CyberwareInstaller>();
        services.AddSingleton<CharacterCreationService>();
        services.AddSingleton<CyberwareOperations>();
        services.AddSingleton<InventoryOperations>();
        services.AddSingleton<EconomyOperations>();
        services.AddSingleton<CombatOperations>();
        services.AddSingleton<WearableOperations>();
        services.AddScoped<CharacterService>();
        services.AddSingleton(sp => NightMarketRuleBook.LoadEmbedded(sp.GetRequiredService<NpcRuleBook>()));
        services.AddSingleton<MarketPricing>();
        services.AddSingleton<MarketOperations>();
        services.AddSingleton<NightMarketGenerator>();
        services.AddSingleton<MarketCatalogCache>();
        services.AddScoped<MarketCatalogProvider>();
        services.AddScoped<NightMarketService>();
        services.AddSingleton(_ => NetArchitectureRuleBook.LoadEmbedded());
        services.AddSingleton<NetArchitectureGenerator>();
        services.AddSingleton<NetArchitectureOperations>();
        services.AddSingleton<NetArchitecturePricing>();
        services.AddSingleton<NetArchitectureExport>();
        services.AddSingleton<NetIceCatalogCache>();
        services.AddScoped<NetIceCatalogProvider>();
        services.AddScoped<NetArchitectureService>();
        services.AddSingleton(_ => CombatRuleBook.LoadEmbedded());
        services.AddSingleton<FighterViews>();
        services.AddSingleton<AttackResolver>();
        services.AddSingleton<NpcCombat>();
        services.AddScoped<EncounterService>();
        services.AddValidatorsFromAssemblyContaining<GearListUpsertValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
