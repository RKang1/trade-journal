namespace TradeJournal.Services.Auth;

public record GoogleSignInCommand(string IdToken);
public record CreateApiTokenCommand(string Name);

public record UserProfileResult(Guid Id, string Email, string DisplayName);

public record AuthResult(string Token, DateTimeOffset ExpiresAt, UserProfileResult User);
public record ApiTokenResult(
	Guid Id,
	string Name,
	string Prefix,
	DateTimeOffset CreatedAt,
	DateTimeOffset? LastUsedAt);
public record CreatedApiTokenResult(string Token, ApiTokenResult Details);
public record ApiTokenPrincipalResult(Guid UserId, string Email, string DisplayName, Guid TokenId);
