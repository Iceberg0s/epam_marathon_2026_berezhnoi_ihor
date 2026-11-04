using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RestaurantAqa.Config;
using RestaurantAqa.Models;

namespace RestaurantAqa.Clients;

public class RestaurantApiClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public RestaurantApiClient()
    {
        _http = new HttpClient { BaseAddress = new Uri(TestSettings.ApiUrl + "/") };
    }

    public async Task<(string Email, string IdToken)> RegisterAndSignInCustomerAsync(string prefix = "aqa")
    {
        var email = $"{prefix}.{Guid.NewGuid():N}@example.com";
        var regPayload = new
        {
            firstName = "Ihor",
            lastName = "Berezhnoi",
            email,
            password = TestSettings.DefaultPassword,
            confirmPassword = TestSettings.DefaultPassword
        };
        var regResp = await PostRawAsync("register", regPayload);
        if (!regResp.IsSuccessStatusCode)
            throw new Exception($"Register failed ({regResp.StatusCode}): {await regResp.Content.ReadAsStringAsync()}");

        var signResp = await PostRawAsync("signin", new { email, password = TestSettings.DefaultPassword });
        var body = await signResp.Content.ReadAsStringAsync();
        if (!signResp.IsSuccessStatusCode)
            throw new Exception($"SignIn failed ({signResp.StatusCode}): {body}");

        var data = JsonSerializer.Deserialize<SignInResponseDto>(body, JsonOpts)!;
        return (email, data.IdToken);
    }

    public async Task<TableDto> CreateTestTableAsync(int capacity = 6)
    {
        var payload = new
        {
            locationId = TestSettings.DefaultLocationId,
            capacity,
            tableNumber = Random.Shared.Next(100, 9999)
        };
        var resp = await PostRawAsync("tables", payload);
        var body = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TableDto>(body, JsonOpts)!;
    }

    public async Task<ReservationDto> CreateReservationAsync(string idToken, string tableId, int guestCount = 4, int hoursAhead = 3)
    {
        var start = DateTime.UtcNow.AddHours(hoursAhead);
        var end = start.AddHours(2);
        var payload = new
        {
            tableId,
            startTime = start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            endTime = end.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            guestCount
        };
        var resp = await SendAsync(HttpMethod.Post, "reservations", payload, idToken);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"CreateReservation failed ({resp.StatusCode}): {body}");

        return JsonSerializer.Deserialize<ReservationDto>(body, JsonOpts)!;
    }

    public async Task CancelReservationSafeAsync(string idToken, string reservationId)
    {
        try
        {
            await SendAsync(HttpMethod.Delete, "reservations", new { id = reservationId }, idToken);
        }
        catch { /* ignore cleanup errors */ }
    }

    public async Task CreateInitialOrderAsync(string idToken, string reservationId)
    {
        var payload = new
        {
            reservationId,
            orderItems = new[] { new { dishId = TestSettings.Dish1Id, quantity = 1 } }
        };
        await SendAsync(HttpMethod.Post, "orders", payload, idToken);
    }

    public async Task<(string GroupReservationId, string InviteToken)> GetGroupInfoAsync(string idToken, string reservationId)
    {
        var tokenResp = await SendAsync(HttpMethod.Get, $"reservations/{reservationId}/invite-token", null, idToken);
        var tokenBody = await tokenResp.Content.ReadAsStringAsync();
        if (!tokenResp.IsSuccessStatusCode)
            throw new Exception($"GetInviteToken failed ({tokenResp.StatusCode}): {tokenBody}");

        var tokenDto = JsonSerializer.Deserialize<InviteTokenDto>(tokenBody, JsonOpts)!;

        var infoResp = await SendAsync(HttpMethod.Get, $"guest/invites/{tokenDto.Token}", null, null);
        var infoBody = await infoResp.Content.ReadAsStringAsync();
        var infoDto = JsonSerializer.Deserialize<GroupInviteInfoDto>(infoBody, JsonOpts)!;

        return (infoDto.GroupReservationId, tokenDto.Token);
    }

    public async Task<GuestSessionCreatedDto> JoinGroupAsGuestAsync(string groupReservationId, string guestName)
    {
        var resp = await PostRawAsync("guest/sessions", new { groupReservationId, guestName });
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"JoinGroupAsGuest failed ({resp.StatusCode}): {body}");

        return JsonSerializer.Deserialize<GuestSessionCreatedDto>(body, JsonOpts)!;
    }

    public async Task<HttpResponseMessage> AddDishToGuestCartAsync(string guestSessionId, string sessionToken, string dishId, int quantity)
    {
        return await SendAsync(HttpMethod.Post, $"guest/sessions/{guestSessionId}/dishes", new { dishId, quantity }, $"Bearer {sessionToken}");
    }

    public async Task<HttpResponseMessage> RemoveDishFromGuestCartAsync(string guestSessionId, string sessionToken, string dishId, int quantity)
    {
        return await SendAsync(HttpMethod.Delete, $"guest/sessions/{guestSessionId}/dishes", new { dishId, quantity }, $"Bearer {sessionToken}");
    }

    public async Task<HttpResponseMessage> SubmitGuestCartAsync(string guestSessionId, string sessionToken)
    {
        return await SendAsync(HttpMethod.Post, $"guest/sessions/{guestSessionId}/submit", new { }, $"Bearer {sessionToken}");
    }

    public async Task<HttpResponseMessage> GetGroupSessionsRawAsync(string? token, string groupReservationId)
    {
        return await SendAsync(HttpMethod.Get, $"guest/groups/{groupReservationId}/sessions", null, token);
    }

    public async Task<GroupSessionsSnapshotDto> GetGroupSessionsAsync(string idToken, string groupReservationId)
    {
        var resp = await GetGroupSessionsRawAsync(idToken, groupReservationId);
        var body = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GroupSessionsSnapshotDto>(body, JsonOpts)!;
    }

    public async Task<GroupStatusDto> GetGroupStatusAsync(string groupReservationId)
    {
        var resp = await SendAsync(HttpMethod.Get, $"guest/groups/{groupReservationId}/status", null, null);
        var body = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GroupStatusDto>(body, JsonOpts)!;
    }

    public async Task<HttpResponseMessage> GetOwnerSessionIdRawAsync(string? idToken, string reservationId)
    {
        return await SendAsync(HttpMethod.Get, $"reservations/{reservationId}/guest-session-id", null, idToken);
    }

    public async Task<HttpResponseMessage> SubmitGroupPreOrderRawAsync(string? idToken, string groupReservationId, string reservationId)
    {
        return await SendAsync(HttpMethod.Post, $"group-reservations/{groupReservationId}/submit", new { reservationId }, idToken);
    }

    public async Task<HttpResponseMessage> DeleteGuestSessionRawAsync(string? idToken, string groupReservationId, string guestSessionId)
    {
        return await SendAsync(HttpMethod.Delete, $"guest/groups/{groupReservationId}/sessions/{guestSessionId}", null, idToken);
    }

    public async Task<OrderResponseDto> GetOrderByReservationAsync(string idToken, string reservationId)
    {
        var resp = await SendAsync(HttpMethod.Get, $"orders/{reservationId}", null, idToken);
        var body = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OrderResponseDto>(body, JsonOpts)!;
    }

    public Task<HttpResponseMessage> PostRawAsync(string path, object payload) =>
        SendAsync(HttpMethod.Post, path, payload, null);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? payload, string? authHeader)
    {
        var req = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            req.Headers.TryAddWithoutValidation("Authorization", authHeader);
        }
        if (payload != null)
        {
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }
        return await _http.SendAsync(req);
    }

    public void Dispose() => _http.Dispose();
}
