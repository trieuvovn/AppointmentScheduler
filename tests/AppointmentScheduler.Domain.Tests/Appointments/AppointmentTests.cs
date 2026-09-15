using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Appointments;

public class AppointmentTests
{
    private static readonly DateTimeOffset NineAm = new(2026, 3, 16, 9, 0, 0, TimeSpan.Zero);

    private static ServiceType OneHourService(params Skill[] requiredSkills) =>
        ServiceType.Create(Guid.NewGuid(), "MOT", "MOT test", 60, requiredSkills: requiredSkills);

    private static Appointment Book(ServiceType? serviceType = null) =>
        Appointment.Book(
            id: Guid.NewGuid(),
            dealershipId: Guid.NewGuid(),
            serviceBayId: Guid.NewGuid(),
            technicianId: Guid.NewGuid(),
            serviceType: serviceType ?? OneHourService(),
            vehicleId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            startsAtUtc: NineAm,
            createdAtUtc: NineAm.AddDays(-1));

    private static Appointment InStatus(AppointmentStatus status)
    {
        var appointment = Book();

        switch (status)
        {
            case AppointmentStatus.Confirmed:
                break;
            case AppointmentStatus.InProgress:
                appointment.Start();
                break;
            case AppointmentStatus.Completed:
                appointment.Start();
                appointment.Complete();
                break;
            case AppointmentStatus.Cancelled:
                appointment.Cancel();
                break;
            case AppointmentStatus.NoShow:
                appointment.MarkNoShow();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return appointment;
    }

    [Fact]
    public void Book_ServiceTypeWithADuration_DerivesTheEndTimeFromIt()
    {
        // Requirement 2.1: the caller supplies a start; the catalogue supplies the length.
        var appointment = Book(ServiceType.Create(Guid.NewGuid(), "SVC", "Service", 90));

        appointment.StartsAtUtc.Should().Be(NineAm);
        appointment.EndsAtUtc.Should().Be(NineAm.AddMinutes(90));
        appointment.Slot.Duration.Should().Be(TimeSpan.FromMinutes(90));
    }

    [Fact]
    public void Book_ValidRequest_StartsOutConfirmedAndOccupyingItsResources()
    {
        var appointment = Book();

        appointment.Status.Should().Be(AppointmentStatus.Confirmed);
        appointment.Occupies.Should().BeTrue();
        appointment.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void Book_GivenServiceType_RecordsItsIdOnTheAppointment()
    {
        var serviceType = OneHourService();

        Book(serviceType).ServiceTypeId.Should().Be(serviceType.Id);
    }

    [Fact]
    public void Book_MissingServiceType_ThrowsArgumentNullException()
    {
        var act = () => Appointment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            serviceType: null!,
            Guid.NewGuid(), Guid.NewGuid(), NineAm, NineAm);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.InProgress, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.InProgress, AppointmentStatus.Cancelled)]
    public void CanTransitionTo_LegalMoveForTheCurrentStatus_ReturnsTrue(AppointmentStatus from, AppointmentStatus to)
    {
        InStatus(from).CanTransitionTo(to).Should().BeTrue();
    }

    [Theory]
    // Nothing may leave a terminal status.
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.InProgress)]
    // Work that has started cannot be un-started, and no-show contradicts it.
    [InlineData(AppointmentStatus.InProgress, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.InProgress, AppointmentStatus.NoShow)]
    // A status cannot be re-entered.
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.InProgress, AppointmentStatus.InProgress)]
    public void CanTransitionTo_IllegalMoveForTheCurrentStatus_ReturnsFalse(AppointmentStatus from, AppointmentStatus to)
    {
        InStatus(from).CanTransitionTo(to).Should().BeFalse();
    }

    [Fact]
    public void Start_ConfirmedAppointment_MovesToInProgress()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.Start();

        appointment.Status.Should().Be(AppointmentStatus.InProgress);
    }

    [Fact]
    public void Complete_AppointmentInProgress_MovesToCompleted()
    {
        var appointment = InStatus(AppointmentStatus.InProgress);

        appointment.Complete();

        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Start_AppointmentAlreadyInATerminalStatus_ThrowsInvalidStatusTransitionException(AppointmentStatus status)
    {
        var appointment = InStatus(status);

        var act = appointment.Start;

        act.Should().Throw<InvalidStatusTransitionException>()
           .Which.From.Should().Be(status);
    }

    [Fact]
    public void Cancel_AppointmentAlreadyCancelled_ThrowsInvalidStatusTransitionException()
    {
        var appointment = InStatus(AppointmentStatus.Cancelled);

        var act = appointment.Cancel;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Cancel_CompletedAppointment_ThrowsExceptionNamingBothEndsOfTheRefusedMove()
    {
        var appointment = InStatus(AppointmentStatus.Completed);

        var act = appointment.Cancel;

        var exception = act.Should().Throw<InvalidStatusTransitionException>().Which;
        exception.From.Should().Be(AppointmentStatus.Completed);
        exception.To.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_CompletedAppointment_LeavesTheStatusUntouched()
    {
        var appointment = InStatus(AppointmentStatus.Completed);

        var act = appointment.Cancel;

        act.Should().Throw<InvalidStatusTransitionException>();
        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed, true)]
    [InlineData(AppointmentStatus.InProgress, true)]
    [InlineData(AppointmentStatus.Completed, false)]
    [InlineData(AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.NoShow, false)]
    public void Occupies_GivenStatus_ReturnsTrueOnlyForConfirmedAndInProgress(AppointmentStatus status, bool occupies)
    {
        InStatus(status).Occupies.Should().Be(occupies);
    }

    [Fact]
    public void Cancel_ConfirmedAppointment_FreesTheResourcesImmediately()
    {
        // Requirement 4.1, and the reason the Stage 5 cancel slice needs no extra work.
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.Cancel();

        appointment.Occupies.Should().BeFalse();
    }

    [Fact]
    public void OccupyingStatuses_StaticSet_MatchesTheFilteredIndexPredicate()
    {
        // The Stage 2 indexes carry WHERE Status IN ('Confirmed', 'InProgress'). If this set
        // and that predicate diverge, availability and the index disagree (data model 3.3).
        Appointment.OccupyingStatuses.Should().BeEquivalentTo(
            [AppointmentStatus.Confirmed, AppointmentStatus.InProgress]);
    }
}
