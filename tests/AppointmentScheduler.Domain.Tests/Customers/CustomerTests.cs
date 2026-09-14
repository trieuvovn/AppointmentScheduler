using AppointmentScheduler.Domain.Customers;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Customers;

public class CustomerTests
{
    [Fact]
    public void Create_ValidRequest_KeepsTheDetailsItWasGiven()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Dana Fox", "dana@example.com", "+44 20 7946 0000");

        customer.FullName.Should().Be("Dana Fox");
        customer.Email.Should().Be("dana@example.com");
        customer.Phone.Should().Be("+44 20 7946 0000");
    }

    [Fact]
    public void Create_NoContactDetailsGiven_LeavesEmailAndPhoneNull()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Dana Fox");

        customer.Email.Should().BeNull();
        customer.Phone.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingFullName_ThrowsArgumentException(string? fullName)
    {
        var act = () => Customer.Create(Guid.NewGuid(), fullName!);

        act.Should().Throw<ArgumentException>();
    }
}
