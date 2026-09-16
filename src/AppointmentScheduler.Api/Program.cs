using AppointmentScheduler.Api.Features.Availability;
using AppointmentScheduler.Api.Features.Booking;
using AppointmentScheduler.Application;
using AppointmentScheduler.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppointmentScheduler")
    ?? throw new InvalidOperationException(
        "Connection string 'AppointmentScheduler' is not configured.");

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddApplication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new HealthResponse("Healthy")))
   .WithName("Health")
   .WithSummary("Liveness probe.");

app.MapAvailabilityEndpoints();
app.MapAppointmentEndpoints();

app.Run();

internal sealed record HealthResponse(string Status);

public partial class Program;
