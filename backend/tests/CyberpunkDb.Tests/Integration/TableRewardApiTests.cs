using System.Net;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// End-of-session reward: eb and IP to several sheets at once, with a chat line
[Collection(ApiCollection.Name)]
public class TableRewardApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private Task<CharacterDetailDto> Get(int id) => _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Get, $"/api/characters/{id}");

    [Fact]
    public async Task Reward_pays_every_chosen_character_and_posts_in_the_chat()
    {
        var a = await _t.FinalizedSolo("Recompensa A");
        var b = await _t.FinalizedSolo("Recompensa B");

        var result = await _t.Send<TableRewardResult>(_t.Gm, HttpMethod.Post, "/api/table/reward",
            new TableRewardRequest([a.Id, b.Id], 500, 10, "Sessão 12"));

        result.Names.Should().HaveCount(2);
        foreach (var before in new[] { a, b })
        {
            var after = await Get(before.Id);
            after.Sheet.Wallet.Balance.Should().Be(before.Sheet.Wallet.Balance + 500);
            after.Sheet.Progress.Ip.Should().Be(before.Sheet.Progress.Ip + 10);
            after.Sheet.Progress.IpEarned.Should().Be(before.Sheet.Progress.IpEarned + 10);
            after.Version.Should().Be(before.Version + 1, "money and IP go in one save");
        }
        var chat = await _t.Send<List<TableMessageDto>>(_t.Gm, HttpMethod.Get, "/api/table/messages");
        chat.Last().Text.Should().Contain("Sessão 12").And.Contain("500 eb").And.Contain("10 IP");
    }

    [Fact]
    public async Task Reward_with_only_IP_leaves_the_balance_alone()
    {
        var a = await _t.FinalizedSolo("Só IP");

        await _t.Send<TableRewardResult>(_t.Gm, HttpMethod.Post, "/api/table/reward", new TableRewardRequest([a.Id], 0, 5, "Missão"));

        var after = await Get(a.Id);
        after.Sheet.Wallet.Balance.Should().Be(a.Sheet.Wallet.Balance);
        after.Sheet.Progress.Ip.Should().Be(a.Sheet.Progress.Ip + 5);
    }

    [Fact]
    public async Task Reward_refuses_everyone_when_one_is_still_in_creation()
    {
        var done = await _t.FinalizedSolo("Pronto");
        var creating = await _t.Send<CharacterDetailDto>(_t.Gm, HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = "Criando", Method = CreationMethod.Streetrat, Role = "Solo", NativeLanguage = "English", Home = "Watson" },
            expected: HttpStatusCode.Created);

        var response = await _t.Raw(_t.Gm, HttpMethod.Post, "/api/table/reward", new TableRewardRequest([done.Id, creating.Id], 100, 0, "Sessão"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Get(done.Id)).Sheet.Wallet.Balance.Should().Be(done.Sheet.Wallet.Balance, "nobody is paid when one can't be");
    }

    [Theory]
    [InlineData(0, 0, "Sessão")]
    [InlineData(-100, 0, "Sessão")]
    [InlineData(100, 0, " ")]
    public async Task Reward_refuses_empty_negative_or_unexplained(int money, int ip, string reason)
    {
        var a = await _t.FinalizedSolo("Inválida");

        var response = await _t.Raw(_t.Gm, HttpMethod.Post, "/api/table/reward", new TableRewardRequest([a.Id], money, ip, reason));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Players_port_cannot_reward()
    {
        var a = await _t.FinalizedSolo("Porta");

        var response = await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/reward", new TableRewardRequest([a.Id], 100, 0, "Sessão"));

        response.IsSuccessStatusCode.Should().BeFalse();
    }
}
