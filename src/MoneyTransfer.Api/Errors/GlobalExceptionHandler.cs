using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MoneyTransfer.Api.Errors;

public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            BadHttpRequestException badRequest => (badRequest.StatusCode, "Invalid request", "The request could not be read. Check that the body is valid JSON with the expected fields and types."),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred. Please try again later.")
        
        };

        if(status >= 500)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Rejected malformed request: {Message}", exception.Message);
        }

        httpContext.Response.StatusCode = status;

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });

        return true;
    }
}
