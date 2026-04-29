using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeJournal.Data.Entities;

namespace TradeJournal.Data.Configurations;

public class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
	public void Configure(EntityTypeBuilder<Trade> builder)
	{
		builder.ToTable("trades");

		builder.HasKey(t => t.Id);

		builder.Property(t => t.Symbol)
			.HasMaxLength(16)
			.IsRequired();

		builder.Property(t => t.Side)
			.HasConversion<string>()
			.HasMaxLength(16)
			.IsRequired();

		builder.Property(t => t.Status)
			.HasConversion<string>()
			.HasMaxLength(16)
			.IsRequired();

		builder.Property(t => t.EntryAt).IsRequired();
		builder.Property(t => t.EntryPrice).HasColumnType("numeric(18,4)").IsRequired();
		builder.Property(t => t.Quantity).HasColumnType("numeric(18,4)").IsRequired();

		builder.Property(t => t.ExitAt);
		builder.Property(t => t.ExitPrice).HasColumnType("numeric(18,4)");
		builder.Property(t => t.Fees).HasColumnType("numeric(18,2)");
		builder.Property(t => t.RealizedPnl).HasColumnType("numeric(18,2)");

		builder.Property(t => t.Setup).HasMaxLength(128);
		builder.Property(t => t.Notes).HasMaxLength(4000);

		builder.Property(t => t.CreatedAt).IsRequired();
		builder.Property(t => t.UpdatedAt).IsRequired();

		builder.HasOne(t => t.User)
			.WithMany(u => u.Trades)
			.HasForeignKey(t => t.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(t => new { t.UserId, t.EntryAt });
	}
}
