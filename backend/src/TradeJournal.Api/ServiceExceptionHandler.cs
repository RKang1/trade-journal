using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TradeJournal.Services.Common;

namespace TradeJournal.Api;

public class ServiceExceptionHandler : IExceptionHandler
{
	private readonly ILogger<ServiceExceptionHandler> _logger;

	public ServiceExceptionHandler(ILogger<ServiceExceptionHandler> logger)
	{
		_logger = logger;
	}

	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		ProblemDetails problem;
		switch (exception)
		{
			case ValidationException validation:
				problem = new ProblemDetails
				{
					Status = StatusCodes.Status400BadRequest,
					Title = "Validation failed",
					Detail = validation.Message,
				};
				problem.Extensions["errors"] = validation.Errors;
				break;
			case NotFoundException notFound:
				problem = new ProblemDetails
				{
					Status = StatusCodes.Status404NotFound,
					Title = "Not found",
					Detail = notFound.Message,
				};
				break;
			case UnauthorizedAccessException:
				problem = new ProblemDetails
				{
					Status = StatusCodes.Status401Unauthorized,
					Title = "Unauthorized",
					Detail = "Authentication is required.",
				};
				break;
			default:
				return false;
		}

		httpContext.Response.StatusCode = problem.Status!.Value;
		httpContext.Response.ContentType = "application/problem+json";
		await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

		if (problem.Status >= 500)
		{
			_logger.LogError(exception, "Unhandled service exception");
		}

		return true;
	}
}
