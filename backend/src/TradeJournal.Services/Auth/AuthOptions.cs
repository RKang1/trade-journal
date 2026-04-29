namespace TradeJournal.Services.Auth;

public class AuthOptions
{
	public const string SectionName = "Auth";

	public string GoogleClientId { get; set; } = string.Empty;
	public string JwtSigningKey { get; set; } = string.Empty;
	public string JwtIssuer { get; set; } = "trade-journal";
	public string JwtAudience { get; set; } = "trade-journal-frontend";
	public int JwtLifetimeMinutes { get; set; } = 60 * 24;
}
