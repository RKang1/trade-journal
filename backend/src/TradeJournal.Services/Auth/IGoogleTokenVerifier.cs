namespace TradeJournal.Services.Auth;

public record GoogleIdentity(string Subject, string Email, string Name);

public interface IGoogleTokenVerifier
{
	Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken);
}
