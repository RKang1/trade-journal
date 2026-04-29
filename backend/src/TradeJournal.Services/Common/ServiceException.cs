namespace TradeJournal.Services.Common;

public abstract class ServiceException : Exception
{
	protected ServiceException(string message) : base(message)
	{
	}
}

public class NotFoundException : ServiceException
{
	public NotFoundException(string message) : base(message)
	{
	}
}

public class ValidationException : ServiceException
{
	public IReadOnlyList<string> Errors { get; }

	public ValidationException(string error) : base(error)
	{
		Errors = new[] { error };
	}

	public ValidationException(IReadOnlyList<string> errors)
		: base(errors.Count == 0 ? "Validation failed." : errors[0])
	{
		Errors = errors;
	}
}
