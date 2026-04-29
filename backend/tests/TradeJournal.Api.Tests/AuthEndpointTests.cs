using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradeJournal.Api.Contracts;
using TradeJournal.Data;

namespace TradeJournal.Api.Tests;

public class AuthEndpointTests : IClassFixture<TestWebAppFactory>
{
	private readonly TestWebAppFactory _factory;

	public AuthEndpointTests(TestWebAppFactory factory)
	{
		_factory = factory;
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

		var signIn = await client.PostAsJsonAsync("/api/auth/google",
			new GoogleSignInRequest("alice"));
		signIn.StatusCode.Should().Be(HttpStatusCode.OK);

		var auth = await signIn.Content.ReadFromJsonAsync<AuthResponse>();
		auth.Should().NotBeNull();
		auth!.Token.Should().NotBeNullOrWhiteSpace();
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
}
