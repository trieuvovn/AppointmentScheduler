using AppointmentScheduler.DatabaseMigration;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class MigratorTests
{
    private readonly DatabaseFixture _fixture;

    public MigratorTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void Run_AgainstAnAlreadyMigratedDatabase_SucceedsAndAppliesNothing()
    {
        var result = DatabaseMigrator.Run(_fixture.ConnectionString);

        result.Successful.Should().BeTrue();
        result.Scripts.Should().BeEmpty("the journal records every script the first run applied");
    }
}
