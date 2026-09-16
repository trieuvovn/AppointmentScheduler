using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Application.Features.Booking;
using AppointmentScheduler.Infrastructure.Persistence;
using AppointmentScheduler.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppointmentScheduler.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppointmentDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.CommandTimeout(30);
            }));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
