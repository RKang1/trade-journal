using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeJournal.Data.Entities;

namespace TradeJournal.Data.Configurations;

public class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
	public void Configure(EntityTypeBuilder<ApiToken> builder)
	{
		builder.ToTable("api_tokens");

		builder.HasKey(token => token.Id);

		builder.Property(token => token.Name)
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(token => token.TokenHash)
			.HasMaxLength(64)
			.IsRequired();

		builder.Property(token => token.Prefix)
			.HasMaxLength(32)
			.IsRequired();

		builder.Property(token => token.CreatedAt).IsRequired();

		builder.HasIndex(token => token.TokenHash).IsUnique();
		builder.HasIndex(token => new { token.UserId, token.RevokedAt, token.CreatedAt });

		builder.HasOne(token => token.User)
			.WithMany(user => user.ApiTokens)
			.HasForeignKey(token => token.UserId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
