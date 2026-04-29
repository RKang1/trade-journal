namespace TradeJournal.Services.Auth;

public record GoogleSignInCommand(string IdToken);

public record UserProfileResult(Guid Id, string Email, string DisplayName);

public record AuthResult(string Token, DateTimeOffset ExpiresAt, UserProfileResult User);
