using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestSessions;
using APIHandler.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Services.GuestSessions;

public interface IGuestSessionService
{
    Task<CreatGuestSessionResponse> CreateGuestSessionAsync(CreateGuestSessionRequest request);
    Task AddDishAsync(string guestSessionId, string dishId, int quantity);
    Task RemoveDishAsync(string guestSessionId, string dishId, int quantity);
    Task<GroupReservationStatusResponse> GetGroupReservationStatusAsync(string groupReservationId);
    Task<bool> IsAuthorizedAsync(string guestSessionToken, string guestSessionId);
    Task SubmitCartAsync(string guestSessionId);
    Task<GuestMenuResponse> GetGuestMenuAsync(string guestSessionId);
    Task<IEnumerable<Dish>> GetOwnerMenuAsync(string userId, string reservationId);
    Task<GetAllSessionsResponse> DeleteGuestSessionAsync(string userId, string groupReservationId, string guestSessionId);
    Task<GetGuestSessionResponse> GetGuestSessionInfoAsync(string guestSessionId);
    Task<GetAllSessionsResponse> GetGroupSessionsAsync(string userId, string groupReservationId);
    Task<string> GetOwnerSessionIdAsync(string userId, string reservationId);
    Task<List<SubmittedDishResponse>> SubmitGroupPreOrderAsync(string userId, string groupReservationId, string reservationId);

}
