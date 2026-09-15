using AppointmentScheduler.DatabaseMigration;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests;
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "APPOINTMENTSCHEDULER_TEST_SQL";

    private readonly MsSqlContainer? _container =
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 }
            ? null
            : new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        if (_container is null)
        {
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable)!;
        }
        else
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        var result = DatabaseMigrator.Run(ConnectionString);

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                "The schema migration failed, so no integration test can be trusted.", result.Error);
        }
    }

    public ValueTask DisposeAsync() =>
        _container?.DisposeAsync() ?? ValueTask.CompletedTask;
    public AppointmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppointmentDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppointmentDbContext(options);
    }

    public void SeedDemoData()
    {
        var result = DatabaseMigrator.RunDemoData(ConnectionString);

        if (!result.Successful)
        {
            throw new InvalidOperationException("The demo data scripts failed.", result.Error);
        }
    }
}

[CollectionDefinition(Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xUnit names collection definitions this way; the suffix is the convention.")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
