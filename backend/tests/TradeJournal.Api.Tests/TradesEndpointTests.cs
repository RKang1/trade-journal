using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using TradeJournal.Api.Contracts;
using TradeJournal.Data.Entities;

namespace TradeJournal.Api.Tests;

public class TradesEndpointTests : IClassFixture<TestWebAppFactory>
{
	private readonly TestWebAppFactory _factory;

	public TradesEndpointTests(TestWebAppFactory factory)
	{
		_factory = factory;
	}

	private async Task<HttpClient> SignInAsync(string subject)
	{
		var client = _factory.CreateClient();
		var resp = await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInRequest(subject));
		resp.EnsureSuccessStatusCode();
		var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonHelpers.Options))!;
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
		return client;
	}

	[Fact]
	public async Task TradeList_ReturnsOnlyAuthenticatedUsersTrades()
	{
		var alice = await SignInAsync("alice");
		var bob = await SignInAsync("bob");

		var aliceCreate = await alice.PostAsJsonAsync("/api/trades", new CreateTradeRequest(
			Symbol: "AAA",
			Side: TradeSide.Long,
			EntryAt: DateTimeOffset.UtcNow.AddMinutes(-10),
			EntryPrice: 50m,
			Quantity: 5m,
			Fees: null,
			Setup: null,
			Notes: null));
		aliceCreate.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

		var bobCreate = await bob.PostAsJsonAsync("/api/trades", new CreateTradeRequest(
			Symbol: "BBB",
			Side: TradeSide.Short,
			EntryAt: DateTimeOffset.UtcNow.AddMinutes(-5),
			EntryPrice: 80m,
			Quantity: 2m,
			Fees: null,
			Setup: null,
			Notes: null));
		bobCreate.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

		var aliceTrades = await alice.GetFromJsonAsync<List<TradeDto>>("/api/trades", JsonHelpers.Options);
		var bobTrades = await bob.GetFromJsonAsync<List<TradeDto>>("/api/trades", JsonHelpers.Options);

		aliceTrades.Should().ContainSingle().Which.Symbol.Should().Be("AAA");
		bobTrades.Should().ContainSingle().Which.Symbol.Should().Be("BBB");
	}

	[Fact]
	public async Task CreateThenClose_PersistsRealizedPnl()
	{
		var client = await SignInAsync("trader");

		var create = await client.PostAsJsonAsync("/api/trades", new CreateTradeRequest(
			Symbol: "MSFT",
			Side: TradeSide.Long,
			EntryAt: DateTimeOffset.UtcNow.AddMinutes(-30),
			EntryPrice: 200m,
			Quantity: 5m,
			Fees: 0m,
			Setup: null,
			Notes: null));
		create.EnsureSuccessStatusCode();
		var trade = (await create.Content.ReadFromJsonAsync<TradeDto>(JsonHelpers.Options))!;

		var close = await client.PostAsJsonAsync($"/api/trades/{trade.Id}/close", new CloseTradeRequest(
			ExitAt: DateTimeOffset.UtcNow,
			ExitPrice: 220m,
			Fees: 1m));
		close.EnsureSuccessStatusCode();

		var closed = (await close.Content.ReadFromJsonAsync<TradeDto>(JsonHelpers.Options))!;
		closed.Status.Should().Be(TradeStatus.Closed);
		closed.RealizedPnl.Should().Be(99m);
	}

	[Fact]
	public async Task UpdateOrCloseAnotherUsersTrade_Returns404()
	{
		var alice = await SignInAsync("alice2");
		var create = await alice.PostAsJsonAsync("/api/trades", new CreateTradeRequest(
			Symbol: "AAPL",
			Side: TradeSide.Long,
			EntryAt: DateTimeOffset.UtcNow.AddMinutes(-10),
			EntryPrice: 100m,
			Quantity: 1m,
			Fees: null,
			Setup: null,
			Notes: null));
		var trade = (await create.Content.ReadFromJsonAsync<TradeDto>(JsonHelpers.Options))!;

		var bob = await SignInAsync("bob2");
		var close = await bob.PostAsJsonAsync($"/api/trades/{trade.Id}/close", new CloseTradeRequest(
			ExitAt: DateTimeOffset.UtcNow,
			ExitPrice: 110m,
			Fees: null));
		close.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}
