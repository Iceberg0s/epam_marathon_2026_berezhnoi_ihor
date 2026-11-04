using System.Net;
using System.Text.Json;
using NUnit.Framework;
using RestaurantAqa.Clients;
using RestaurantAqa.Config;
using RestaurantAqa.Models;

namespace RestaurantAqa.Tests;

[TestFixture]
[Category("Level2")]
[Category("API")]
public class Level2_GroupConsolidationSubmitTests
{
    private RestaurantApiClient _api = null!;
    private string _ownerToken = null!;
    private readonly List<string> _reservationsToClean = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [SetUp]
    public async Task SetUp()
    {
        _api = new RestaurantApiClient();
        (_, _ownerToken) = await _api.RegisterAndSignInCustomerAsync("l2owner");
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var id in _reservationsToClean)
            await _api.CancelReservationSafeAsync(_ownerToken, id);
        _reservationsToClean.Clear();
        _api.Dispose();
    }

    [Test(Description = "L2: Owner отримує власну сесію в групі, однакові страви підсумовуються, непідтверджені кошики ігноруються")]
    public async Task SubmitGroupPreOrder_ConsolidatesSubmittedCarts_AndUpdatesStatuses()
    {
        var table = await _api.CreateTestTableAsync(6);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 5);
        _reservationsToClean.Add(res.Id);

        await _api.CreateInitialOrderAsync(_ownerToken, res.Id);
        var (groupId, _) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);

        var ownerSessResp = await _api.GetOwnerSessionIdRawAsync(_ownerToken, res.Id);
        Assert.That(ownerSessResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var g1 = await _api.JoinGroupAsGuestAsync(groupId, "Alice");
        await _api.AddDishToGuestCartAsync(g1.SessionId, g1.SessionToken, TestSettings.Dish1Id, 2);
        await _api.SubmitGuestCartAsync(g1.SessionId, g1.SessionToken);

        var g2 = await _api.JoinGroupAsGuestAsync(groupId, "Bob");
        await _api.AddDishToGuestCartAsync(g2.SessionId, g2.SessionToken, TestSettings.Dish1Id, 3);
        await _api.AddDishToGuestCartAsync(g2.SessionId, g2.SessionToken, TestSettings.Dish2Id, 1);
        await _api.SubmitGuestCartAsync(g2.SessionId, g2.SessionToken);

        var g3 = await _api.JoinGroupAsGuestAsync(groupId, "UnsubmittedCharlie");
        await _api.AddDishToGuestCartAsync(g3.SessionId, g3.SessionToken, TestSettings.Dish2Id, 5);

        var submitResp = await _api.SubmitGroupPreOrderRawAsync(_ownerToken, groupId, res.Id);
        var submitBody = await submitResp.Content.ReadAsStringAsync();
        Assert.That(submitResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), submitBody);

        var submittedDishes = JsonSerializer.Deserialize<List<SubmittedDishDto>>(submitBody, JsonOpts)!;
        var item1 = submittedDishes.Single(i => i.DishId == TestSettings.Dish1Id);
        var item2 = submittedDishes.Single(i => i.DishId == TestSettings.Dish2Id);

        var status = await _api.GetGroupStatusAsync(groupId);

        Assert.Multiple(() =>
        {
            Assert.That(item1.Quantity, Is.EqualTo(5));
            Assert.That(item2.Quantity, Is.EqualTo(1));
            Assert.That(status.Status.ToString(), Is.EqualTo("Submitted").Or.EqualTo("1"));
        });
    }

    [Test(Description = "L2 Негативний: Неможливо підтвердити групу без жодного підтвердженого кошика (400)")]
    public async Task SubmitGroupPreOrder_WithoutSubmittedCarts_Returns400()
    {
        var table = await _api.CreateTestTableAsync(4);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 3);
        _reservationsToClean.Add(res.Id);

        await _api.CreateInitialOrderAsync(_ownerToken, res.Id);
        var (groupId, _) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);

        var g1 = await _api.JoinGroupAsGuestAsync(groupId, "NotSubmittedGuest");
        await _api.AddDishToGuestCartAsync(g1.SessionId, g1.SessionToken, TestSettings.Dish1Id, 1);

        var resp = await _api.SubmitGroupPreOrderRawAsync(_ownerToken, groupId, res.Id);
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(body, Does.Contain("No submitted guest sessions found"));
        });
    }

    [Test(Description = "L2 Негативний: Неможливо підтвердити при невідповідності reservationId групі (400)")]
    public async Task SubmitGroupPreOrder_WhenReservationMismatchOrOrderMissing_Returns400()
    {
        var table1 = await _api.CreateTestTableAsync(4);
        var table2 = await _api.CreateTestTableAsync(4);
        var res1 = await _api.CreateReservationAsync(_ownerToken, table1.Id, guestCount: 3, hoursAhead: 3);
        var res2 = await _api.CreateReservationAsync(_ownerToken, table2.Id, guestCount: 3, hoursAhead: 6);
        _reservationsToClean.AddRange([res1.Id, res2.Id]);

        var (group1Id, _) = await _api.GetGroupInfoAsync(_ownerToken, res1.Id);

        var resp = await _api.SubmitGroupPreOrderRawAsync(_ownerToken, group1Id, res2.Id);
        Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test(Description = "L2 Негативний: Підтвердження не від Owner повертає 403, неіснуючої групи — 404")]
    public async Task SubmitGroupPreOrder_NonOwnerReturns403_NonExistentReturns404()
    {
        var table = await _api.CreateTestTableAsync(4);
        var res = await _api.CreateReservationAsync(_ownerToken, table.Id, guestCount: 3);
        _reservationsToClean.Add(res.Id);

        var (groupId, _) = await _api.GetGroupInfoAsync(_ownerToken, res.Id);

        var (_, strangerToken) = await _api.RegisterAndSignInCustomerAsync("stranger");
        var forbiddenResp = await _api.SubmitGroupPreOrderRawAsync(strangerToken, groupId, res.Id);
        Assert.That(forbiddenResp.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var notFoundResp = await _api.SubmitGroupPreOrderRawAsync(_ownerToken, Guid.NewGuid().ToString(), res.Id);
        Assert.That(notFoundResp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
