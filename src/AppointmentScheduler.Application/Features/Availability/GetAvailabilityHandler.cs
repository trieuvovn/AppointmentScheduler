using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;

namespace AppointmentScheduler.Application.Features.Availability;

public sealed class GetAvailabilityHandler
{
    private readonly IAvailabilityRepository _repository;

    public GetAvailabilityHandler(IAvailabilityRepository repository) => _repository = repository;

    public async Task<Result<GetAvailabilityResponse>> HandleAsync(
        GetAvailabilityRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dealership = await _repository.FindDealershipAsync(request.DealershipId, ct);

        if (dealership is null)
        {
            return Result.Failure<GetAvailabilityResponse>(BookingError.DealershipNotFound);
        }

        var serviceType = await _repository.FindServiceTypeAsync(request.ServiceTypeId, ct);

        if (serviceType is null)
        {
            return Result.Failure<GetAvailabilityResponse>(BookingError.ServiceTypeNotFound);
        }

        var timeZone = TimeZoneResolver.Resolve(dealership.TimeZoneId);
        var openingWindow = dealership.OpeningWindowOn(request.Date, timeZone);

        if (openingWindow is null)
        {
            return Result.Success(new GetAvailabilityResponse(null, []));
        }

        var window = openingWindow.Value;

        var bays = await _repository.GetBayOccupancyAsync(request.DealershipId, window, ct);
        var technicians = await _repository.GetQualifiedTechnicianOccupancyAsync(
            request.DealershipId, request.ServiceTypeId, window, ct);

        var assignments = AvailabilitySearch.SearchDay(window, serviceType.Duration, bays, technicians);

        var slots = assignments
            .Select(a => new AvailableSlotResponse(a.Slot.Start, a.Slot.End))
            .ToList();

        return Result.Success(new GetAvailabilityResponse(window.Start, slots));
    }
}
