using FluentValidation;

namespace AppointmentScheduler.Application.Features.Booking;

public sealed class BookAppointmentValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentValidator()
    {
        RuleFor(r => r.DealershipId).NotEmpty();
        RuleFor(r => r.ServiceTypeId).NotEmpty();
        RuleFor(r => r.VehicleId).NotEmpty();
        RuleFor(r => r.CustomerId).NotEmpty();
        RuleFor(r => r.StartsAtUtc.Offset)
            .Equal(TimeSpan.Zero)
            .WithMessage("StartsAtUtc must be expressed with a zero UTC offset.");
    }
}
