using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeJournal.Api.Contracts;
using TradeJournal.Services.Auth;

namespace TradeJournal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
	private readonly IAuthService _auth;

	public AuthController(IAuthService auth)
	{
		_auth = auth;
	}

	[HttpPost("google")]
	[AllowAnonymous]
	public async Task<ActionResult<AuthResponse>> Google(
		[FromBody] GoogleSignInRequest request,
		CancellationToken cancellationToken)
	{
		var result = await _auth.SignInWithGoogleAsync(new GoogleSignInCommand(request.IdToken), cancellationToken);
		var user = new UserProfileDto(result.User.Id, result.User.Email, result.User.DisplayName);
		return Ok(new AuthResponse(result.Token, result.ExpiresAt, user));
	}

	[HttpGet("me")]
	[Authorize]
	public async Task<ActionResult<UserProfileDto>> Me(CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		var profile = await _auth.GetProfileAsync(userId, cancellationToken);
		return Ok(new UserProfileDto(profile.Id, profile.Email, profile.DisplayName));
	}

	[HttpGet("tokens")]
	[Authorize(Policy = TradeJournalAuthentication.InteractiveUserPolicy)]
	public async Task<ActionResult<IReadOnlyList<ApiTokenDto>>> ListTokens(CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		var tokens = await _auth.ListApiTokensAsync(userId, cancellationToken);
		return Ok(tokens.Select(token => token.ToDto()).ToList());
	}

	[HttpPost("tokens")]
	[Authorize(Policy = TradeJournalAuthentication.InteractiveUserPolicy)]
	public async Task<ActionResult<CreateApiTokenResponse>> CreateToken(
		[FromBody] CreateApiTokenRequest request,
		CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		var created = await _auth.CreateApiTokenAsync(userId, new CreateApiTokenCommand(request.Name), cancellationToken);
		return Ok(new CreateApiTokenResponse(created.Token, created.Details.ToDto()));
	}

	[HttpDelete("tokens/{id:guid}")]
	[Authorize(Policy = TradeJournalAuthentication.InteractiveUserPolicy)]
	public async Task<IActionResult> RevokeToken(Guid id, CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		await _auth.RevokeApiTokenAsync(userId, id, cancellationToken);
		return NoContent();
	}
}
