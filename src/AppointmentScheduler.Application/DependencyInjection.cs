using AppointmentScheduler.Application.Features.Availability;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentScheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<GetAvailabilityHandler>();

        return services;
    }
}
