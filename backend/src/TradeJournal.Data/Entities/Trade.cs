namespace TradeJournal.Data.Entities;

public class Trade
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }

	public string Symbol { get; set; } = string.Empty;
	public TradeSide Side { get; set; }
	public TradeStatus Status { get; set; }

	public DateTimeOffset EntryAt { get; set; }
	public decimal EntryPrice { get; set; }
	public decimal Quantity { get; set; }

	public DateTimeOffset? ExitAt { get; set; }
	public decimal? ExitPrice { get; set; }
	public decimal? Fees { get; set; }

	public string? Setup { get; set; }
	public string? Notes { get; set; }

	public decimal? RealizedPnl { get; set; }

	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }

	public User? User { get; set; }
}
