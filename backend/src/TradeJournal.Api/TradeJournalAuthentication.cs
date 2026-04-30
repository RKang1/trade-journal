using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace TradeJournal.Api;

public static class TradeJournalAuthentication
{
	public const string DynamicBearerScheme = "DynamicBearer";
	public const string ApiTokenScheme = "ApiToken";
	public const string InteractiveUserPolicy = "InteractiveUser";

	public static string SelectScheme(HttpContext context)
	{
		var header = context.Request.Headers.Authorization.ToString();
		if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
		{
			var token = header["Bearer ".Length..].Trim();
			if (LooksLikeJwt(token))
			{
				return JwtBearerDefaults.AuthenticationScheme;
			}

			return ApiTokenScheme;
		}

		return JwtBearerDefaults.AuthenticationScheme;
	}

	private static bool LooksLikeJwt(string token)
	{
		var dotCount = 0;
		foreach (var ch in token)
		{
			if (ch == '.')
			{
				dotCount++;
			}
		}

		return dotCount == 2;
	}
}
