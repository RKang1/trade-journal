namespace TradeJournal.Services.Auth;

public interface IAuthService
{
	Task<AuthResult> SignInWithGoogleAsync(GoogleSignInCommand command, CancellationToken cancellationToken);
	Task<UserProfileResult> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}
