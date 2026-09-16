using AppointmentScheduler.Api.Common;
using AppointmentScheduler.Api.Features.Availability;
using AppointmentScheduler.Api.Features.Booking;
using AppointmentScheduler.Application;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

var connectionString = builder.Configuration.GetConnectionString("AppointmentScheduler")
    ?? throw new InvalidOperationException(
        "Connection string 'AppointmentScheduler' is not configured.");

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(connectionString, builder.Configuration);
builder.Services.AddApplication(builder.Configuration);

builder.Services.AddExceptionHandler<ExceptionToProblemDetails>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(Telemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(Telemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCorrelationId();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapAvailabilityEndpoints();
app.MapAppointmentEndpoints();

app.Run();

public partial class Program;
