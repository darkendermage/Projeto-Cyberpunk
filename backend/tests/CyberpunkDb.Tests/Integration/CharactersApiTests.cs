using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Transfer;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

[Collection(ApiCollection.Name)]
public class CharactersApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();

    // ------------------------------------------------------------ helpers

    private async Task<CharacterDetailDto> Send(HttpMethod method, string url, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body, options: Json) };
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>(Json))!;
    }

    private async Task<HttpResponseMessage> Raw(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body, options: Json) };
        return await _client.SendAsync(request);
    }

    private Task<CharacterDetailDto> Create(CreationMethod method, string role, string name = "Teste") =>
        Send(HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = name, Method = method, Role = role, NativeLanguage = "English", Home = "Watson" },
            HttpStatusCode.Created);

    private Task<CharacterDetailDto> Post(CharacterDetailDto c, string path, object body) =>
        Send(HttpMethod.Post, $"/api/characters/{c.Id}/{path}", body);

    private Task<CharacterDetailDto> Save(CharacterDetailDto c, CharacterSheet sheet, string? reason = null) =>
        Send(HttpMethod.Put, $"/api/characters/{c.Id}", new SaveSheetRequest(c.Version, sheet, reason));

    private async Task<CharacterDetailDto> FinalizedSolo(string name = "Combatente")
    {
        var c = await Create(CreationMethod.Streetrat, "Solo", name);
        c = await Post(c, "creation/template-row", new TemplateRowRequest(c.Version, 6));
        c = await Save(c, c.Sheet with
        {
            Creation = c.Sheet.Creation with
            {
                StartingChoices = new Dictionary<string, int> { ["weapons:2"] = 0, ["cyberware:2"] = 0 },
            },
        });
        return await Post(c, "creation/finalize", new VersionRequest(c.Version));
    }

    // ------------------------------------------------------------ rules

    [Fact]
    public async Task Rules_expose_roles_methods_and_tables_from_the_rule_files()
    {
        var rules = (await _client.GetFromJsonAsync<CharacterRulesDto>("/api/characters/rules", Json))!;

        rules.Roles.Should().HaveCount(10);
        rules.Methods.Single(m => m.Method == CreationMethod.CompletePackage).StatPoints.Should().Be(62);
        rules.Skills.Should().HaveCount(66).And.OnlyContain(s => s.NamePt != null);
        rules.CriticalInjuries.Should().HaveCount(22);
        var solo = rules.Roles.Single(r => r.Id == "Solo");
        solo.Starting.Single(s => s.Key == "cyberware").Groups[2].Options.Select(o => o.Label)
            .Should().Equal("Sandevistan", "Wolvers (braço de carne)");
        solo.Starting.SelectMany(s => s.Groups).SelectMany(g => g.Options).SelectMany(o => o.Items).Should().OnlyContain(i => i.ItemId != null);
        rules.Facts.Should().OnlyContain(f => f.Source != null || f.Complementary);
        rules.Roles.Should().OnlyContain(r => r.AbilityInfo.Summary.Length > 0 && r.AbilityInfo.Points.Count > 0 && r.AbilityInfo.Source.StartsWith("CP:R p."));
    }

    [Fact]
    public async Task Store_pages_through_the_whole_catalog_with_descriptions()
    {
        var first = (await _client.GetFromJsonAsync<StorePageDto>("/api/characters/store?category=Weapon&page=1&pageSize=12", Json))!;
        first.Items.Should().HaveCount(12);
        first.Total.Should().BeGreaterThan(60, "antes a loja parava em 60 itens");
        first.Items.Should().Contain(i => !string.IsNullOrWhiteSpace(i.Description));

        var last = (first.Total + 11) / 12;
        var end = (await _client.GetFromJsonAsync<StorePageDto>($"/api/characters/store?category=Weapon&page={last}&pageSize=12", Json))!;
        end.Page.Should().Be(last);
        end.Items.Should().HaveCount(first.Total - (last - 1) * 12);
        end.Items.Select(i => i.Id).Should().NotIntersectWith(first.Items.Select(i => i.Id));

        var beyond = (await _client.GetFromJsonAsync<StorePageDto>($"/api/characters/store?category=Weapon&page={last + 5}&pageSize=12", Json))!;
        beyond.Page.Should().Be(last, "página fora do fim volta para a última");
    }

    [Fact]
    public async Task Every_cyberware_effect_points_to_an_item_of_the_real_catalog()
    {
        var book = CyberpunkDb.Application.Characters.Rules.CharacterRuleBook.LoadEmbedded(CyberpunkDb.Application.Npcs.Rules.NpcRuleBook.LoadEmbedded());
        var slugs = book.Document.CyberwareEffects.Effects.SelectMany(e => e.Items.Concat(e.AllOf)).Distinct().ToList();
        foreach (var slug in slugs)
        {
            var found = (await _client.GetFromJsonAsync<StorePageDto>($"/api/characters/store?q={slug}&pageSize=20", Json))!;
            found.Items.Should().Contain(i => i.Slug == slug, $"o efeito cita {slug}");
        }
    }

    // ------------------------------------------------------------ 1. the three methods

    // ------------------------------------------------------------ export and import

    [Fact]
    public async Task Export_and_import_bring_the_character_back_as_a_new_one()
    {
        var c = await FinalizedSolo("Exportado");
        c = await Post(c, "balance", new AdjustBalanceRequest(c.Version, 300, "Pagamento do Fixer"));

        var response = await _client.GetAsync($"/api/characters/{c.Id}/export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName)
            .Should().Be("exportado.cyberpunk.json");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var file = JsonSerializer.Deserialize<CharacterExportFile>(bytes, Json)!;
        file.Items!.Values.Select(i => i.Slug).Should().Contain("sandevistan");
        file.History.Should().Contain(e => e.Action == "manual-adjustment" && e.Description == "Pagamento do Fixer");

        // Sent exactly as the browser sends the chosen file.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/characters/import") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new("application/json");
        var importResponse = await _client.SendAsync(request);
        importResponse.StatusCode.Should().Be(HttpStatusCode.Created, await importResponse.Content.ReadAsStringAsync());
        var imported = (await importResponse.Content.ReadFromJsonAsync<CharacterDetailDto>(Json))!;

        imported.Id.Should().NotBe(c.Id);
        imported.Sheet.Identity.Name.Should().Be("Exportado (importado)", "o original continua no banco");
        var sameName = imported.Sheet with { Identity = imported.Sheet.Identity with { Name = c.Sheet.Identity.Name } };
        JsonSerializer.Serialize(sameName, Json).Should().Be(JsonSerializer.Serialize(c.Sheet, Json));
        imported.Report.Health.MaxHp.Should().Be(c.Report.Health.MaxHp);
        imported.Report.Humanity.Current.Should().Be(c.Report.Humanity.Current);
        imported.Status.Should().Be(c.Status);

        var originalHistory = (await _client.GetFromJsonAsync<List<CharacterEventDto>>($"/api/characters/{c.Id}/events", Json))!;
        var importedHistory = (await _client.GetFromJsonAsync<List<CharacterEventDto>>($"/api/characters/{imported.Id}/events", Json))!;
        importedHistory.Should().HaveCount(originalHistory.Count + 1).And.Contain(e => e.Action == "import");
        (await _client.GetAsync($"/api/characters/{c.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        using var again = new HttpRequestMessage(HttpMethod.Post, "/api/characters/import") { Content = new ByteArrayContent(bytes) };
        again.Content.Headers.ContentType = new("application/json");
        var second = (await (await _client.SendAsync(again)).Content.ReadFromJsonAsync<CharacterDetailDto>(Json))!;
        second.Sheet.Identity.Name.Should().Be("Exportado (importado 2)");
    }

    [Fact]
    public async Task Import_refuses_files_that_are_not_characters_or_come_from_a_newer_version()
    {
        var c = await Create(CreationMethod.CompletePackage, "Netrunner", "Versão");
        var exported = (await _client.GetFromJsonAsync<CharacterExportFile>($"/api/characters/{c.Id}/export", Json))!;

        (await Raw(HttpMethod.Post, "/api/characters/import", exported with { Format = "outra-coisa" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Raw(HttpMethod.Post, "/api/characters/import", exported with { FormatVersion = 99 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Raw(HttpMethod.Post, "/api/characters/import", exported with { Sheet = null })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var broken = new HttpRequestMessage(HttpMethod.Post, "/api/characters/import") { Content = new StringContent("{ isto não é json", System.Text.Encoding.UTF8, "application/json") };
        (await _client.SendAsync(broken)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creating_keeps_the_cultural_origin_chosen_with_the_native_language()
    {
        var c = await Send(HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = "Kenji", Method = CreationMethod.CompletePackage, Role = "Solo", NativeLanguage = "Japanese", CulturalRegion = "east asian" },
            HttpStatusCode.Created);
        c.Sheet.Lifepath.CulturalRegion.Should().Be("East Asian");
        c.Sheet.Skills["language:Japanese"].Should().Be(4);

        var unknown = await Raw(HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = "X", Method = CreationMethod.CompletePackage, Role = "Solo", CulturalRegion = "Atlântida" });
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Streetrat_receives_the_role_package_with_preset_humanity_loss()
    {
        var c = await FinalizedSolo("Jay");

        c.Status.Should().Be(CharacterStatus.Complete);
        c.Sheet.Stats["REF"].Should().Be(7, "linha 6 da tabela do Solo (CP:R p.74)");
        c.Report.Weapons.Select(w => w.Name).Should().Contain(["Very Heavy Pistol", "Heavy Melee Weapon"]);
        c.Report.Armor.Body.Sp.Should().Be(11);
        c.Report.Armor.Head.Sp.Should().Be(11);
        c.Report.Cyberware.Select(x => x.Slug).Should().BeEquivalentTo(["biomonitor", "neural-link", "sandevistan"]);
        c.Report.Cyberware.Single(x => x.Slug == "sandevistan").ParentInstanceId.Should().NotBeNull();
        c.Report.Humanity.Lost.Should().Be(14);
        c.Report.Humanity.Current.Should().Be(c.Sheet.Stats["EMP"] * 10 - 14);
        c.Sheet.Wallet.Balance.Should().Be(500);
        c.Sheet.Housing.HousingId.Should().Be("cargo-container");

        var events = (await _client.GetFromJsonAsync<List<CharacterEventDto>>($"/api/characters/{c.Id}/events", Json))!;
        events.Should().Contain(e => e.Action == "starting-cash" && e.Amount == 500);
        events.Count(e => e.Action == "install-cyberware").Should().Be(3);
    }

    [Theory]
    [InlineData("Rockerboy"), InlineData("Solo"), InlineData("Netrunner"), InlineData("Tech"), InlineData("Medtech")]
    [InlineData("Media"), InlineData("Exec"), InlineData("Lawman"), InlineData("Fixer"), InlineData("Nomad")]
    public async Task Every_role_finalizes_as_a_streetrat_with_the_book_humanity_loss(string roleId)
    {
        var rules = (await _client.GetFromJsonAsync<CharacterRulesDto>("/api/characters/rules", Json))!;
        var role = rules.Roles.Single(r => r.Id == roleId);

        var c = await Create(CreationMethod.Streetrat, roleId, $"Streetrat {roleId}");
        c = await Post(c, "creation/template-row", new TemplateRowRequest(c.Version, 1));
        foreach (var skill in role.ChoiceSkills)
            c = await Post(c, "creation/skill-choice", new SkillChoiceRequest(c.Version, skill, "Guitar"));
        var choices = role.Starting.SelectMany(s => s.Groups.Where(g => g.Options.Count > 1).Select(g => $"{s.Key}:{g.Index}"))
            .ToDictionary(k => k, _ => 0);
        c = await Save(c, c.Sheet with { Creation = c.Sheet.Creation with { StartingChoices = choices } });
        c.Validation.CanFinalize.Should().BeTrue(string.Join(" | ", c.Validation.Issues.Select(i => i.Message)));

        c = await Post(c, "creation/finalize", new VersionRequest(c.Version));
        c.Status.Should().Be(CharacterStatus.Complete);
        c.Report.Humanity.Lost.Should().Be(role.Starting.Single(s => s.Key == "cyberware").HumanityLoss);
        c.Validation.Issues.Should().NotContain(i => i.Severity == Application.Characters.Validation.CharacterIssueSeverity.Error);
    }

    [Fact]
    public async Task Nomad_streetrat_installs_wolvers_in_a_meat_arm_without_a_cyberarm()
    {
        var c = await Create(CreationMethod.Streetrat, "Nomad", "Santiago");
        c = await Post(c, "creation/template-row", new TemplateRowRequest(c.Version, 1));
        c = await Save(c, c.Sheet with
        {
            Creation = c.Sheet.Creation with
            {
                StartingChoices = new Dictionary<string, int> { ["weapons:0"] = 0, ["weapons:1"] = 0, ["weapons:2"] = 0, ["cyberware:0"] = 1 },
            },
        });
        c = await Post(c, "creation/finalize", new VersionRequest(c.Version));

        c.Report.Cyberware.Select(x => x.Slug).Should().BeEquivalentTo(["wolvers", "neural-link"]);
        c.Report.Cyberware.Single(x => x.Slug == "wolvers").MeatLimb.Should().BeTrue();
        c.Report.Humanity.Lost.Should().Be(14);
    }

    [Fact]
    public async Task Edgerunner_rolls_each_stat_and_spends_86_points_on_role_skills()
    {
        var c = await Create(CreationMethod.Edgerunner, "Netrunner", "Spider");
        c.Report.Skills.Where(s => s.RoleSkill && s.Level > 0).Should().HaveCount(20);

        var rolls = new Dictionary<string, int> { ["INT"] = 1, ["REF"] = 2, ["DEX"] = 3, ["TECH"] = 4, ["COOL"] = 5, ["WILL"] = 6, ["LUCK"] = 7, ["MOVE"] = 8, ["BODY"] = 9, ["EMP"] = 10 };
        c = await Post(c, "creation/stat-rolls", new StatRollsRequest(c.Version, rolls));
        c.Sheet.Stats["INT"].Should().Be(5);   // row 1, INT column
        c.Sheet.Stats["EMP"].Should().Be(6);   // row 10, EMP column

        // 20 skills at 2 = 40 (x2 Electronics/Security costs 4 → 42); raise to spend exactly 86.
        var skills = new Dictionary<string, int>(c.Sheet.Skills);
        foreach (var key in new[] { "basic-tech", "conceal-reveal-object", "cryptography", "cybertech", "handgun", "library-search", "education", "evasion", "stealth" })
            skills[key] = 6; // +4 each = 36 → 78
        skills["electronics-security-tech"] = 6; // +4 levels × 2 = 8 → 86
        c = await Save(c, c.Sheet with
        {
            Skills = skills,
            Creation = c.Sheet.Creation with
            {
                StartingChoices = new Dictionary<string, int> { ["outfit:5"] = 0, ["outfit:6"] = 1, ["outfit:7"] = 0 },
            },
        });
        c.Validation.Skills!.Used.Should().Be(86);
        c.Validation.CanFinalize.Should().BeTrue(string.Join(" | ", c.Validation.Issues.Select(i => i.Message)));

        c = await Post(c, "creation/finalize", new VersionRequest(c.Version));
        c.Status.Should().Be(CharacterStatus.Complete);
        c.Report.Cyberware.Select(x => x.Slug).Should().Contain(["interface-plugs", "neural-link", "shift-tacts"]);
        c.Report.Inventory.Should().Contain(i => i.Slug == "cyberdeck-standard");
    }

    [Fact]
    public async Task Complete_package_buys_with_two_separate_budgets_and_settles_them_on_finalize()
    {
        var c = await Create(CreationMethod.CompletePackage, "Solo", "Jay CP");
        c.Validation.Stats!.Used.Should().Be(60);
        var stats = new Dictionary<string, int>(c.Sheet.Stats) { ["REF"] = 8 };
        var skills = new Dictionary<string, int>(c.Sheet.Skills);
        foreach (var key in new[] { "handgun", "shoulder-arms", "melee-weapon", "tactics", "interrogation", "resist-torture-drugs", "perception", "evasion", "stealth" })
            skills[key] = 6;
        skills["autofire"] = 5; // 26 + 9 skills (6 new × 6 + 3 basics × 4 = 48) + 10 = 84
        skills["brawling"] = 4;  // +2 → 86
        c = await Save(c, c.Sheet with { Stats = stats, Skills = skills });
        c.Validation.Stats!.Used.Should().Be(62);
        c.Validation.Skills!.Used.Should().Be(86);

        var rules = (await _client.GetFromJsonAsync<CharacterRulesDto>("/api/characters/rules", Json))!;
        var ids = rules.Roles.SelectMany(r => r.Starting).SelectMany(s => s.Groups).SelectMany(g => g.Options).SelectMany(o => o.Items)
            .Where(i => i.ItemId != null).GroupBy(i => i.Slug).ToDictionary(g => g.Key, g => g.First().ItemId!.Value);

        c = await Post(c, "purchase", new PurchaseRequest(c.Version, ids["assault-rifle-padrao"]));
        c = await Post(c, "purchase", new PurchaseRequest(c.Version, ids["neural-link"]));
        c = await Post(c, "purchase", new PurchaseRequest(c.Version, ids["sandevistan"]));
        c = await Post(c, "purchase", new PurchaseRequest(c.Version, ids["roupa-leisurewear-jacket"]));
        c = await Post(c, "purchase", new PurchaseRequest(c.Version, ids["basic-ammo"], 3, Caliber.Rifle));
        c.Validation.Gear!.Used.Should().Be(500 + 500 + 500 + 30);
        c.Validation.Fashion!.Used.Should().Be(100);
        c.Sheet.Inventory.Single(i => i.Caliber == Caliber.Rifle).Rounds.Should().Be(30);
        c.Report.Humanity.Lost.Should().Be(14, "HL pré-definida durante a criação");

        // 18. above the budget → refused and nothing changes
        var refused = await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/purchase", new PurchaseRequest(c.Version, ids["assault-rifle-padrao"], 3));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetFromJsonAsync<CharacterDetailDto>($"/api/characters/{c.Id}", Json))!.Version.Should().Be(c.Version);

        // 19. a general item can't come out of the fashion budget
        var misuse = await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/purchase",
            new PurchaseRequest(c.Version, ids["agent"], 1, null, BudgetPool.CreationFashion));
        misuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var rifle = c.Sheet.Inventory.Single(i => i.ItemId == ids["assault-rifle-padrao"]);
        c = await Post(c, "purchase/undo", new InstanceRequest(c.Version, rifle.InstanceId));
        c.Validation.Gear!.Used.Should().Be(1030);

        c = await Post(c, "creation/finalize", new VersionRequest(c.Version));
        c.Sheet.Wallet.Balance.Should().Be(2550 - 1030, "a sobra do orçamento geral fica; a de moda se perde");
        var events = (await _client.GetFromJsonAsync<List<CharacterEventDto>>($"/api/characters/{c.Id}/events?kind=Ledger", Json))!;
        events.Should().Contain(e => e.Action == "fashion-budget-lost");
    }

    [Fact]
    public async Task Finalizing_twice_never_hands_over_the_package_again()
    {
        var c = await FinalizedSolo("Duas vezes");
        var again = await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/creation/finalize", new VersionRequest(c.Version));
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reloaded = (await _client.GetFromJsonAsync<CharacterDetailDto>($"/api/characters/{c.Id}", Json))!;
        reloaded.Sheet.Inventory.Should().HaveCount(c.Sheet.Inventory.Count);
        reloaded.Sheet.Wallet.Balance.Should().Be(500);
        reloaded.Report.Humanity.Lost.Should().Be(14);
    }

    [Fact]
    public async Task Improvement_points_raise_skills_and_roles_through_the_api()
    {
        var c = await FinalizedSolo("Veterano");
        c = await Post(c, "progression/ip", new AwardIpRequest(c.Version, 500, "Campanha"));
        c = await Post(c, "progression/skill", new ImproveSkillRequest(c.Version, "handgun"));
        c.Sheet.Skills["handgun"].Should().Be(7);
        c = await Post(c, "progression/role", new ImproveRoleRequest(c.Version, "Solo"));
        c.Report.Roles.Single().Rank.Should().Be(5);
        c.Sheet.Progress.Ip.Should().Be(500 - 140 - 300, "Handgun 6→7 custa 140 e Rank 4→5 custa 300 (CP:R p.410)");
        var events = (await _client.GetFromJsonAsync<List<CharacterEventDto>>($"/api/characters/{c.Id}/events?kind=Progression", Json))!;
        events.Count(e => e.Action == "spend-ip").Should().Be(2);
    }

    [Fact]
    public async Task Finalize_is_refused_while_there_are_blocking_errors()
    {
        var c = await Create(CreationMethod.Streetrat, "Solo");
        var response = await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/creation/finalize", new VersionRequest(c.Version));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("pendência");
    }

    // ------------------------------------------------------------ 24–28. persistence and isolation

    [Fact]
    public async Task Characters_are_isolated_and_survive_a_restart()
    {
        var a = await FinalizedSolo("Alpha");
        var b = await FinalizedSolo("Bravo");

        a = await Post(a, "combat/damage", new DamageRequest(a.Version, 20));
        a = await Post(a, "balance", new AdjustBalanceRequest(a.Version, 250, "Pagamento do Fixer"));

        var bNow = (await _client.GetFromJsonAsync<CharacterDetailDto>($"/api/characters/{b.Id}", Json))!;
        bNow.Report.Health.CurrentHp.Should().Be(bNow.Report.Health.MaxHp);
        bNow.Sheet.Wallet.Balance.Should().Be(500);
        bNow.Sheet.Inventory.Select(i => i.InstanceId).Should().Equal(b.Sheet.Inventory.Select(i => i.InstanceId));

        using var restarted = new ApiFactory(factory.DbPath) { KeepDatabase = true };
        var client = restarted.CreateClient();
        var reloaded = (await client.GetFromJsonAsync<CharacterDetailDto>($"/api/characters/{a.Id}", Json))!;
        reloaded.Report.Health.CurrentHp.Should().Be(a.Report.Health.CurrentHp);
        reloaded.Sheet.Wallet.Balance.Should().Be(750);
        reloaded.Version.Should().Be(a.Version);
        var list = (await client.GetFromJsonAsync<List<CharacterSummaryDto>>("/api/characters", Json))!;
        list.Select(x => x.Id).Should().Contain([a.Id, b.Id]);
    }

    [Fact]
    public async Task Stale_versions_are_rejected()
    {
        var c = await Create(CreationMethod.CompletePackage, "Fixer");
        await Save(c, c.Sheet with { Notes = "primeira janela" });
        var stale = await Raw(HttpMethod.Put, $"/api/characters/{c.Id}", new SaveSheetRequest(c.Version, c.Sheet with { Notes = "segunda janela" }));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Duplicate_copies_the_sheet_and_delete_removes_only_one_character()
    {
        var original = await FinalizedSolo("Original");
        var copy = await Send(HttpMethod.Post, $"/api/characters/{original.Id}/duplicate", null, HttpStatusCode.Created);

        copy.Id.Should().NotBe(original.Id);
        copy.Sheet.Identity.Name.Should().Be("Original (cópia)");
        copy.Sheet.Cyberware.Should().HaveCount(original.Sheet.Cyberware.Count);
        var copyEvents = (await _client.GetFromJsonAsync<List<CharacterEventDto>>($"/api/characters/{copy.Id}/events", Json))!;
        copyEvents.Should().ContainSingle(e => e.Action == "duplicate");

        (await _client.DeleteAsync($"/api/characters/{copy.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/characters/{copy.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/characters/{original.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task After_creation_stats_change_only_with_a_logged_manual_reason()
    {
        var c = await FinalizedSolo("Ajuste");
        var raised = c.Sheet with { Stats = new Dictionary<string, int>(c.Sheet.Stats) { ["COOL"] = 8 } };

        (await Raw(HttpMethod.Put, $"/api/characters/{c.Id}", new SaveSheetRequest(c.Version, raised))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        c = await Save(c, raised, "Recompensa do Mestre");
        c.Sheet.Stats["COOL"].Should().Be(8);
        var events = (await _client.GetFromJsonAsync<List<CharacterEventDto>>($"/api/characters/{c.Id}/events?kind=Edit", Json))!;
        events.Should().Contain(e => e.Action == "manual-adjustment" && e.IsManual);
    }

    [Fact]
    public async Task Combat_flow_damages_ablates_and_resolves_death_saves()
    {
        var c = await FinalizedSolo("Alvo");
        var maxHp = c.Report.Health.MaxHp;

        // 21. 15 on the body against SP 11: 4 through, armor ablated to 10
        c = await Post(c, "combat/damage", new DamageRequest(c.Version, 15));
        c.Report.Health.CurrentHp.Should().Be(maxHp - 4);
        c.Report.Armor.Body.Sp.Should().Be(10);
        c.Report.Armor.Head.Sp.Should().Be(11);

        c = await Post(c, "combat/damage", new DamageRequest(c.Version, 200, BypassArmor: true));
        c.Report.Health.WoundState.Should().Be(WoundState.Mortally);
        c.Notices.Should().NotBeEmpty();

        c = await Post(c, "combat/death-save", new DeathSaveRequest(c.Version, 1));
        c.Report.Health.DeathSavePenalty.Should().Be(1);
        c.Sheet.Combat.Conditions.Should().NotContain("Morto");

        (await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/combat/heal", new HealRequest(c.Version, 5))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "precisa estabilizar antes");

        c = await Post(c, "combat/stabilize", new VersionRequest(c.Version));
        c.Report.Health.CurrentHp.Should().Be(1);
        c.Report.Health.DeathSavePenalty.Should().Be(0);

        // 23. Critical Injury with bonus damage and Quick Fix
        c = await Post(c, "injuries", new AddInjuryRequest(c.Version, InjuryLocation.Body, "Broken Leg", ApplyBonusDamage: false));
        var injury = c.Sheet.Injuries.Single();
        c.Report.Stats.Single(s => s.Key == "MOVE").Effective.Should().BeLessThan(c.Sheet.Stats["MOVE"]);
        c = await Post(c, $"injuries/{injury.Id}", new UpdateInjuryRequest(c.Version, InjuryStatus.QuickFixed, "Paramedic DV13"));
        c.Sheet.Injuries.Single().Status.Should().Be(InjuryStatus.QuickFixed);
        c.Report.ActiveInjuries.Should().BeEmpty();
    }

    [Fact]
    public async Task Lifepath_relationships_and_housing_are_saved_and_monthly_costs_are_paid_from_the_balance()
    {
        var c = await FinalizedSolo("Suzu");
        c.Report.Housing.Housing.Should().Be("Cargo Container");
        c.Report.Housing.MonthlyTotal.Should().Be(1100);
        c.Report.Housing.Status.Should().Be(PaymentStatus.Paid);

        c = await Save(c, c.Sheet with
        {
            Lifepath = new LifepathState
            {
                CulturalRegion = "Western European",
                Answers = new() { ["personality"] = "Stable and serious", ["valueMost"] = "Your word" },
                Relationships =
                [
                    new Relationship { Kind = RelationshipKind.Friend, Name = "Vendedor de pipoca", Relation = "Someone you know from The Street." },
                    new Relationship
                    {
                        Kind = RelationshipKind.Enemy, Name = "Booster ganger", Cause = "Caused the loss of lover, friend, or relative.",
                        Resources = "An entire gang (at least 1d10 + 5 people).", Intent = "Set out to murder or maim them.",
                    },
                ],
            },
            RoleLifepath = new() { ["Solo"] = new() { ["type"] = "Bodyguard", ["territory"] = "Combat Zones" } },
            Style = new StyleState { ClothingStyle = "Urban Flash", Hairstyle = "Wolf cut", Appearance = "Mirrorshades" },
            Housing = c.Sheet.Housing with { Location = "Combat Zone" },
        });

        c.Sheet.Lifepath.Relationships.Should().HaveCount(2).And.OnlyContain(r => r.Id.Length > 0);
        c.Sheet.RoleLifepath["Solo"]["type"].Should().Be("Bodyguard");

        // 500eb don't cover the 1,100eb month: refused, nothing changes.
        var refused = await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/housing/pay", new PayMonthsRequest(c.Version));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reloaded = (await _client.GetFromJsonAsync<CharacterDetailDto>($"/api/characters/{c.Id}", Json))!;
        reloaded.Sheet.Wallet.Balance.Should().Be(500);
        reloaded.Sheet.Lifepath.Answers["valueMost"].Should().Be("Your word");
        reloaded.Sheet.Style.Hairstyle.Should().Be("Wolf cut");
    }

    [Fact]
    public async Task Paying_the_month_with_enough_money_moves_the_paid_until_date()
    {
        var c = await FinalizedSolo("Inquilina");
        c = await Post(c, "balance", new AdjustBalanceRequest(c.Version, 2000, "Pagamento de um trabalho"));
        var before = c.Sheet.Housing.PaidUntil!.Value;

        c = await Post(c, "housing/pay", new PayMonthsRequest(c.Version));
        c.Sheet.Wallet.Balance.Should().Be(2500 - 1100);
        c.Sheet.Housing.PaidUntil.Should().Be(before.AddMonths(1));

        var ledger = (await _client.GetFromJsonAsync<List<CharacterEventDto>>($"/api/characters/{c.Id}/events?kind=Ledger", Json))!;
        ledger.Select(e => e.Action).Should().Contain(["monthly-payment", "manual-adjustment", "starting-cash"]);
    }

    [Fact]
    public async Task Weapons_spend_and_reload_ammunition_from_the_inventory()
    {
        var c = await FinalizedSolo("Atirador");
        var pistol = c.Report.Weapons.Single(w => w.Name == "Very Heavy Pistol");

        c = await Post(c, "weapons/reload", new ReloadRequest(c.Version, pistol.InstanceId));
        var loaded = c.Report.Weapons.Single(w => w.InstanceId == pistol.InstanceId).LoadedRounds;
        loaded.Should().Be(pistol.Magazine!.Value);
        c.Sheet.Inventory.Single(i => i.Caliber == Caliber.VeryHeavyPistol).Rounds.Should().Be(30 - loaded);

        c = await Post(c, "weapons/fire", new FireRequest(c.Version, pistol.InstanceId));
        c.Report.Weapons.Single(w => w.InstanceId == pistol.InstanceId).LoadedRounds.Should().Be(loaded - 1);

        (await Raw(HttpMethod.Post, $"/api/characters/{c.Id}/weapons/fire", new FireRequest(c.Version, pistol.InstanceId, FireMode.Autofire)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "pistola não tem Autofire");
    }
}
