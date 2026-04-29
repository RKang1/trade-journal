using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
}
