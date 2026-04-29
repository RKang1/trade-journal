using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TradeJournal.Data;
using TradeJournal.Data.Entities;
using TradeJournal.Services.Common;
using TradeJournal.Services.Trades;

namespace TradeJournal.Services.Tests;

public class TradeServiceTests
{
	private static TradeJournalDbContext NewDb()
	{
		var options = new DbContextOptionsBuilder<TradeJournalDbContext>()
			.UseInMemoryDatabase($"trades-{Guid.NewGuid()}")
			.Options;
		return new TradeJournalDbContext(options);
	}

	private static TradeService NewService(TradeJournalDbContext db)
		=> new(db, TimeProvider.System);

	private static CreateTradeCommand ValidCreate(string symbol = "AAPL") => new(
		Symbol: symbol,
		Side: TradeSide.Long,
		EntryAt: DateTimeOffset.UtcNow.AddMinutes(-30),
		EntryPrice: 100m,
		Quantity: 10m,
		Fees: 1m,
		Setup: "breakout",
		Notes: "test");

	[Fact]
	public async Task Create_PersistsOpenTradeForCaller()
	{
		await using var db = NewDb();
		var service = NewService(db);
		var userId = Guid.NewGuid();

		var result = await service.CreateAsync(userId, ValidCreate(), CancellationToken.None);

		result.Status.Should().Be(TradeStatus.Open);
		result.Symbol.Should().Be("AAPL");
		result.RealizedPnl.Should().BeNull();

		var stored = await db.Trades.SingleAsync();
		stored.UserId.Should().Be(userId);
		stored.Symbol.Should().Be("AAPL");
	}

	[Theory]
	[InlineData("", 100, 10)]
	[InlineData("AAPL", 0, 10)]
	[InlineData("AAPL", -1, 10)]
	[InlineData("AAPL", 100, 0)]
	[InlineData("AAPL", 100, -5)]
	public async Task Create_RejectsInvalidPayloads(string symbol, decimal entryPrice, decimal quantity)
	{
		await using var db = NewDb();
		var service = NewService(db);

		var command = ValidCreate(symbol == "" ? "" : symbol) with
		{
			EntryPrice = entryPrice,
			Quantity = quantity,
		};

		await Assert.ThrowsAsync<ValidationException>(() =>
			service.CreateAsync(Guid.NewGuid(), command, CancellationToken.None));

		(await db.Trades.CountAsync()).Should().Be(0);
	}

	[Fact]
	public async Task Close_LongTrade_ComputesPnlCorrectly()
	{
		await using var db = NewDb();
		var service = NewService(db);
		var userId = Guid.NewGuid();

		var trade = await service.CreateAsync(userId, ValidCreate() with
		{
			Side = TradeSide.Long,
			EntryPrice = 100m,
			Quantity = 10m,
			Fees = 1m,
		}, CancellationToken.None);

		var closed = await service.CloseAsync(userId, trade.Id, new CloseTradeCommand(
			ExitAt: DateTimeOffset.UtcNow,
			ExitPrice: 110m,
			Fees: 2m), CancellationToken.None);

		closed.Status.Should().Be(TradeStatus.Closed);
		closed.ExitPrice.Should().Be(110m);
		// (110 - 100) * 10 - 2 = 98
		closed.RealizedPnl.Should().Be(98m);
	}

	[Fact]
	public async Task Close_ShortTrade_ComputesPnlCorrectly()
	{
		await using var db = NewDb();
		var service = NewService(db);
		var userId = Guid.NewGuid();

		var trade = await service.CreateAsync(userId, ValidCreate() with
		{
			Side = TradeSide.Short,
			EntryPrice = 50m,
			Quantity = 20m,
			Fees = 1m,
		}, CancellationToken.None);

		var closed = await service.CloseAsync(userId, trade.Id, new CloseTradeCommand(
			ExitAt: DateTimeOffset.UtcNow,
			ExitPrice: 45m,
			Fees: 1m), CancellationToken.None);

		// (50 - 45) * 20 - 1 = 99
		closed.RealizedPnl.Should().Be(99m);
	}

	[Fact]
	public async Task Close_RejectsMissingExitPrice()
	{
		await using var db = NewDb();
		var service = NewService(db);
		var userId = Guid.NewGuid();
		var trade = await service.CreateAsync(userId, ValidCreate(), CancellationToken.None);

		var bad = new CloseTradeCommand(
			ExitAt: DateTimeOffset.UtcNow,
			ExitPrice: 0m,
			Fees: null);

		await Assert.ThrowsAsync<ValidationException>(() =>
			service.CloseAsync(userId, trade.Id, bad, CancellationToken.None));
	}

	[Fact]
	public async Task Close_RejectsExitBeforeEntry()
	{
		await using var db = NewDb();
		var service = NewService(db);
		var userId = Guid.NewGuid();
		var trade = await service.CreateAsync(userId, ValidCreate() with
		{
			EntryAt = DateTimeOffset.UtcNow,
		}, CancellationToken.None);

		var bad = new CloseTradeCommand(
			ExitAt: DateTimeOffset.UtcNow.AddDays(-1),
			ExitPrice: 50m,
			Fees: null);

		await Assert.ThrowsAsync<ValidationException>(() =>
			service.CloseAsync(userId, trade.Id, bad, CancellationToken.None));
	}

	[Fact]
	public async Task Close_AlreadyClosed_ThrowsValidation()
	{
		await using var db = NewDb();
		var service = NewService(db);
		var userId = Guid.NewGuid();
		var trade = await service.CreateAsync(userId, ValidCreate(), CancellationToken.None);

		await service.CloseAsync(userId, trade.Id, new CloseTradeCommand(
			ExitAt: DateTimeOffset.UtcNow,
			ExitPrice: 110m,
			Fees: null), CancellationToken.None);

		await Assert.ThrowsAsync<ValidationException>(() =>
			service.CloseAsync(userId, trade.Id, new CloseTradeCommand(
				ExitAt: DateTimeOffset.UtcNow,
				ExitPrice: 120m,
				Fees: null), CancellationToken.None));
	}

	[Fact]
	public async Task CrossUserAccess_ReturnsNotFound()
	{
		await using var db = NewDb();
		var service = NewService(db);

		var owner = Guid.NewGuid();
		var stranger = Guid.NewGuid();

		var trade = await service.CreateAsync(owner, ValidCreate(), CancellationToken.None);

		await Assert.ThrowsAsync<NotFoundException>(() =>
			service.UpdateAsync(stranger, trade.Id, new UpdateTradeCommand(
				Symbol: "AAPL",
				Side: TradeSide.Long,
				EntryAt: trade.EntryAt,
				EntryPrice: 100m,
				Quantity: 10m,
				Fees: null,
				Setup: null,
				Notes: null), CancellationToken.None));

		await Assert.ThrowsAsync<NotFoundException>(() =>
			service.CloseAsync(stranger, trade.Id, new CloseTradeCommand(
				ExitAt: DateTimeOffset.UtcNow,
				ExitPrice: 110m,
				Fees: null), CancellationToken.None));
	}

	[Fact]
	public async Task List_ReturnsOnlyCallersTrades()
	{
		await using var db = NewDb();
		var service = NewService(db);
		var alice = Guid.NewGuid();
		var bob = Guid.NewGuid();

		await service.CreateAsync(alice, ValidCreate("AAA"), CancellationToken.None);
		await service.CreateAsync(alice, ValidCreate("BBB"), CancellationToken.None);
		await service.CreateAsync(bob, ValidCreate("CCC"), CancellationToken.None);

		var aliceTrades = await service.ListAsync(alice, CancellationToken.None);
		var bobTrades = await service.ListAsync(bob, CancellationToken.None);

		aliceTrades.Should().HaveCount(2);
		aliceTrades.Select(t => t.Symbol).Should().BeEquivalentTo(new[] { "AAA", "BBB" });

		bobTrades.Should().HaveCount(1);
		bobTrades[0].Symbol.Should().Be("CCC");
	}
}
