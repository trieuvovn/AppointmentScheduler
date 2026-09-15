using AppointmentScheduler.Domain.Resources;
using AppointmentScheduler.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class UnitOfWorkTests
{
    private readonly DatabaseFixture _fixture;

    public UnitOfWorkTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ExecuteInTransactionAsync_CalledWhileAlreadyInATransaction_DoesNotThrow()
    {
        await using var db = _fixture.CreateContext();
        var uow = new UnitOfWork(db);
        var ct = TestContext.Current.CancellationToken;

        var act = async () => await uow.ExecuteInTransactionAsync(async outerCt =>
        {
            return await uow.ExecuteInTransactionAsync(
                innerCt => Task.FromResult(1), outerCt);
        }, ct);

        await act.Should().NotThrowAsync(
            "a nested call must join the ambient transaction instead of calling BeginTransactionAsync again");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_NestedCall_CommitsOnlyOnceWhenTheOuterScopeCompletes()
    {
        await using var db = _fixture.CreateContext();
        var uow = new UnitOfWork(db);
        var ct = TestContext.Current.CancellationToken;
        var dealershipId = Guid.NewGuid();

        await uow.ExecuteInTransactionAsync(async outerCt =>
        {
            db.Dealerships.Add(Dealership.Create(dealershipId, "Outer Motors", "Europe/London"));
            await db.SaveChangesAsync(outerCt);

            // Nested call: adds a second row inside what should be the same transaction.
            return await uow.ExecuteInTransactionAsync<object?>(async innerCt =>
            {
                var bay = ServiceBay.Create(Guid.NewGuid(), dealershipId, "BAY-1");
                db.ServiceBays.Add(bay);
                await db.SaveChangesAsync(innerCt);
                return null;
            }, outerCt);
        }, ct);

        await using var reading = _fixture.CreateContext();

        var dealership = await reading.Dealerships.SingleOrDefaultAsync(d => d.Id == dealershipId, ct);
        var bay = await reading.ServiceBays.SingleOrDefaultAsync(b => b.DealershipId == dealershipId, ct);

        dealership.Should().NotBeNull("the outer scope's write must be committed");
        bay.Should().NotBeNull("the inner scope's write must be committed alongside the outer one");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_InnerCallThrows_RollsBackTheOuterScopeToo()
    {
        await using var db = _fixture.CreateContext();
        var uow = new UnitOfWork(db);
        var ct = TestContext.Current.CancellationToken;
        var dealershipId = Guid.NewGuid();

        var act = async () => await uow.ExecuteInTransactionAsync<object?>(async outerCt =>
        {
            db.Dealerships.Add(Dealership.Create(dealershipId, "Rolled Back Motors", "Europe/London"));
            await db.SaveChangesAsync(outerCt);

            await uow.ExecuteInTransactionAsync<object?>(_ =>
                throw new InvalidOperationException("simulated failure inside the nested scope"),
                outerCt);

            return null;
        }, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();

        await using var reading = _fixture.CreateContext();

        var dealership = await reading.Dealerships.SingleOrDefaultAsync(d => d.Id == dealershipId, ct);

        dealership.Should().BeNull(
            "an exception from the inner scope must roll back the whole outer transaction, " +
            "including work the outer scope already staged before the nested call");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_NotAlreadyInATransaction_StillOpensAndCommitsOne()
    {
        await using var db = _fixture.CreateContext();
        var uow = new UnitOfWork(db);
        var ct = TestContext.Current.CancellationToken;
        var dealershipId = Guid.NewGuid();

        await uow.ExecuteInTransactionAsync<object?>(async innerCt =>
        {
            db.Dealerships.Add(Dealership.Create(dealershipId, "Standalone Motors", "Europe/London"));
            await db.SaveChangesAsync(innerCt);
            return null;
        }, ct);

        await using var reading = _fixture.CreateContext();

        var dealership = await reading.Dealerships.SingleOrDefaultAsync(d => d.Id == dealershipId, ct);

        dealership.Should().NotBeNull("the top-level (non-nested) case must be unaffected by the change");
    }
}
