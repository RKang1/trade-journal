using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeJournal.Api.Contracts;
using TradeJournal.Services.Trades;

namespace TradeJournal.Api.Controllers;

[ApiController]
[Route("api/trades")]
[Authorize]
public class TradesController : ControllerBase
{
	private readonly ITradeService _trades;

	public TradesController(ITradeService trades)
	{
		_trades = trades;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<TradeDto>>> List(CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		var trades = await _trades.ListAsync(userId, cancellationToken);
		return Ok(trades.Select(t => t.ToDto()).ToList());
	}

	[HttpPost]
	public async Task<ActionResult<TradeDto>> Create(
		[FromBody] CreateTradeRequest request,
		CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		var result = await _trades.CreateAsync(userId, request.ToCommand(), cancellationToken);
		return CreatedAtAction(nameof(List), new { id = result.Id }, result.ToDto());
	}

	[HttpPut("{id:guid}")]
	public async Task<ActionResult<TradeDto>> Update(
		Guid id,
		[FromBody] UpdateTradeRequest request,
		CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		var result = await _trades.UpdateAsync(userId, id, request.ToCommand(), cancellationToken);
		return Ok(result.ToDto());
	}

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		await _trades.DeleteAsync(userId, id, cancellationToken);
		return NoContent();
	}

	[HttpPost("{id:guid}/close")]
	public async Task<ActionResult<TradeDto>> Close(
		Guid id,
		[FromBody] CloseTradeRequest request,
		CancellationToken cancellationToken)
	{
		var userId = User.GetUserId();
		var result = await _trades.CloseAsync(userId, id, request.ToCommand(), cancellationToken);
		return Ok(result.ToDto());
	}
}
