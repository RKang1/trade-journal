using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TradeJournal.Api;

public static class CurrentUser
{
	public static Guid GetUserId(this ClaimsPrincipal principal)
	{
		var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
			?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

		if (Guid.TryParse(sub, out var id))
		{
			return id;
		}

		throw new UnauthorizedAccessException("Authenticated principal does not have a valid user id claim.");
	}
}
