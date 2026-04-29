using Microsoft.EntityFrameworkCore;
using TradeJournal.Data.Entities;

namespace TradeJournal.Data;

public class TradeJournalDbContext : DbContext
{
	public TradeJournalDbContext(DbContextOptions<TradeJournalDbContext> options)
		: base(options)
	{
	}

	public DbSet<User> Users => Set<User>();
	public DbSet<Trade> Trades => Set<Trade>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeJournalDbContext).Assembly);
	}
}
