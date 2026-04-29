using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeJournal.Data.Entities;

namespace TradeJournal.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
	public void Configure(EntityTypeBuilder<User> builder)
	{
		builder.ToTable("users");

		builder.HasKey(u => u.Id);

		builder.Property(u => u.GoogleSubject)
			.HasMaxLength(255)
			.IsRequired();

		builder.HasIndex(u => u.GoogleSubject).IsUnique();

		builder.Property(u => u.Email)
			.HasMaxLength(320)
			.IsRequired();

		builder.Property(u => u.DisplayName)
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(u => u.CreatedAt).IsRequired();
		builder.Property(u => u.LastLoginAt).IsRequired();
	}
}
