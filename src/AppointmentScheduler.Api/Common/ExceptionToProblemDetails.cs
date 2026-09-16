using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AppointmentScheduler.Api.Common;

internal sealed class ExceptionToProblemDetails : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, title) = exception switch
        {
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "The request conflicts with the current schedule"),
            InvalidStatusTransitionException => (StatusCodes.Status422UnprocessableEntity, "The request could not be processed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var value)
            ? value as string
            : null;

        httpContext.Response.StatusCode = statusCode;

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = title,
                Status = statusCode,
                Extensions =
                {
                    ["correlationId"] = correlationId,
                },
            },
        });
    }
}
