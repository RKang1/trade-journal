namespace TradeJournal.Services.Trades;

public interface ITradeService
{
	Task<IReadOnlyList<TradeResult>> ListAsync(Guid userId, CancellationToken cancellationToken);
	Task<TradeResult> CreateAsync(Guid userId, CreateTradeCommand command, CancellationToken cancellationToken);
	Task<TradeResult> UpdateAsync(Guid userId, Guid tradeId, UpdateTradeCommand command, CancellationToken cancellationToken);
	Task<TradeResult> CloseAsync(Guid userId, Guid tradeId, CloseTradeCommand command, CancellationToken cancellationToken);
}
