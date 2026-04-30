namespace TradeJournal.Services.Auth;

public static class AuthClaimTypes
{
	public const string AuthType = "trade_journal:auth_type";
	public const string ApiTokenId = "trade_journal:api_token_id";
}

public static class AuthClaimValues
{
	public const string Jwt = "jwt";
	public const string ApiToken = "api_token";
}
