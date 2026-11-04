using System;

namespace APIHandler.Models.GuestReservation;

public record GetGroupReservationResponse(
    string GroupReservationId,
    string ReservationId,
    string LocationId,
    string RestaurantName,
    string LocationAddress,
    DateTime StartTime,
    DateTime EndTime,
    int MaxGuestCount,
    int TableNumber,
    string InviterName
);