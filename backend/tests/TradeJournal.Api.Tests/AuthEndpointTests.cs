using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradeJournal.Api.Contracts;
using TradeJournal.Data;
using TradeJournal.Data.Entities;

namespace TradeJournal.Api.Tests;

public class AuthEndpointTests : IClassFixture<TestWebAppFactory>
{
	private readonly TestWebAppFactory _factory;

	public AuthEndpointTests(TestWebAppFactory factory)
	{
		_factory = factory;
	}

	private static async Task<AuthResponse> SignInAsync(HttpClient client, string subject)
	{
		var signIn = await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInRequest(subject));
		signIn.StatusCode.Should().Be(HttpStatusCode.OK);

		var auth = await signIn.Content.ReadFromJsonAsync<AuthResponse>(JsonHelpers.Options);
		auth.Should().NotBeNull();
		auth!.Token.Should().NotBeNullOrWhiteSpace();
		return auth;
	}

	[Fact]
	public async Task ProtectedEndpoint_RequiresJwt()
	{
		var client = _factory.CreateClient();
		var response = await client.GetAsync("/api/trades");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GoogleSignIn_CreatesLocalUser_AndAuthMeReturnsProfile()
	{
		var client = _factory.CreateClient();
		var auth = await SignInAsync(client, "alice");
		auth.User.Email.Should().Be("alice@example.com");

		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
		var me = await client.GetFromJsonAsync<UserProfileDto>("/api/auth/me");
		me.Should().NotBeNull();
		me!.Email.Should().Be("alice@example.com");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TradeJournalDbContext>();
		var users = await db.Users.ToListAsync();
		users.Should().ContainSingle(u => u.GoogleSubject == "sub-alice");
	}

	[Fact]
	public async Task GoogleSignIn_SecondCall_UpdatesExistingUser()
	{
		var client = _factory.CreateClient();
		await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInRequest("repeat"));
		await client.PostAsJsonAsync("/api/auth/google", new GoogleSignInRequest("repeat"));

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TradeJournalDbContext>();
		var matching = await db.Users.Where(u => u.GoogleSubject == "sub-repeat").ToListAsync();
		matching.Should().HaveCount(1);
	}

	[Fact]
	public async Task InteractiveUser_CanCreateListAndRevokeApiTokens()
	{
		var client = _factory.CreateClient();
		var auth = await SignInAsync(client, "token-owner");
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

		var create = await client.PostAsJsonAsync("/api/auth/tokens", new CreateApiTokenRequest("Day trader sync"));
		create.StatusCode.Should().Be(HttpStatusCode.OK);

		var created = await create.Content.ReadFromJsonAsync<CreateApiTokenResponse>(JsonHelpers.Options);
		created.Should().NotBeNull();
		created!.Token.Should().StartWith("tj_pat_");
		created.Details.Name.Should().Be("Day trader sync");

		var tokens = await client.GetFromJsonAsync<List<ApiTokenDto>>("/api/auth/tokens", JsonHelpers.Options);
		tokens.Should().ContainSingle(token => token.Id == created.Details.Id && token.Prefix == created.Details.Prefix);

		var revoke = await client.DeleteAsync($"/api/auth/tokens/{created.Details.Id}");
		revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var remaining = await client.GetFromJsonAsync<List<ApiTokenDto>>("/api/auth/tokens", JsonHelpers.Options);
		remaining.Should().BeEmpty();
	}

	[Fact]
	public async Task ApiToken_CanCallProtectedUserTradeEndpoints()
	{
		var interactiveClient = _factory.CreateClient();
		var auth = await SignInAsync(interactiveClient, "api-user");
		interactiveClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

		var createToken = await interactiveClient.PostAsJsonAsync("/api/auth/tokens", new CreateApiTokenRequest("External app"));
		var apiToken = (await createToken.Content.ReadFromJsonAsync<CreateApiTokenResponse>(JsonHelpers.Options))!;

		var apiClient = _factory.CreateClient();
		apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken.Token);

		var me = await apiClient.GetFromJsonAsync<UserProfileDto>("/api/auth/me", JsonHelpers.Options);
		me.Should().NotBeNull();
		me!.Email.Should().Be(auth.User.Email);

		var createTrade = await apiClient.PostAsJsonAsync("/api/trades", new CreateTradeRequest(
			Symbol: "NVDA",
			Side: TradeSide.Long,
			EntryAt: DateTimeOffset.UtcNow.AddMinutes(-3),
			EntryPrice: 901.25m,
			Quantity: 1m,
			Fees: null,
			Setup: "breakout",
			Notes: "created over API token"));
		createTrade.StatusCode.Should().Be(HttpStatusCode.Created);
	}

	[Fact]
	public async Task ApiToken_CannotManageOtherApiTokens()
	{
		var interactiveClient = _factory.CreateClient();
		var auth = await SignInAsync(interactiveClient, "pat-limited");
		interactiveClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

		var createToken = await interactiveClient.PostAsJsonAsync("/api/auth/tokens", new CreateApiTokenRequest("External app"));
		var apiToken = (await createToken.Content.ReadFromJsonAsync<CreateApiTokenResponse>(JsonHelpers.Options))!;

		var apiClient = _factory.CreateClient();
		apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken.Token);

		var response = await apiClient.GetAsync("/api/auth/tokens");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task RevokedApiToken_IsRejectedFromProtectedEndpoints()
	{
		var interactiveClient = _factory.CreateClient();
		var auth = await SignInAsync(interactiveClient, "revoke-me");
		interactiveClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

		var createToken = await interactiveClient.PostAsJsonAsync("/api/auth/tokens", new CreateApiTokenRequest("External app"));
		var apiToken = (await createToken.Content.ReadFromJsonAsync<CreateApiTokenResponse>(JsonHelpers.Options))!;

		var revoke = await interactiveClient.DeleteAsync($"/api/auth/tokens/{apiToken.Details.Id}");
		revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var apiClient = _factory.CreateClient();
		apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken.Token);

		var response = await apiClient.GetAsync("/api/trades");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}
