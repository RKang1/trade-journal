using Microsoft.EntityFrameworkCore;
using TradeJournal.Data;
using TradeJournal.Data.Entities;
using TradeJournal.Services.Common;

namespace TradeJournal.Services.Trades;

public class TradeService : ITradeService
{
	private readonly TradeJournalDbContext _db;
	private readonly TimeProvider _clock;

	public TradeService(TradeJournalDbContext db, TimeProvider clock)
	{
		_db = db;
		_clock = clock;
	}

	public async Task<IReadOnlyList<TradeResult>> ListAsync(Guid userId, CancellationToken cancellationToken)
	{
		var trades = await _db.Trades
			.AsNoTracking()
			.Where(t => t.UserId == userId)
			.OrderByDescending(t => t.EntryAt)
			.ToListAsync(cancellationToken);

		return trades.Select(ToResult).ToList();
	}

	public async Task<TradeResult> CreateAsync(Guid userId, CreateTradeCommand command, CancellationToken cancellationToken)
	{
		var symbol = ValidateAndNormalizeSymbol(command.Symbol);
		var errors = new List<string>();
		ValidateSide(command.Side, errors);
		ValidatePositive("Entry price", command.EntryPrice, errors);
		ValidatePositive("Quantity", command.Quantity, errors);
		ValidateNonNegativeNullable("Fees", command.Fees, errors);
		ValidateSetupAndNotes(command.Setup, command.Notes, errors);
		ThrowIfErrors(errors);

		var now = _clock.GetUtcNow();
		var trade = new Trade
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Symbol = symbol,
			Side = command.Side,
			Status = TradeStatus.Open,
			EntryAt = command.EntryAt,
			EntryPrice = command.EntryPrice,
			Quantity = command.Quantity,
			Fees = command.Fees,
			Setup = NullIfBlank(command.Setup),
			Notes = NullIfBlank(command.Notes),
			CreatedAt = now,
			UpdatedAt = now,
		};

		_db.Trades.Add(trade);
		await _db.SaveChangesAsync(cancellationToken);

		return ToResult(trade);
	}

	public async Task<TradeResult> UpdateAsync(Guid userId, Guid tradeId, UpdateTradeCommand command, CancellationToken cancellationToken)
	{
		var trade = await GetOwnedTradeAsync(userId, tradeId, cancellationToken);

		var symbol = ValidateAndNormalizeSymbol(command.Symbol);
		var errors = new List<string>();
		ValidateSide(command.Side, errors);
		ValidatePositive("Entry price", command.EntryPrice, errors);
		ValidatePositive("Quantity", command.Quantity, errors);
		ValidateNonNegativeNullable("Fees", command.Fees, errors);
		ValidateSetupAndNotes(command.Setup, command.Notes, errors);
		ThrowIfErrors(errors);

		trade.Symbol = symbol;
		trade.Side = command.Side;
		trade.EntryAt = command.EntryAt;
		trade.EntryPrice = command.EntryPrice;
		trade.Quantity = command.Quantity;
		trade.Fees = command.Fees;
		trade.Setup = NullIfBlank(command.Setup);
		trade.Notes = NullIfBlank(command.Notes);
		trade.UpdatedAt = _clock.GetUtcNow();

		if (trade.Status == TradeStatus.Closed)
		{
			trade.RealizedPnl = ComputeRealizedPnl(trade);
		}

		await _db.SaveChangesAsync(cancellationToken);
		return ToResult(trade);
	}

	public async Task<TradeResult> CloseAsync(Guid userId, Guid tradeId, CloseTradeCommand command, CancellationToken cancellationToken)
	{
		var trade = await GetOwnedTradeAsync(userId, tradeId, cancellationToken);

		if (trade.Status != TradeStatus.Open)
		{
			throw new ValidationException("Trade is already closed.");
		}

		var errors = new List<string>();
		ValidatePositive("Exit price", command.ExitPrice, errors);
		ValidateNonNegativeNullable("Fees", command.Fees, errors);
		if (command.ExitAt < trade.EntryAt)
		{
			errors.Add("Exit time must be on or after entry time.");
		}
		ThrowIfErrors(errors);

		trade.Status = TradeStatus.Closed;
		trade.ExitAt = command.ExitAt;
		trade.ExitPrice = command.ExitPrice;
		if (command.Fees is not null)
		{
			trade.Fees = command.Fees;
		}
		trade.RealizedPnl = ComputeRealizedPnl(trade);
		trade.UpdatedAt = _clock.GetUtcNow();

		await _db.SaveChangesAsync(cancellationToken);
		return ToResult(trade);
	}

	public async Task DeleteAsync(Guid userId, Guid tradeId, CancellationToken cancellationToken)
	{
		var trade = await GetOwnedTradeAsync(userId, tradeId, cancellationToken);
		_db.Trades.Remove(trade);
		await _db.SaveChangesAsync(cancellationToken);
	}

	private async Task<Trade> GetOwnedTradeAsync(Guid userId, Guid tradeId, CancellationToken cancellationToken)
	{
		var trade = await _db.Trades.FirstOrDefaultAsync(t => t.Id == tradeId, cancellationToken)
			?? throw new NotFoundException("Trade not found.");

		if (trade.UserId != userId)
		{
			throw new NotFoundException("Trade not found.");
		}

		return trade;
	}

	private static decimal ComputeRealizedPnl(Trade trade)
	{
		if (trade.ExitPrice is null)
		{
			return 0m;
		}

		var fees = trade.Fees ?? 0m;
		var gross = trade.Side switch
		{
			TradeSide.Long => (trade.ExitPrice.Value - trade.EntryPrice) * trade.Quantity,
			TradeSide.Short => (trade.EntryPrice - trade.ExitPrice.Value) * trade.Quantity,
			_ => 0m,
		};
		return decimal.Round(gross - fees, 2, MidpointRounding.AwayFromZero);
	}

	private static string ValidateAndNormalizeSymbol(string? symbol)
	{
		if (string.IsNullOrWhiteSpace(symbol))
		{
			throw new ValidationException("Symbol is required.");
		}

		var trimmed = symbol.Trim().ToUpperInvariant();
		if (trimmed.Length > 16)
		{
			throw new ValidationException("Symbol must be 16 characters or fewer.");
		}

		foreach (var ch in trimmed)
		{
			if (!(char.IsLetterOrDigit(ch) || ch is '.' or '-' or '/'))
			{
				throw new ValidationException("Symbol contains invalid characters.");
			}
		}

		return trimmed;
	}

	private static void ValidateSide(TradeSide side, List<string> errors)
	{
		if (side != TradeSide.Long && side != TradeSide.Short)
		{
			errors.Add("Side must be Long or Short.");
		}
	}

	private static void ValidatePositive(string field, decimal value, List<string> errors)
	{
		if (value <= 0m)
		{
			errors.Add($"{field} must be greater than zero.");
		}
	}

	private static void ValidateNonNegativeNullable(string field, decimal? value, List<string> errors)
	{
		if (value is < 0m)
		{
			errors.Add($"{field} must be zero or greater.");
		}
	}

	private static void ValidateSetupAndNotes(string? setup, string? notes, List<string> errors)
	{
		if (setup is { Length: > 128 })
		{
			errors.Add("Setup must be 128 characters or fewer.");
		}
		if (notes is { Length: > 4000 })
		{
			errors.Add("Notes must be 4000 characters or fewer.");
		}
	}

	private static void ThrowIfErrors(List<string> errors)
	{
		if (errors.Count > 0)
		{
			throw new ValidationException(errors);
		}
	}

	private static string? NullIfBlank(string? value)
		=> string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static TradeResult ToResult(Trade t) => new(
		t.Id, t.Symbol, t.Side, t.Status,
		t.EntryAt, t.EntryPrice, t.Quantity,
		t.ExitAt, t.ExitPrice, t.Fees,
		t.Setup, t.Notes, t.RealizedPnl,
		t.CreatedAt, t.UpdatedAt);
}
