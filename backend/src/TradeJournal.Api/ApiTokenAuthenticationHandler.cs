using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TradeJournal.Services.Auth;

namespace TradeJournal.Api;

public class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
	private readonly IAuthService _auth;

	public ApiTokenAuthenticationHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder,
		IAuthService auth)
		: base(options, logger, encoder)
	{
		_auth = auth;
	}

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
		{
			return AuthenticateResult.NoResult();
		}

		var header = headerValues.ToString();
		if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
		{
			return AuthenticateResult.NoResult();
		}

		var token = header["Bearer ".Length..].Trim();
		if (string.IsNullOrWhiteSpace(token))
		{
			return AuthenticateResult.Fail("Missing bearer token.");
		}

		var principalResult = await _auth.AuthenticateApiTokenAsync(token, Context.RequestAborted);
		if (principalResult is null)
		{
			return AuthenticateResult.Fail("Invalid API token.");
		}

		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, principalResult.UserId.ToString()),
			new Claim(JwtRegisteredClaimNames.Email, principalResult.Email),
			new Claim(JwtRegisteredClaimNames.Name, principalResult.DisplayName),
			new Claim(AuthClaimTypes.AuthType, AuthClaimValues.ApiToken),
			new Claim(AuthClaimTypes.ApiTokenId, principalResult.TokenId.ToString()),
		};

		var identity = new ClaimsIdentity(claims, Scheme.Name);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, Scheme.Name);
		return AuthenticateResult.Success(ticket);
	}
}
