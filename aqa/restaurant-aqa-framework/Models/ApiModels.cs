using System.Text.Json;

namespace RestaurantAqa.Models;

public record SignInResponseDto(string Message, string IdToken, string AccessToken);
public record TableDto(string Id, string LocationId, int Capacity, int TableNumber);
public record ReservationDto(string Id, string TableId, DateTime StartTime, DateTime EndTime, int GuestCount, JsonElement Status);
public record InviteTokenDto(string Token);
public record GroupInviteInfoDto(string GroupReservationId, string LocationId, string RestaurantName, string LocationAddress, int MaxGuestCount);
public record GuestSessionCreatedDto(string SessionId, string SessionToken);
public record OwnerSessionDto(string GuestSessionId);
public record DishDto(string Id, string Name, decimal Price, JsonElement DishType, JsonElement State);
public record OrderItemDto(DishDto Dish, int Quantity);
public record ParticipantDto(string? Id, JsonElement Type, string Name, List<OrderItemDto> Items, decimal Total, bool IsSubmitted);
public record GroupSessionsSnapshotDto(List<ParticipantDto> Participants, decimal Total);
public record GroupStatusDto(int CurrentParticipants, int MaxCapacity, JsonElement Status);
public record SubmittedDishDto(string DishId, int Quantity);
