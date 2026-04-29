using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeJournal.Data;
using TradeJournal.Services.Auth;
using TradeJournal.Services.Common;

namespace TradeJournal.Services.Tests;

public class AuthServiceTests
{
	private static TradeJournalDbContext NewDb() => new(
		new DbContextOptionsBuilder<TradeJournalDbContext>()
			.UseInMemoryDatabase($"auth-{Guid.NewGuid()}")
			.Options);

	private static AuthOptions Options() => new()
	{
		GoogleClientId = "test-client-id",
		JwtSigningKey = "test-signing-key-with-at-least-32-bytes-of-entropy",
		JwtIssuer = "trade-journal-test",
		JwtAudience = "trade-journal-test-frontend",
		JwtLifetimeMinutes = 60,
	};

	private sealed class StubVerifier : IGoogleTokenVerifier
	{
		public GoogleIdentity Identity { get; init; } = new("google-sub-1", "user@example.com", "User One");
		public Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken)
			=> Task.FromResult(Identity);
	}

	[Fact]
	public async Task SignInWithGoogle_FirstLogin_CreatesUserAndIssuesJwt()
	{
		await using var db = NewDb();
		var verifier = new StubVerifier();
		var service = new AuthService(db, verifier, Microsoft.Extensions.Options.Options.Create(Options()), TimeProvider.System);

		var result = await service.SignInWithGoogleAsync(new GoogleSignInCommand("any"), CancellationToken.None);

		result.Token.Should().NotBeNullOrWhiteSpace();
		result.User.Email.Should().Be("user@example.com");
		result.User.DisplayName.Should().Be("User One");

		var user = await db.Users.SingleAsync();
		user.GoogleSubject.Should().Be("google-sub-1");
		user.LastLoginAt.Should().NotBe(default);
	}

	[Fact]
	public async Task SignInWithGoogle_ReturningUser_UpdatesProfileAndLastLogin()
	{
		await using var db = NewDb();

		var firstVerifier = new StubVerifier
		{
			Identity = new("google-sub-2", "alice@example.com", "Alice"),
		};
		var first = new AuthService(db, firstVerifier, Microsoft.Extensions.Options.Options.Create(Options()), TimeProvider.System);
		await first.SignInWithGoogleAsync(new GoogleSignInCommand("token"), CancellationToken.None);

		var renamedVerifier = new StubVerifier
		{
			Identity = new("google-sub-2", "alice@example.com", "Alice Smith"),
		};
		var second = new AuthService(db, renamedVerifier, Microsoft.Extensions.Options.Options.Create(Options()), TimeProvider.System);
		await second.SignInWithGoogleAsync(new GoogleSignInCommand("token"), CancellationToken.None);

		var users = await db.Users.ToListAsync();
		users.Should().HaveCount(1);
		users[0].DisplayName.Should().Be("Alice Smith");
	}

	[Fact]
	public async Task SignInWithGoogle_RejectsBlankToken()
	{
		await using var db = NewDb();
		var verifier = new StubVerifier();
		var service = new AuthService(db, verifier, Microsoft.Extensions.Options.Options.Create(Options()), TimeProvider.System);

		await Assert.ThrowsAsync<ValidationException>(() =>
			service.SignInWithGoogleAsync(new GoogleSignInCommand(""), CancellationToken.None));
	}
}
