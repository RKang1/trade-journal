using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeJournal.Data;
using TradeJournal.Services.Auth;
using TradeJournal.Services.Trades;

namespace TradeJournal.Services;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddTradeJournalServices(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

		services.AddDbContext<TradeJournalDbContext>(options =>
		{
			var conn = configuration.GetConnectionString("Postgres");
			if (string.IsNullOrWhiteSpace(conn))
			{
				throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
			}
			options.UseNpgsql(conn);
		});

		services.AddSingleton(TimeProvider.System);
		services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
		services.AddScoped<IAuthService, AuthService>();
		services.AddScoped<ITradeService, TradeService>();

		return services;
	}
}
