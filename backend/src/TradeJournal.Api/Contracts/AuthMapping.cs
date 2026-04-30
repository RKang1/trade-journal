using TradeJournal.Services.Auth;

namespace TradeJournal.Api.Contracts;

public static class AuthMapping
{
	public static ApiTokenDto ToDto(this ApiTokenResult result) => new(
		result.Id,
		result.Name,
		result.Prefix,
		result.CreatedAt,
		result.LastUsedAt);
}
