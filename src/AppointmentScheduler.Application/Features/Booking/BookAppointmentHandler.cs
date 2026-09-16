using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Resources;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Application.Features.Booking;

public sealed class BookAppointmentHandler
{
    private readonly IAppointmentRepository _appointments;
    private readonly IAvailabilityRepository _availability;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly BookingHorizon _horizon;

    public BookAppointmentHandler(
        IAppointmentRepository appointments,
        IAvailabilityRepository availability,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<BookingHorizon> horizon)
    {
        ArgumentNullException.ThrowIfNull(horizon);

        _appointments = appointments;
        _availability = availability;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _horizon = horizon.Value;
    }

    public async Task<Result<BookAppointmentResponse>> HandleAsync(
        BookAppointmentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dealership? dealership;
        ServiceType? serviceType;
        DateTimeOffset now;

        using (var validate = Telemetry.Source.StartActivity("booking.validate"))
        {
            validate?.SetTag("dealership.id", request.DealershipId);

            dealership = await _availability.FindDealershipAsync(request.DealershipId, ct);

            if (dealership is null)
            {
                Telemetry.BookingConflicts.Add(1, new KeyValuePair<string, object?>("reason", nameof(BookingError.DealershipNotFound)));
                return Result.Failure<BookAppointmentResponse>(BookingError.DealershipNotFound);
            }

            serviceType = await _availability.FindServiceTypeAsync(request.ServiceTypeId, ct);

            if (serviceType is null)
            {
                Telemetry.BookingConflicts.Add(1, new KeyValuePair<string, object?>("reason", nameof(BookingError.ServiceTypeNotFound)));
                return Result.Failure<BookAppointmentResponse>(BookingError.ServiceTypeNotFound);
            }

            validate?.SetTag("service.duration_minutes", serviceType.Duration.TotalMinutes);

            now = _timeProvider.GetUtcNow();

            var ruleError = BookingRules.CanBeBookedAt(
                request.StartsAtUtc, now, _horizon.MinimumLeadTime, _horizon.MaximumHorizon);

            if (ruleError is { } error)
            {
                Telemetry.BookingConflicts.Add(1, new KeyValuePair<string, object?>("reason", error.ToString()));
                return Result.Failure<BookAppointmentResponse>(error);
            }

            if (!await _appointments.VehicleBelongsToCustomerAsync(request.VehicleId, request.CustomerId, ct))
            {
                Telemetry.BookingConflicts.Add(1, new KeyValuePair<string, object?>("reason", nameof(BookingError.VehicleNotFound)));
                return Result.Failure<BookAppointmentResponse>(BookingError.VehicleNotFound);
            }
        }

        TimeSlot slot;
        SlotAssignment assignment;

        using (var search = Telemetry.Source.StartActivity("booking.search_resources"))
        {
            search?.SetTag("dealership.id", request.DealershipId);
            search?.SetTag("service.duration_minutes", serviceType.Duration.TotalMinutes);

            slot = TimeSlot.FromDuration(request.StartsAtUtc, serviceType.Duration);
            var timeZone = TimeZoneResolver.Resolve(dealership.TimeZoneId);

            var localStart = TimeZoneInfo.ConvertTime(request.StartsAtUtc, timeZone);
            var openingWindow = dealership.OpeningWindowOn(DateOnly.FromDateTime(localStart.DateTime), timeZone);

            var bays = await _availability.GetBayOccupancyAsync(request.DealershipId, slot, ct);
            var technicians = await _availability.GetQualifiedTechnicianOccupancyAsync(
                request.DealershipId, request.ServiceTypeId, slot, ct);

            search?.SetTag("candidates.bays", bays.Count);
            search?.SetTag("candidates.technicians", technicians.Count);

            var searchResult = AvailabilitySearch.Search(slot, openingWindow, bays, technicians);

            if (!searchResult.IsSuccess)
            {
                Telemetry.BookingConflicts.Add(1, new KeyValuePair<string, object?>("reason", searchResult.Error!.Value.ToString()));
                return Result.Failure<BookAppointmentResponse>(searchResult.Error!.Value);
            }

            assignment = searchResult.Assignment!;
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            using var commit = Telemetry.Source.StartActivity("booking.commit");
            commit?.SetTag("dealership.id", request.DealershipId);

            await _appointments.LockResourcesAsync(assignment.ServiceBayId, assignment.TechnicianId, innerCt);

            if (await _appointments.HasOverlappingAppointmentAsync(request.VehicleId, slot, innerCt))
            {
                Telemetry.BookingConflicts.Add(1, new KeyValuePair<string, object?>("reason", nameof(BookingError.VehicleAlreadyBooked)));
                return Result.Failure<BookAppointmentResponse>(BookingError.VehicleAlreadyBooked);
            }

            if (!await _availability.IsStillFreeAsync(
                assignment.ServiceBayId, assignment.TechnicianId, slot, innerCt))
            {
                Telemetry.BookingConflicts.Add(1, new KeyValuePair<string, object?>("reason", nameof(BookingError.NoServiceBayAvailable)));
                return Result.Failure<BookAppointmentResponse>(BookingError.NoServiceBayAvailable);
            }

            var appointment = Appointment.Book(
                Guid.NewGuid(),
                request.DealershipId,
                assignment.ServiceBayId,
                assignment.TechnicianId,
                serviceType,
                request.VehicleId,
                request.CustomerId,
                request.StartsAtUtc,
                now,
                request.IdempotencyKey);

            _appointments.Add(appointment);
            await _unitOfWork.SaveChangesAsync(innerCt);

            Telemetry.BookingsConfirmed.Add(1);

            return Result.Success(new BookAppointmentResponse(
                appointment.Id,
                appointment.DealershipId,
                appointment.ServiceBayId,
                appointment.TechnicianId,
                appointment.ServiceTypeId,
                appointment.VehicleId,
                appointment.CustomerId,
                appointment.StartsAtUtc,
                appointment.EndsAtUtc,
                appointment.Status.ToString()));
        }, ct);
    }
}
