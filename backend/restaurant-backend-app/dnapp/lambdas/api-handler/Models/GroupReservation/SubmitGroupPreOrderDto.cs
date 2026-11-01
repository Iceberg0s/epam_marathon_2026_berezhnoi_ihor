using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservation;

public class SubmitGroupPreOrderRequest
{
    [JsonPropertyName("reservationId")]
    public string ReservationId { get; set; } = string.Empty;
}

public class SubmittedDishResponse
{
    [JsonPropertyName("dishId")]
    public string DishId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public class OwnerSessionIdResponse
{
    [JsonPropertyName("guestSessionId")]
    public string GuestSessionId { get; set; } = string.Empty;

    public OwnerSessionIdResponse(string guestSessionId)
    {
        GuestSessionId = guestSessionId;
    }
}