using TradeJournal.Services.Trades;

namespace TradeJournal.Api.Contracts;

public static class TradeMapping
{
	public static TradeDto ToDto(this TradeResult result) => new(
		result.Id, result.Symbol, result.Side, result.Status,
		result.EntryAt, result.EntryPrice, result.Quantity,
		result.ExitAt, result.ExitPrice, result.Fees,
		result.Setup, result.Notes, result.RealizedPnl,
		result.CreatedAt, result.UpdatedAt);

	public static CreateTradeCommand ToCommand(this CreateTradeRequest request) => new(
		request.Symbol, request.Side, request.EntryAt, request.EntryPrice,
		request.Quantity, request.Fees, request.Setup, request.Notes);

	public static UpdateTradeCommand ToCommand(this UpdateTradeRequest request) => new(
		request.Symbol, request.Side, request.EntryAt, request.EntryPrice,
		request.Quantity, request.Fees, request.Setup, request.Notes);

	public static CloseTradeCommand ToCommand(this CloseTradeRequest request) => new(
		request.ExitAt, request.ExitPrice, request.Fees);
}
