using System.Net;
using System.Text.Json;
using NUnit.Framework;
using RestaurantAqa.Clients;
using RestaurantAqa.Config;
using RestaurantAqa.Models;

namespace RestaurantAqa.Tests;

[TestFixture]
[Category("Level3")]
[Category("API")]
public class Level3_GroupDeleteParticipantTests
{
    private RestaurantApiClient _api = null!;
    private string _ownerToken = null!;
    private readonly List<string> _reservationsToClean = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [SetUp]
    public async Task SetUp()
    {
        _api = new RestaurantApiClient();
        (_, _ownerToken) = await _api.RegisterAndSignInCustomerAsync("l3owner");
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var id in _reservationsToClean)
            await _api.CancelReservationSafeAsync(_ownerToken, id);
        _reservationsToClean.Clear();
        _api.Dispose();
    }

    [Test(Description = "L3 E2E: Кілька гостей додають страви -> Owner видаляє одного учасника -> підтверджує -> у замовленні лише страви залишених учасників")]
    public async Task EndToEnd_DeleteSubmittedParticipant_RecalculatesTotal_AndExcludesFromFinalOrder()
    {
        var table = await _api.CreateTestTableAsync(6);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 5);
        _reservationsToClean.Add(res.Id);

        await _api.CreateInitialOrderAsync(_ownerToken, res.Id);
        var (groupId, _) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);

        var g1 = await _api.JoinGroupAsGuestAsync(groupId, "KeepGuest");
        await _api.AddDishToGuestCartAsync(g1.SessionId, g1.SessionToken, TestSettings.Dish1Id, 2);
        await _api.SubmitGuestCartAsync(g1.SessionId, g1.SessionToken);

        var g2 = await _api.JoinGroupAsGuestAsync(groupId, "RemoveGuest");
        await _api.AddDishToGuestCartAsync(g2.SessionId, g2.SessionToken, TestSettings.Dish2Id, 1);
        await _api.SubmitGuestCartAsync(g2.SessionId, g2.SessionToken);

        var delResp = await _api.DeleteGuestSessionRawAsync(_ownerToken, groupId, g2.SessionId);
        Assert.That(delResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var snapshotAfterDelete = await _api.GetGroupSessionsAsync(_ownerToken, groupId);
        Assert.Multiple(() =>
        {
            Assert.That(snapshotAfterDelete.Participants.Count, Is.EqualTo(1));
            Assert.That(snapshotAfterDelete.Participants[0].Name, Is.EqualTo("KeepGuest"));
            Assert.That(snapshotAfterDelete.Total, Is.GreaterThan(0m));
        });

        var submitResp = await _api.SubmitGroupPreOrderRawAsync(_ownerToken, groupId, res.Id);
        var submitBody = await submitResp.Content.ReadAsStringAsync();
        Assert.That(submitResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), submitBody);

        var submittedDishes = JsonSerializer.Deserialize<List<SubmittedDishDto>>(submitBody, JsonOpts)!;
        Assert.Multiple(() =>
        {
            Assert.That(submittedDishes.Any(i => i.DishId == TestSettings.Dish2Id), Is.False);
            Assert.That(submittedDishes.Single(i => i.DishId == TestSettings.Dish1Id).Quantity, Is.EqualTo(2));
        });
    }

    [Test(Description = "L3: Видалення останнього учасника залишає порожній список та суму 0")]
    public async Task DeleteLastParticipant_ReturnsEmptyListAndZeroTotal()
    {
        var table = await _api.CreateTestTableAsync(4);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 3);
        _reservationsToClean.Add(res.Id);

        var (groupId, _) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);
        var soloGuest = await _api.JoinGroupAsGuestAsync(groupId, "SoloGuest");

        var delResp = await _api.DeleteGuestSessionRawAsync(_ownerToken, groupId, soloGuest.SessionId);
        Assert.That(delResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var snapshot = await _api.GetGroupSessionsAsync(_ownerToken, groupId);
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Participants, Is.Empty);
            Assert.That(snapshot.Total, Is.EqualTo(0m));
        });
    }

    [Test(Description = "L3 Негативні: Учасник іншої групи (400), неіснуючий учасник (404), видалення не від Owner (403)")]
    public async Task DeleteParticipant_ValidationAndAuthorizationChecks()
    {
        var table1 = await _api.CreateTestTableAsync(4);
        var table2 = await _api.CreateTestTableAsync(4);

        var res1 = await _api.CreateReservationAsync(_ownerToken, table1.Id, guestCount: 3, hoursAhead: 4);
        var res2 = await _api.CreateReservationAsync(_ownerToken, table2.Id, guestCount: 3, hoursAhead: 6);
        _reservationsToClean.AddRange([res1.Id, res2.Id]);

        var (group1Id, _) = await _api.GetGroupInfoAsync(_ownerToken, res1.Id);
        var (group2Id, _) = await _api.GetGroupInfoAsync(_ownerToken, res2.Id);

        var guestInGroup2 = await _api.JoinGroupAsGuestAsync(group2Id, "Group2Guest");

        var crossGroupResp = await _api.DeleteGuestSessionRawAsync(_ownerToken, group1Id, guestInGroup2.SessionId);
        Assert.That(crossGroupResp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var notFoundResp = await _api.DeleteGuestSessionRawAsync(_ownerToken, group1Id, Guid.NewGuid().ToString());
        Assert.That(notFoundResp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var (_, otherUserToken) = await _api.RegisterAndSignInCustomerAsync("otherL3");
        var forbiddenResp = await _api.DeleteGuestSessionRawAsync(otherUserToken, group2Id, guestInGroup2.SessionId);
        Assert.That(forbiddenResp.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
