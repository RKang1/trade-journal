using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TradeJournal.Data;
using TradeJournal.Data.Entities;
using TradeJournal.Services.Common;

namespace TradeJournal.Services.Auth;

public class AuthService : IAuthService
{
	private const string ApiTokenPrefix = "tj_pat_";
	private const int ApiTokenBytes = 32;
	private const int ApiTokenPreviewLength = 18;
	private static readonly TimeSpan ApiTokenLastUsedWriteInterval = TimeSpan.FromMinutes(5);

	private readonly TradeJournalDbContext _db;
	private readonly IGoogleTokenVerifier _googleVerifier;
	private readonly AuthOptions _options;
	private readonly TimeProvider _clock;

	public AuthService(
		TradeJournalDbContext db,
		IGoogleTokenVerifier googleVerifier,
		IOptions<AuthOptions> options,
		TimeProvider clock)
	{
		_db = db;
		_googleVerifier = googleVerifier;
		_options = options.Value;
		_clock = clock;
	}

	public async Task<AuthResult> SignInWithGoogleAsync(GoogleSignInCommand command, CancellationToken cancellationToken)
	{
		if (command is null || string.IsNullOrWhiteSpace(command.IdToken))
		{
			throw new ValidationException("idToken is required.");
		}

		var identity = await _googleVerifier.VerifyAsync(command.IdToken, cancellationToken);
		var now = _clock.GetUtcNow();

		var user = await _db.Users
			.FirstOrDefaultAsync(u => u.GoogleSubject == identity.Subject, cancellationToken);

		if (user is null)
		{
			user = new User
			{
				Id = Guid.NewGuid(),
				GoogleSubject = identity.Subject,
				Email = identity.Email,
				DisplayName = identity.Name,
				CreatedAt = now,
				LastLoginAt = now,
			};
			_db.Users.Add(user);
		}
		else
		{
			user.Email = identity.Email;
			user.DisplayName = identity.Name;
			user.LastLoginAt = now;
		}

		await _db.SaveChangesAsync(cancellationToken);

		var (token, expiresAt) = IssueJwt(user, now);

		return new AuthResult(
			Token: token,
			ExpiresAt: expiresAt,
			User: new UserProfileResult(user.Id, user.Email, user.DisplayName));
	}

	public async Task<UserProfileResult> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
	{
		var user = await _db.Users
			.AsNoTracking()
			.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
			?? throw new NotFoundException("User not found.");

		return new UserProfileResult(user.Id, user.Email, user.DisplayName);
	}

	public async Task<IReadOnlyList<ApiTokenResult>> ListApiTokensAsync(Guid userId, CancellationToken cancellationToken)
	{
		return await _db.ApiTokens
			.AsNoTracking()
			.Where(token => token.UserId == userId && token.RevokedAt == null)
			.OrderByDescending(token => token.CreatedAt)
			.Select(token => new ApiTokenResult(
				token.Id,
				token.Name,
				token.Prefix,
				token.CreatedAt,
				token.LastUsedAt))
			.ToListAsync(cancellationToken);
	}

	public async Task<CreatedApiTokenResult> CreateApiTokenAsync(
		Guid userId,
		CreateApiTokenCommand command,
		CancellationToken cancellationToken)
	{
		var name = NormalizeApiTokenName(command);
		var userExists = await _db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
		if (!userExists)
		{
			throw new NotFoundException("User not found.");
		}

		var now = _clock.GetUtcNow();
		var rawToken = CreateRawApiToken();
		var apiToken = new ApiToken
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Name = name,
			TokenHash = HashApiToken(rawToken),
			Prefix = rawToken[..Math.Min(ApiTokenPreviewLength, rawToken.Length)],
			CreatedAt = now,
		};

		_db.ApiTokens.Add(apiToken);
		await _db.SaveChangesAsync(cancellationToken);

		return new CreatedApiTokenResult(
			rawToken,
			new ApiTokenResult(apiToken.Id, apiToken.Name, apiToken.Prefix, apiToken.CreatedAt, apiToken.LastUsedAt));
	}

	public async Task RevokeApiTokenAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken)
	{
		var token = await _db.ApiTokens
			.FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId && t.RevokedAt == null, cancellationToken)
			?? throw new NotFoundException("API token not found.");

		token.RevokedAt = _clock.GetUtcNow();
		await _db.SaveChangesAsync(cancellationToken);
	}

	public async Task<ApiTokenPrincipalResult?> AuthenticateApiTokenAsync(string token, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return null;
		}

		var rawToken = token.Trim();
		if (!rawToken.StartsWith(ApiTokenPrefix, StringComparison.Ordinal))
		{
			return null;
		}

		var tokenHash = HashApiToken(rawToken);
		var match = await _db.ApiTokens
			.Include(apiToken => apiToken.User)
			.FirstOrDefaultAsync(
				apiToken => apiToken.TokenHash == tokenHash && apiToken.RevokedAt == null,
				cancellationToken);

		if (match is null)
		{
			return null;
		}

		var now = _clock.GetUtcNow();
		if (match.LastUsedAt is null || now - match.LastUsedAt.Value >= ApiTokenLastUsedWriteInterval)
		{
			match.LastUsedAt = now;
			await _db.SaveChangesAsync(cancellationToken);
		}

		return new ApiTokenPrincipalResult(match.UserId, match.User.Email, match.User.DisplayName, match.Id);
	}

	private (string token, DateTimeOffset expiresAt) IssueJwt(User user, DateTimeOffset issuedAt)
	{
		if (string.IsNullOrWhiteSpace(_options.JwtSigningKey))
		{
			throw new InvalidOperationException("Auth:JwtSigningKey is not configured.");
		}

		var keyBytes = Encoding.UTF8.GetBytes(_options.JwtSigningKey);
		if (keyBytes.Length < 32)
		{
			throw new InvalidOperationException("Auth:JwtSigningKey must be at least 32 bytes (256 bits) of entropy.");
		}

		var expiresAt = issuedAt.AddMinutes(_options.JwtLifetimeMinutes);

		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
			new Claim(JwtRegisteredClaimNames.Email, user.Email),
			new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
			new Claim(AuthClaimTypes.AuthType, AuthClaimValues.Jwt),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
		};

		var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

		var jwt = new JwtSecurityToken(
			issuer: _options.JwtIssuer,
			audience: _options.JwtAudience,
			claims: claims,
			notBefore: issuedAt.UtcDateTime,
			expires: expiresAt.UtcDateTime,
			signingCredentials: creds);

		var token = new JwtSecurityTokenHandler().WriteToken(jwt);
		return (token, expiresAt);
	}

	private static string NormalizeApiTokenName(CreateApiTokenCommand command)
	{
		var errors = new List<string>();
		var name = (command.Name ?? string.Empty).Trim();

		if (name.Length == 0)
		{
			errors.Add("API token name is required.");
		}

		if (name.Length > 100)
		{
			errors.Add("API token name must be 100 characters or fewer.");
		}

		if (errors.Count > 0)
		{
			throw new ValidationException(errors);
		}

		return name;
	}

	private static string CreateRawApiToken()
	{
		var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(ApiTokenBytes)).ToLowerInvariant();
		return ApiTokenPrefix + secret;
	}

	private static string HashApiToken(string token)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}
}
