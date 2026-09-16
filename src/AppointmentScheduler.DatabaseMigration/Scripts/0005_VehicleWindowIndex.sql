-- Closes the Stage 4 known limitation: the vehicle-overlap check in
-- IAppointmentRepository.HasOverlappingAppointmentAsync scanned Appointments for a matching
-- VehicleId with no supporting index. This turns that scan into a seek, matching the same
-- Status IN ('Confirmed', 'InProgress') predicate already used by IX_Appointments_Bay_Window
-- and IX_Appointments_Technician_Window.
CREATE NONCLUSTERED INDEX IX_Appointments_Vehicle_Window
    ON Appointments (VehicleId, StartsAtUtc)
    INCLUDE (EndsAtUtc)
    WHERE Status IN ('Confirmed', 'InProgress');
GO
