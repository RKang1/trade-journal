namespace TradeJournal.Services.Auth;

public interface IAuthService
{
	Task<AuthResult> SignInWithGoogleAsync(GoogleSignInCommand command, CancellationToken cancellationToken);
	Task<UserProfileResult> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<ApiTokenResult>> ListApiTokensAsync(Guid userId, CancellationToken cancellationToken);
	Task<CreatedApiTokenResult> CreateApiTokenAsync(Guid userId, CreateApiTokenCommand command, CancellationToken cancellationToken);
	Task RevokeApiTokenAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken);
	Task<ApiTokenPrincipalResult?> AuthenticateApiTokenAsync(string token, CancellationToken cancellationToken);
}
