namespace AppointmentScheduler.Domain.Customers;

/// <summary>The vehicle owner.</summary>
public sealed class Customer
{
    private Customer(Guid id, string fullName, string? email, string? phone)
    {
        Id = id;
        FullName = fullName;
        Email = email;
        Phone = phone;
    }

    /// <summary>Rehydration constructor for EF Core.</summary>
    private Customer()
    {
        FullName = string.Empty;
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public static Customer Create(Guid id, string fullName, string? email = null, string? phone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new Customer(id, fullName, email, phone);
    }
}
