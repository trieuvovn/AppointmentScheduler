using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Application.Features.Booking;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentScheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<GetAvailabilityHandler>();
        services.AddScoped<BookAppointmentHandler>();

        services.AddValidatorsFromAssemblyContaining<BookAppointmentValidator>();

        services.AddOptions<BookingHorizon>()
            .Bind(configuration.GetSection(BookingHorizon.SectionName));

        return services;
    }
}
