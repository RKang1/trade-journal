using TradeJournal.Data.Entities;

namespace TradeJournal.Services.Trades;

public record CreateTradeCommand(
	string Symbol,
	TradeSide Side,
	DateTimeOffset EntryAt,
	decimal EntryPrice,
	decimal Quantity,
	decimal? Fees,
	string? Setup,
	string? Notes);

public record UpdateTradeCommand(
	string Symbol,
	TradeSide Side,
	DateTimeOffset EntryAt,
	decimal EntryPrice,
	decimal Quantity,
	decimal? Fees,
	string? Setup,
	string? Notes);

public record CloseTradeCommand(
	DateTimeOffset ExitAt,
	decimal ExitPrice,
	decimal? Fees);

public record TradeResult(
	Guid Id,
	string Symbol,
	TradeSide Side,
	TradeStatus Status,
	DateTimeOffset EntryAt,
	decimal EntryPrice,
	decimal Quantity,
	DateTimeOffset? ExitAt,
	decimal? ExitPrice,
	decimal? Fees,
	string? Setup,
	string? Notes,
	decimal? RealizedPnl,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt);
