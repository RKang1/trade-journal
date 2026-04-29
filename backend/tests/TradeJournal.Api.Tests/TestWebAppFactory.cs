using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradeJournal.Data;
using TradeJournal.Services.Auth;

namespace TradeJournal.Api.Tests;

public class TestWebAppFactory : WebApplicationFactory<Program>
{
	public const string SigningKey = "test-signing-key-with-at-least-32-bytes-of-entropy";
	public const string Issuer = "trade-journal-test";
	public const string Audience = "trade-journal-test-frontend";

	public StubGoogleVerifier GoogleVerifier { get; } = new();

	private readonly string _databaseName = $"api-tests-{Guid.NewGuid()}";

	private static readonly IServiceProvider InMemoryEfServices =
		new ServiceCollection()
			.AddEntityFrameworkInMemoryDatabase()
			.BuildServiceProvider();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Testing");

		builder.ConfigureAppConfiguration((_, config) =>
		{
			config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Postgres"] = "Host=ignored",
				["Auth:GoogleClientId"] = "test-client-id",
				["Auth:JwtSigningKey"] = SigningKey,
				["Auth:JwtIssuer"] = Issuer,
				["Auth:JwtAudience"] = Audience,
				["Auth:JwtLifetimeMinutes"] = "60",
			});
		});

		builder.ConfigureServices(services =>
		{
			var dbDescriptors = services
				.Where(d => d.ServiceType == typeof(DbContextOptions<TradeJournalDbContext>)
						 || d.ServiceType == typeof(DbContextOptions))
				.ToList();
			foreach (var d in dbDescriptors)
			{
				services.Remove(d);
			}

			services.AddDbContext<TradeJournalDbContext>(opts =>
			{
				opts.UseInMemoryDatabase(_databaseName);
				opts.UseInternalServiceProvider(InMemoryEfServices);
			});

			services.RemoveAll<IGoogleTokenVerifier>();
			services.AddSingleton<IGoogleTokenVerifier>(GoogleVerifier);
		});
	}
}

public class StubGoogleVerifier : IGoogleTokenVerifier
{
	public Dictionary<string, GoogleIdentity> Identities { get; } = new();

	public Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken)
	{
		if (Identities.TryGetValue(idToken, out var identity))
		{
			return Task.FromResult(identity);
		}

		// Default: derive a deterministic identity from the token string.
		return Task.FromResult(new GoogleIdentity(
			Subject: $"sub-{idToken}",
			Email: $"{idToken}@example.com",
			Name: $"User {idToken}"));
	}
}
