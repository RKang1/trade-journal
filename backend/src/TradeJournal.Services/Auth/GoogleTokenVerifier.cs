using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using TradeJournal.Services.Common;

namespace TradeJournal.Services.Auth;

public class GoogleTokenVerifier : IGoogleTokenVerifier
{
	private readonly AuthOptions _options;

	public GoogleTokenVerifier(IOptions<AuthOptions> options)
	{
		_options = options.Value;
	}

	public async Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(idToken))
		{
			throw new ValidationException("idToken is required.");
		}

		if (string.IsNullOrWhiteSpace(_options.GoogleClientId))
		{
			throw new InvalidOperationException("Auth:GoogleClientId is not configured.");
		}

		GoogleJsonWebSignature.Payload payload;
		try
		{
			var settings = new GoogleJsonWebSignature.ValidationSettings
			{
				Audience = new[] { _options.GoogleClientId },
			};
			payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
		}
		catch (InvalidJwtException ex)
		{
			throw new ValidationException($"Invalid Google ID token: {ex.Message}");
		}

		return new GoogleIdentity(
			Subject: payload.Subject,
			Email: payload.Email ?? string.Empty,
			Name: payload.Name ?? payload.Email ?? "User");
	}
}
