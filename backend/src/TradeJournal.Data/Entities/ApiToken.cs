namespace TradeJournal.Data.Entities;

public class ApiToken
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public User User { get; set; } = null!;
	public string Name { get; set; } = string.Empty;
	public string TokenHash { get; set; } = string.Empty;
	public string Prefix { get; set; } = string.Empty;
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset? LastUsedAt { get; set; }
	public DateTimeOffset? RevokedAt { get; set; }
}
