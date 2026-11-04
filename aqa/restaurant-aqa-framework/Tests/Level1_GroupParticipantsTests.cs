using System.Net;
using NUnit.Framework;
using RestaurantAqa.Clients;
using RestaurantAqa.Config;

namespace RestaurantAqa.Tests;

[TestFixture]
[Category("Level1")]
[Category("API")]
public class Level1_GroupParticipantsTests
{
    private RestaurantApiClient _api = null!;
    private string _ownerToken = null!;
    private string? _createdReservationId;

    [SetUp]
    public async Task SetUp()
    {
        _api = new RestaurantApiClient();
        (_, _ownerToken) = await _api.RegisterAndSignInCustomerAsync("l1owner");
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_createdReservationId != null)
            await _api.CancelReservationSafeAsync(_ownerToken, _createdReservationId);
        _api.Dispose();
    }

    [Test(Description = "L1: Група без учасників повертає порожній список participants: [] та total: 0")]
    public async Task EmptyGroup_ReturnsEmptyParticipantsAndZeroTotal()
    {
        var table = await _api.CreateTestTableAsync(4);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 4);
        _createdReservationId = res.Id;

        var (groupId, _) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);
        var snapshot = await _api.GetGroupSessionsAsync(_ownerToken, groupId);

        Assert.That(snapshot.Participants, Is.Empty);
        Assert.That(snapshot.Total, Is.EqualTo(0m));
    }

    [Test(Description = "L1: Owner бачить усіх учасників, їхні страви, суми, статус підтвердження та динамічну зміну кошика")]
    public async Task Owner_SeesAllParticipantsDishesTotals_AndDynamicCartUpdates()
    {
        var table = await _api.CreateTestTableAsync(6);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 5);
        _createdReservationId = res.Id;

        var (groupId, _) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);

        // Гість 1 додає страви та підтверджує кошик
        var guest1 = await _api.JoinGroupAsGuestAsync(groupId, "GuestOne");
        await _api.AddDishToGuestCartAsync(guest1.SessionId, guest1.SessionToken, TestSettings.Dish1Id, 2); // 2 * 12.00 = 24.00
        await _api.SubmitGuestCartAsync(guest1.SessionId, guest1.SessionToken);

        // Гість 2 додає страву, але НЕ підтверджує кошик
        var guest2 = await _api.JoinGroupAsGuestAsync(groupId, "GuestTwo");
        await _api.AddDishToGuestCartAsync(guest2.SessionId, guest2.SessionToken, TestSettings.Dish2Id, 1); // 1 * 14.50 = 14.50

        // Гість 3 приєднується без страв
        var guest3 = await _api.JoinGroupAsGuestAsync(groupId, "GuestEmpty");

        var snapshot = await _api.GetGroupSessionsAsync(_ownerToken, groupId);

        Assert.That(snapshot.Participants.Count, Is.EqualTo(3));

        var p1 = snapshot.Participants.Single(p => p.Name == "GuestOne");
        var p2 = snapshot.Participants.Single(p => p.Name == "GuestTwo");
        var p3 = snapshot.Participants.Single(p => p.Name == "GuestEmpty");

        Assert.Multiple(() =>
        {
            Assert.That(p1.IsSubmitted, Is.True);
            Assert.That(p1.Total, Is.GreaterThan(0m));
            Assert.That(p2.IsSubmitted, Is.False);
            Assert.That(p2.Total, Is.GreaterThan(0m));
            Assert.That(p3.Items, Is.Empty);
            Assert.That(p3.Total, Is.EqualTo(0m));
            Assert.That(snapshot.Total, Is.EqualTo(p1.Total + p2.Total + p3.Total));
        });

        // Гість 2 додає ще одну страву — загальна сума динамічно збільшується
        await _api.AddDishToGuestCartAsync(guest2.SessionId, guest2.SessionToken, TestSettings.Dish1Id, 1); // +12.00
        var updatedSnapshot = await _api.GetGroupSessionsAsync(_ownerToken, groupId);
        Assert.That(updatedSnapshot.Total, Is.GreaterThan(snapshot.Total));
    }

    [Test(Description = "L1: Список учасників бачить лише Owner — інший клієнт отримує 403, неавторизований 401")]
    public async Task GetParticipants_AccessControl_ForbidsOtherCustomerAndUnauthorized()
    {
        var table = await _api.CreateTestTableAsync(4);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 3);
        _createdReservationId = res.Id;

        var (groupId, _) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);

        // Інший зареєстрований клієнт -> 403 Forbidden
        var (_, otherCustomerToken) = await _api.RegisterAndSignInCustomerAsync("other");
        var otherResp = await _api.GetGroupSessionsRawAsync(otherCustomerToken, groupId);
        Assert.That(otherResp.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // Неавторизований запит -> 401 Unauthorized
        var unauthResp = await _api.GetGroupSessionsRawAsync(null, groupId);
        Assert.That(unauthResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test(Description = "L1: Запит неіснуючої групи повертає 404 NotFound")]
    public async Task GetParticipants_NonExistentGroup_Returns404()
    {
        var resp = await _api.GetGroupSessionsRawAsync(_ownerToken, Guid.NewGuid().ToString());
        Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
