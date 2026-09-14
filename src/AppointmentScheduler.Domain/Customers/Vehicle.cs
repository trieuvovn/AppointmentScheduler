namespace AppointmentScheduler.Domain.Customers;

/// <summary>
/// The car being serviced, identified by its VIN.
/// </summary>
public sealed class Vehicle
{
    /// <summary>A VIN is a fixed 17 characters by international standard.</summary>
    public const int VinLength = 17;

    private Vehicle(
        Guid id,
        Guid customerId,
        string vin,
        string make,
        string model,
        string? licensePlate,
        short? modelYear)
    {
        Id = id;
        CustomerId = customerId;
        Vin = vin;
        Make = make;
        Model = model;
        LicensePlate = licensePlate;
        ModelYear = modelYear;
    }

    /// <summary>Rehydration constructor for EF Core.</summary>
    private Vehicle()
    {
        Vin = string.Empty;
        Make = string.Empty;
        Model = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Vin { get; private set; }

    public string? LicensePlate { get; private set; }

    public string Make { get; private set; }

    public string Model { get; private set; }

    public short? ModelYear { get; private set; }

    public static Vehicle Create(
        Guid id,
        Guid customerId,
        string vin,
        string make,
        string model,
        string? licensePlate = null,
        short? modelYear = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);
        ArgumentException.ThrowIfNullOrWhiteSpace(make);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (vin.Length != VinLength)
        {
            throw new ArgumentException(
                $"A VIN must be exactly {VinLength} characters, but got {vin.Length}.", nameof(vin));
        }

        return new Vehicle(id, customerId, vin, make, model, licensePlate, modelYear);
    }
}
