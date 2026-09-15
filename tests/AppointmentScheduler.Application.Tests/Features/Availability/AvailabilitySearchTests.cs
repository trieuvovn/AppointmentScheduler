using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Application.Tests.Features.Availability;

public class AvailabilitySearchTests
{
    private static readonly DateTimeOffset Monday0800 = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private static readonly TimeSlot OpeningWindow =
        TimeSlot.Create(Monday0800, Monday0800.AddHours(10)); // 08:00-18:00

    private static readonly ResourceOccupancy FreeBay = new(Guid.NewGuid(), 0, []);
    private static readonly ResourceOccupancy FreeTechnician = new(Guid.NewGuid(), 1, []);

    [Fact]
    public void Search_AClosedDay_IsOutsideOpeningHours()
    {
        var slot = TimeSlot.FromDuration(Monday0800.AddHours(1), TimeSpan.FromMinutes(30));

        var result = AvailabilitySearch.Search(slot, null, [FreeBay], [FreeTechnician]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BookingError.OutsideOpeningHours);
    }

    [Fact]
    public void Search_ASlotBeforeOpening_IsOutsideOpeningHours()
    {
        var slot = TimeSlot.FromDuration(Monday0800.AddHours(-1), TimeSpan.FromMinutes(30));

        var result = AvailabilitySearch.Search(slot, OpeningWindow, [FreeBay], [FreeTechnician]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BookingError.OutsideOpeningHours);
    }

    [Fact]
    public void Search_AServiceRunningPastClosing_IsCrossesClosingTime()
    {
        // Saturday 09:00-13:00 window, a 240-minute EV check starting at closing minus 60.
        var saturday0900 = new DateTimeOffset(2026, 3, 7, 9, 0, 0, TimeSpan.Zero);
        var saturdayWindow = TimeSlot.Create(saturday0900, saturday0900.AddHours(4));
        var slot = TimeSlot.FromDuration(saturday0900.AddHours(3), TimeSpan.FromMinutes(240));

        var result = AvailabilitySearch.Search(slot, saturdayWindow, [FreeBay], [FreeTechnician]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BookingError.CrossesClosingTime);
    }

    [Fact]
    public void Search_EveryBayBusy_IsNoServiceBayAvailable()
    {
        var slot = TimeSlot.FromDuration(Monday0800.AddHours(1), TimeSpan.FromMinutes(30));
        var busyBay = new ResourceOccupancy(Guid.NewGuid(), 0, [slot]);

        var result = AvailabilitySearch.Search(slot, OpeningWindow, [busyBay], [FreeTechnician]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BookingError.NoServiceBayAvailable);
    }

    [Fact]
    public void Search_EveryQualifiedTechnicianBusy_IsNoQualifiedTechnicianAvailable()
    {
        var slot = TimeSlot.FromDuration(Monday0800.AddHours(1), TimeSpan.FromMinutes(30));
        var busyTechnician = new ResourceOccupancy(Guid.NewGuid(), 1, [slot]);

        var result = AvailabilitySearch.Search(slot, OpeningWindow, [FreeBay], [busyTechnician]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BookingError.NoQualifiedTechnicianAvailable);
    }

    [Fact]
    public void Search_ATouchingAppointment_IsStillAvailable()
    {
        // Busy 09:00-09:30; the candidate starts exactly at 09:30 — touching, not overlapping.
        var busyStart = Monday0800.AddHours(1);
        var busySlot = TimeSlot.FromDuration(busyStart, TimeSpan.FromMinutes(30));
        var bay = new ResourceOccupancy(Guid.NewGuid(), 0, [busySlot]);

        var candidate = TimeSlot.FromDuration(busySlot.End, TimeSpan.FromMinutes(30));

        var result = AvailabilitySearch.Search(candidate, OpeningWindow, [bay], [FreeTechnician]);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Search_TheOnlyFreeBayAndTheOnlyFreeTechnicianAreDifferentCandidates_StillSucceeds()
    {
        // Two bays, two technicians. Bay A is busy but Technician A is free; Bay B is free but
        // Technician B is busy. Steps 3 and 4 are independent scans (problem 1.2): there must be
        // no pairing rule that requires the same "index" to be free in both pools.
        var slot = TimeSlot.FromDuration(Monday0800.AddHours(1), TimeSpan.FromMinutes(30));

        var busyBay = new ResourceOccupancy(Guid.NewGuid(), 0, [slot]);
        var freeBay = new ResourceOccupancy(Guid.NewGuid(), 0, []);

        var freeTechnician = new ResourceOccupancy(Guid.NewGuid(), 1, []);
        var busyTechnician = new ResourceOccupancy(Guid.NewGuid(), 2, [slot]);

        var result = AvailabilitySearch.Search(
            slot, OpeningWindow, [busyBay, freeBay], [freeTechnician, busyTechnician]);

        result.IsSuccess.Should().BeTrue();
        result.Assignment!.ServiceBayId.Should().Be(freeBay.ResourceId);
        result.Assignment!.TechnicianId.Should().Be(freeTechnician.ResourceId);
    }

    [Fact]
    public void Search_DoesNotReSortCandidates_TakesTheFirstOfEachAsGiven()
    {
        // The repository's ordering is authority; Search must take the first free candidate in
        // the given order, not re-sort by some other key such as skill count.
        var slot = TimeSlot.FromDuration(Monday0800.AddHours(1), TimeSpan.FromMinutes(30));

        var firstBay = new ResourceOccupancy(Guid.NewGuid(), 0, []);
        var secondBay = new ResourceOccupancy(Guid.NewGuid(), 0, []);

        // Given out of skill-count order deliberately — Search must not reorder by SkillCount.
        var narrowerButSecond = new ResourceOccupancy(Guid.NewGuid(), 1, []);
        var widerButFirst = new ResourceOccupancy(Guid.NewGuid(), 5, []);

        var result = AvailabilitySearch.Search(
            slot, OpeningWindow, [firstBay, secondBay], [widerButFirst, narrowerButSecond]);

        result.Assignment!.ServiceBayId.Should().Be(firstBay.ResourceId);
        result.Assignment!.TechnicianId.Should().Be(widerButFirst.ResourceId);
    }

    [Fact]
    public void CandidateSlots_AFourHourWindow_GeneratesEveryThirtyMinuteStart()
    {
        var slots = AvailabilitySearch.CandidateSlots(
            TimeSlot.Create(Monday0800, Monday0800.AddHours(4)), TimeSpan.FromMinutes(30));

        slots.Should().HaveCount(8);
        slots[0].Start.Should().Be(Monday0800);
        slots[^1].Start.Should().Be(Monday0800.AddHours(3).AddMinutes(30));
    }

    [Fact]
    public void SearchDay_AClosedDay_ReturnsEmpty()
    {
        var assignments = AvailabilitySearch.SearchDay(null, TimeSpan.FromMinutes(30), [FreeBay], [FreeTechnician]);

        assignments.Should().BeEmpty();
    }

    [Fact]
    public void SearchDay_AShortWindowThatCannotFitTheDuration_ReturnsEmpty()
    {
        // Saturday 09:00-13:00 (4h) window against a 240-minute service: only the 09:00 start
        // fits exactly; anything past it crosses closing. This is the "straddles a lunch break"
        // behaviour stand-in noted in stage3.md's doc updates.
        var saturday0900 = new DateTimeOffset(2026, 3, 7, 9, 0, 0, TimeSpan.Zero);
        var window = TimeSlot.Create(saturday0900, saturday0900.AddHours(4));

        var assignments = AvailabilitySearch.SearchDay(
            window, TimeSpan.FromMinutes(241), [FreeBay], [FreeTechnician]);

        assignments.Should().BeEmpty();
    }

    [Fact]
    public void SearchDay_AnOpenDayWithFreeResources_ReturnsEverySlotThatFits()
    {
        var window = OpeningWindow; // 08:00-18:00

        var assignments = AvailabilitySearch.SearchDay(
            window, TimeSpan.FromMinutes(30), [FreeBay], [FreeTechnician]);

        assignments.Should().HaveCount(20); // 10 hours / 30 minutes
        assignments[0].Slot.Start.Should().Be(window.Start);
    }
}
