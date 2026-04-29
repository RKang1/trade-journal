using System.ComponentModel.DataAnnotations;

namespace TradeJournal.Api.Contracts;

public record GoogleSignInRequest([Required] string IdToken);

public record UserProfileDto(Guid Id, string Email, string DisplayName);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserProfileDto User);
