namespace TradeJournal.Data.Entities;

public class User
{
	public Guid Id { get; set; }
	public string GoogleSubject { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset LastLoginAt { get; set; }

	public ICollection<Trade> Trades { get; set; } = new List<Trade>();
}
