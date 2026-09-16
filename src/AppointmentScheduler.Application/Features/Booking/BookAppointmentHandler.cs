using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
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

        var dealership = await _availability.FindDealershipAsync(request.DealershipId, ct);

        if (dealership is null)
        {
            return Result.Failure<BookAppointmentResponse>(BookingError.DealershipNotFound);
        }

        var serviceType = await _availability.FindServiceTypeAsync(request.ServiceTypeId, ct);

        if (serviceType is null)
        {
            return Result.Failure<BookAppointmentResponse>(BookingError.ServiceTypeNotFound);
        }

        var now = _timeProvider.GetUtcNow();

        var ruleError = BookingRules.CanBeBookedAt(
            request.StartsAtUtc, now, _horizon.MinimumLeadTime, _horizon.MaximumHorizon);

        if (ruleError is { } error)
        {
            return Result.Failure<BookAppointmentResponse>(error);
        }

        if (!await _appointments.VehicleBelongsToCustomerAsync(request.VehicleId, request.CustomerId, ct))
        {
            return Result.Failure<BookAppointmentResponse>(BookingError.VehicleNotFound);
        }

        var slot = TimeSlot.FromDuration(request.StartsAtUtc, serviceType.Duration);
        var timeZone = TimeZoneResolver.Resolve(dealership.TimeZoneId);

        var localStart = TimeZoneInfo.ConvertTime(request.StartsAtUtc, timeZone);
        var openingWindow = dealership.OpeningWindowOn(DateOnly.FromDateTime(localStart.DateTime), timeZone);

        var bays = await _availability.GetBayOccupancyAsync(request.DealershipId, slot, ct);
        var technicians = await _availability.GetQualifiedTechnicianOccupancyAsync(
            request.DealershipId, request.ServiceTypeId, slot, ct);

        var search = AvailabilitySearch.Search(slot, openingWindow, bays, technicians);

        if (!search.IsSuccess)
        {
            return Result.Failure<BookAppointmentResponse>(search.Error!.Value);
        }

        var assignment = search.Assignment!;

        return await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _appointments.LockResourcesAsync(assignment.ServiceBayId, assignment.TechnicianId, innerCt);

            if (await _appointments.HasOverlappingAppointmentAsync(request.VehicleId, slot, innerCt))
            {
                return Result.Failure<BookAppointmentResponse>(BookingError.VehicleAlreadyBooked);
            }

            if (!await _availability.IsStillFreeAsync(
                assignment.ServiceBayId, assignment.TechnicianId, slot, innerCt))
            {
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
