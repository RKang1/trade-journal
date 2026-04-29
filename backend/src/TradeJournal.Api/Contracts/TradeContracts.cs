using TradeJournal.Data.Entities;

namespace TradeJournal.Api.Contracts;

public record TradeDto(
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

public record CreateTradeRequest(
	string Symbol,
	TradeSide Side,
	DateTimeOffset EntryAt,
	decimal EntryPrice,
	decimal Quantity,
	decimal? Fees,
	string? Setup,
	string? Notes);

public record UpdateTradeRequest(
	string Symbol,
	TradeSide Side,
	DateTimeOffset EntryAt,
	decimal EntryPrice,
	decimal Quantity,
	decimal? Fees,
	string? Setup,
	string? Notes);

public record CloseTradeRequest(
	DateTimeOffset ExitAt,
	decimal ExitPrice,
	decimal? Fees);
