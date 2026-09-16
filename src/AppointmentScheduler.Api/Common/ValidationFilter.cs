using FluentValidation;

namespace AppointmentScheduler.Api.Common;

internal sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null) return await next(context);

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        return result.IsValid ? await next(context) : Results.ValidationProblem(result.ToDictionary());
    }
}

internal static class ValidationFilterExtensions
{
    internal static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class => builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
