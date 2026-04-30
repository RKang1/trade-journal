using System.ComponentModel.DataAnnotations;

namespace TradeJournal.Api.Contracts;

public record GoogleSignInRequest([Required] string IdToken);
public record CreateApiTokenRequest([Required] string Name);

public record UserProfileDto(Guid Id, string Email, string DisplayName);
public record ApiTokenDto(
	Guid Id,
	string Name,
	string Prefix,
	DateTimeOffset CreatedAt,
	DateTimeOffset? LastUsedAt);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserProfileDto User);
public record CreateApiTokenResponse(string Token, ApiTokenDto Details);
